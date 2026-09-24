using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    public enum WirelessAdbStatus
    {
        Listening,
        NotListening,
        Unknown
    }

    /// <summary>
    /// Developer-only helper for opening wireless ADB settings and probing the legacy TCP port.
    /// It never enables ADB or changes Android system debug state.
    /// </summary>
    public sealed class WirelessAdbHelper : MonoBehaviour
    {
        public const int DefaultAdbPort = 5555;
        public const int MaxProbeTimeoutMs = 500;
        public static readonly string[] WirelessDebuggingSettingsActions =
        {
            "android.settings.WIRELESS_DEBUGGING_SETTINGS",
            "android.settings.APPLICATION_DEVELOPMENT_SETTINGS"
        };

        private Canvas _canvas;
        private GameObject _page;
        private Text _ipText, _portText, _statusText, _commandText, _helpText;
        private Action _onBack;
        private bool _initialized;
        private Coroutine _probeRoutine;
        private int _probeGeneration;

        public bool IsVisible => _page != null && _page.activeInHierarchy;
        public static bool IsDeveloperToolsAvailable =>
#if QPS_DEV_TOOLS || DEVELOPMENT_BUILD || UNITY_EDITOR
            true;
#else
            false;
#endif

        public void Initialize(Canvas canvas, Action onBack)
        {
            if (_initialized) return;
            if (canvas == null) throw new ArgumentException("Wireless ADB helper requires a canvas");
            _canvas = canvas;
            _onBack = onBack;
            Build();
            _initialized = true;
        }

        public void Show()
        {
            if (!_initialized) throw new InvalidOperationException("Initialize Wireless ADB helper before showing it");
            _page.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            CancelProbe();
            if (_page != null) _page.SetActive(false);
        }

        public void Refresh()
        {
            if (!_initialized || !IsVisible) return;

            CancelProbe();
            var ip = GetCurrentIpv4();
            _ipText.text = "Quest IP: " + (string.IsNullOrEmpty(ip) ? "Unavailable" : ip);
            _portText.text = "ADB TCP: :" + DefaultAdbPort;

            if (string.IsNullOrEmpty(ip))
            {
                SetProbeState(WirelessAdbStatus.Unknown, string.Empty);
                _helpText.text = "Quest IP is unavailable. Connect to Wi-Fi, then tap Refresh.";
                return;
            }

            SetProbeState(WirelessAdbStatus.Unknown, BuildConnectCommand(ip, DefaultAdbPort));
            _helpText.text = "Checking port 5555...";
            _probeRoutine = StartCoroutine(ProbeRoutine(ip, _probeGeneration));
        }

        public static string SelectIpv4(IEnumerable<string> addresses)
        {
            if (addresses == null) return string.Empty;
            string fallback = string.Empty;
            foreach (var value in addresses)
            {
                if (!IsIpv4(value) || IPAddress.IsLoopback(IPAddress.Parse(value))) continue;
                if (string.IsNullOrEmpty(fallback)) fallback = value;
                if (IsPrivateIpv4(value)) return value;
            }
            return fallback;
        }

        public static bool IsIpv4(string value)
        {
            return IPAddress.TryParse(value, out var address) && address.AddressFamily == AddressFamily.InterNetwork;
        }

        public static string BuildConnectCommand(string ip, int port = DefaultAdbPort)
        {
            return IsIpv4(ip) && port > 0 && port <= 65535 ? $"adb connect {ip}:{port}" : string.Empty;
        }

        public static WirelessAdbStatus ProbePort(string ip, int port = DefaultAdbPort, int timeoutMs = MaxProbeTimeoutMs)
        {
            if (!IsIpv4(ip) || port <= 0 || port > 65535) return WirelessAdbStatus.Unknown;
            var timeout = Math.Max(1, Math.Min(MaxProbeTimeoutMs, timeoutMs));
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(ip, port, null, null);
                    try
                    {
                        if (!result.AsyncWaitHandle.WaitOne(timeout)) return WirelessAdbStatus.NotListening;
                        client.EndConnect(result);
                        return client.Connected ? WirelessAdbStatus.Listening : WirelessAdbStatus.NotListening;
                    }
                    finally
                    {
                        result.AsyncWaitHandle.Close();
                    }
                }
            }
            catch (SocketException)
            {
                return WirelessAdbStatus.NotListening;
            }
            catch (Exception)
            {
                return WirelessAdbStatus.Unknown;
            }
        }

        private IEnumerator ProbeRoutine(string ip, int generation)
        {
            WirelessAdbStatus status = WirelessAdbStatus.Unknown;
            using (var client = new TcpClient())
            {
                IAsyncResult result;
                try
                {
                    result = client.BeginConnect(ip, DefaultAdbPort, null, null);
                }
                catch (SocketException)
                {
                    status = WirelessAdbStatus.NotListening;
                    result = null;
                }
                catch (Exception)
                {
                    result = null;
                }

                var deadline = Time.realtimeSinceStartup + MaxProbeTimeoutMs / 1000f;
                while (result != null && !result.IsCompleted && Time.realtimeSinceStartup < deadline)
                {
                    if (generation != _probeGeneration || !IsVisible) yield break;
                    yield return null;
                }

                if (generation != _probeGeneration || !IsVisible) yield break;
                if (result == null)
                {
                    // Keep Unknown for unexpected socket setup failures.
                }
                else if (!result.IsCompleted)
                {
                    status = WirelessAdbStatus.NotListening;
                }
                else
                {
                    try
                    {
                        client.EndConnect(result);
                        status = client.Connected ? WirelessAdbStatus.Listening : WirelessAdbStatus.NotListening;
                    }
                    catch (SocketException)
                    {
                        status = WirelessAdbStatus.NotListening;
                    }
                    catch (Exception)
                    {
                        status = WirelessAdbStatus.Unknown;
                    }
                }
            }

            if (generation != _probeGeneration || !IsVisible) yield break;
            _probeRoutine = null;
            SetProbeState(status, BuildConnectCommand(ip, DefaultAdbPort));
            _helpText.text = status == WirelessAdbStatus.NotListening
                ? "One-time setup over USB:\nadb devices\nadb tcpip 5555\nThen connect with the command above."
                : "Use the command above from a computer on the same Wi-Fi network.\nADB authentication is managed by Android.";
        }

        private void CancelProbe()
        {
            ++_probeGeneration;
            if (_probeRoutine != null)
            {
                StopCoroutine(_probeRoutine);
                _probeRoutine = null;
            }
        }

        private void OnDestroy() => CancelProbe();

        public static string StatusLabel(WirelessAdbStatus status)
        {
            switch (status)
            {
                case WirelessAdbStatus.Listening: return "Listening";
                case WirelessAdbStatus.NotListening: return "Not listening";
                default: return "Unknown";
            }
        }

        public static bool TryOpenSettings(Func<string, bool> openAction)
        {
            if (openAction == null) return false;
            foreach (var action in WirelessDebuggingSettingsActions)
            {
                try
                {
                    if (openAction(action)) return true;
                }
                catch (Exception)
                {
                    // Try the compatibility action, then let the page show in-app help.
                }
            }
            return false;
        }

        private static string GetCurrentIpv4()
        {
            var addresses = new List<string>();
            try
            {
                foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var address in network.GetIPProperties().UnicastAddresses)
                        addresses.Add(address.Address.ToString());
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
            return SelectIpv4(addresses);
        }

        private static bool IsPrivateIpv4(string value)
        {
            if (!IPAddress.TryParse(value, out var address)) return false;
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168);
        }

        private void Build()
        {
            _page = new GameObject("DeveloperToolsPanel");
            _page.transform.SetParent(_canvas.transform, false);
            var image = _page.AddComponent<Image>();
            image.color = new Color(0.08f, 0.1f, 0.15f, 0.98f);
            var pageRect = _page.GetComponent<RectTransform>();
            pageRect.anchorMin = new Vector2(0.1f, 0.1f);
            pageRect.anchorMax = new Vector2(0.9f, 0.9f);
            pageRect.sizeDelta = Vector2.zero;

            CreateText(_page.transform, "Developer Tools", 30, TextAnchor.MiddleCenter, 0.25f, 0.86f, 0.95f, 0.96f, out _);
            CreateText(_page.transform, "", 24, TextAnchor.MiddleLeft, 0.08f, 0.70f, 0.92f, 0.78f, out _ipText);
            CreateText(_page.transform, "", 24, TextAnchor.MiddleLeft, 0.08f, 0.60f, 0.92f, 0.68f, out _portText);
            CreateText(_page.transform, "", 24, TextAnchor.MiddleLeft, 0.08f, 0.50f, 0.92f, 0.58f, out _statusText);
            CreateText(_page.transform, "Command:", 18, TextAnchor.MiddleLeft, 0.08f, 0.40f, 0.92f, 0.47f, out _);
            CreateText(_page.transform, "", 20, TextAnchor.MiddleLeft, 0.08f, 0.32f, 0.92f, 0.40f, out _commandText);
            CreateText(_page.transform, "", 17, TextAnchor.UpperLeft, 0.08f, 0.17f, 0.92f, 0.31f, out _helpText);

            CreateButton(_page.transform, "Open Wireless Debugging", 0.08f, 0.06f, 0.38f, 0.14f, OpenWirelessDebugging);
            CreateButton(_page.transform, "Copy Command", 0.41f, 0.06f, 0.62f, 0.14f, CopyCommand);
            CreateButton(_page.transform, "Refresh", 0.65f, 0.06f, 0.78f, 0.14f, Refresh);
            CreateButton(_page.transform, "Back", 0.81f, 0.06f, 0.92f, 0.14f, Back);
            _page.SetActive(false);
        }

        private void SetProbeState(WirelessAdbStatus status, string command)
        {
            _statusText.text = "Status: " + StatusLabel(status);
            _commandText.text = string.IsNullOrEmpty(command) ? "Unavailable" : command;
        }

        private void CopyCommand()
        {
            if (!string.IsNullOrEmpty(_commandText.text) && _commandText.text != "Unavailable")
                GUIUtility.systemCopyBuffer = _commandText.text;
        }

        private void OpenWirelessDebugging()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (TryOpenSettings(StartSettingsActivity))
            {
                _helpText.text = "Finish enabling Wireless debugging in Android settings, then return and tap Refresh.";
                return;
            }
#endif
            _helpText.text = "Wireless debugging settings are unavailable. Open Quest Settings > Developer Options manually.";
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool StartSettingsActivity(string action)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent", action))
            {
                activity.Call("startActivity", intent);
                return true;
            }
        }
#endif

        private void Back()
        {
            Hide();
            _onBack?.Invoke();
        }

        private static void CreateText(Transform parent, string value, int fontSize, TextAnchor alignment,
            float minX, float minY, float maxX, float maxY, out Text text)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.sizeDelta = Vector2.zero;
        }

        private static void CreateButton(Transform parent, string label, float minX, float minY, float maxX, float maxY, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.35f, 0.55f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.sizeDelta = Vector2.zero;
            CreateText(go.transform, label, 16, TextAnchor.MiddleCenter, 0, 0, 1, 1, out _);
        }
    }
}
