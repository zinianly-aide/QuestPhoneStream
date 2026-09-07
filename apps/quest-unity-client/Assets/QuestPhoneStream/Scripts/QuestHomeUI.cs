using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream
{
    /// <summary>
    /// Compact end-user home surface. Engineering fields stay behind Settings;
    /// the normal path is device readiness -> Screen / Media / Keyboard.
    /// </summary>
    public sealed class QuestHomeUI : MonoBehaviour
    {
        private Canvas _canvas;
        private QuestSignalingClient _signaling;
        private QuestWebRtcReceiver _receiver;
        private Camera _camera;
        private Text _phoneStatus, _screenStatus, _controlStatus, _mediaStatus;
        private Button _phoneTab, _videosTab, _keyboardButton, _advancedSettingsButton;
        private Text _hint;
        private Transform _mediaDeviceList;
        private Text _mediaDeviceEmptyText;
        private readonly Dictionary<string, Button> _mediaDeviceButtons = new Dictionary<string, Button>();
        private bool _initialized;
        private bool _videosSelected;
        private Coroutine _keyboardRoutine;
        private Coroutine _visionRoutine;
        private string _noticeText;
        private float _noticeUntil;

        public bool IsVisible => _canvas != null && _canvas.gameObject.activeInHierarchy;

        public static Vector3 HomeWorldPosition(Vector3 cameraPosition, Vector3 cameraForward)
        {
            var forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return cameraPosition + forward.normalized * 1.5f + Vector3.down * 0.15f;
        }

        public void Initialize(QuestSignalingClient signaling, Camera xrCamera, QuestWebRtcReceiver receiver)
        {
            if (_initialized) return;
            if (signaling == null || xrCamera == null || receiver == null)
                throw new System.ArgumentException("Quest home UI requires signaling, camera and receiver");
            _signaling = signaling;
            _camera = xrCamera;
            _receiver = receiver;
            Build();
            _signaling.StateChanged += OnStateChanged;
            _signaling.TargetChanged += OnTargetChanged;
            _receiver.ActiveDeviceChanged += OnActiveDeviceChanged;
            if (_receiver.mediaDiscovery != null)
                _receiver.mediaDiscovery.DevicesChanged += RefreshMediaDevices;
            UpdateStatus(_signaling.State);
            RefreshMediaDevices();
            Show();
            _initialized = true;
        }

        public void Show()
        {
            if (_canvas == null || _camera == null) return;
            var forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            _canvas.transform.position = HomeWorldPosition(_camera.transform.position, _camera.transform.forward);
            _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _canvas.gameObject.SetActive(true);
            _receiver?.ProbeMedia();
            RefreshStatus();
        }

        public void Hide() { if (_canvas != null) _canvas.gameObject.SetActive(false); }
        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        private void Build()
        {
            var canvasGo = new GameObject("QuestHomeCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _camera;
            _canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(900, 520);
            canvasRect.localScale = Vector3.one * 0.0015f;

            var panelGo = new GameObject("HomePanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = panelGo.AddComponent<Image>();
            panel.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var title = MakeText(panelGo.transform, "QuestPhoneStream", 28, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, 0.05f, 0.85f, 0.58f, 0.97f);
            var phone = MakeText(panelGo.transform, "Device  ·  Disconnected", 20, TextAnchor.MiddleRight);
            Anchor(phone.rectTransform, 0.58f, 0.85f, 0.95f, 0.97f);
            _phoneStatus = phone.textComponent;

            _screenStatus = AddStatus(panelGo.transform, "Screen", 0.05f, 0.68f);
            _controlStatus = AddStatus(panelGo.transform, "Control", 0.30f, 0.68f);
            _mediaStatus = AddStatus(panelGo.transform, "Media", 0.55f, 0.68f);

            _phoneTab = MakeButton(panelGo.transform, "Screen", 0.05f, 0.49f, 0.27f, 0.61f, OnPhone);
            _videosTab = MakeButton(panelGo.transform, "Media", 0.29f, 0.49f, 0.51f, 0.61f, OnVideos);
            _keyboardButton = MakeButton(panelGo.transform, "Keyboard", 0.53f, 0.49f, 0.75f, 0.61f, OpenKeyboard);
            _advancedSettingsButton = MakeButton(panelGo.transform, "⚙ Settings", 0.77f, 0.49f, 0.95f, 0.61f, OpenSettings);

            // Device-scoped quick actions: pick a discovered device, then Stream (screen push)
            // or Vision (headset camera frame -> AI). These sit directly above the device list.
            MakeButton(panelGo.transform, "▶ 切换推流", 0.05f, 0.42f, 0.52f, 0.48f, OnPhone);
            MakeButton(panelGo.transform, "✥ 抓取视频", 0.52f, 0.42f, 0.95f, 0.48f, OnVision);

            var devicesTitle = MakeText(panelGo.transform, "Devices", 17, TextAnchor.MiddleLeft);
            Anchor(devicesTitle.rectTransform, 0.05f, 0.37f, 0.95f, 0.42f);
            BuildDeviceScroll(panelGo.transform);

            var hint = MakeText(panelGo.transform, "Select a device, then choose Screen, Media, or Keyboard", 16, TextAnchor.MiddleLeft);
            hint.textComponent.color = new Color(0.72f, 0.78f, 0.9f, 1f);
            Anchor(hint.rectTransform, 0.05f, 0.01f, 0.95f, 0.07f);
            _hint = hint.textComponent;
        }

        private void BuildDeviceScroll(Transform parent)
        {
            var scrollGo = new GameObject("DeviceScroll");
            scrollGo.transform.SetParent(parent, false);
            var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
            Anchor(scrollRectTransform, 0.05f, 0.09f, 0.95f, 0.36f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewportGo.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(0.08f, 0.10f, 0.15f, 0.55f);
            scroll.viewport = viewportRect;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = Vector2.zero;
            var deviceLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            deviceLayout.spacing = 5;
            deviceLayout.padding = new RectOffset(6, 6, 6, 6);
            deviceLayout.childForceExpandWidth = true;
            deviceLayout.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRect;
            _mediaDeviceList = contentRect;

            var empty = MakeText(contentGo.transform, "Searching for devices…", 15, TextAnchor.MiddleLeft);
            var emptyLayout = empty.textComponent.gameObject.AddComponent<LayoutElement>();
            emptyLayout.preferredHeight = 36;
            _mediaDeviceEmptyText = empty.textComponent;
        }

        private Text AddStatus(Transform parent, string label, float x, float y)
        {
            var text = MakeText(parent, label + "  ·  —", 17, TextAnchor.MiddleLeft);
            Anchor(text.rectTransform, x, y, x + 0.25f, y + 0.12f);
            return text.textComponent;
        }

        private void OnPhone()
        {
            if (_receiver == null || !_receiver.SupportsScreen)
            {
                SetNotice("Screen is unavailable on this device", 3f);
                return;
            }
            _videosSelected = false;
            _receiver?.SetPhoneScreenMode();
            SetTab(_phoneTab, true);
            SetTab(_videosTab, false);
            SetNotice("Phone screen selected", 2f);
        }

        private void OnVideos()
        {
            if (_receiver == null) return;
            if (!_receiver.SupportsMedia)
            {
                SetNotice("Media is unavailable on this device", 3f);
                return;
            }
            _videosSelected = true;
            if (!_receiver.HasMediaUrl)
            {
                _videosSelected = false;
                if (_receiver.HasReadyMediaDevice)
                {
                    SetNotice("Select a Ready device below", 4f);
                    return;
                }
                SetNotice("Media isn't configured. Open Settings to add a manual source.", 4f);
                _receiver.ToggleSettings();
                Hide();
                return;
            }
            if (!_receiver.IsMediaReady)
            {
                _videosSelected = false;
                SetNotice(_receiver.IsMediaFailed || _receiver.IsMediaStale
                    ? "Media is unreachable. Retrying…"
                    : "Checking media. Try again shortly.", 4f);
                _receiver.ProbeMedia();
                return;
            }
            _receiver.OpenVideoLibrary();
            Hide();
        }

        private void OnVision()
        {
            if (_receiver == null) return;
            _videosSelected = false;
            SetTab(_phoneTab, true);
            SetTab(_videosTab, false);
            var vision = FindObjectOfType<QuestVisionService>();
            var ai = FindObjectOfType<QuestAiClient>();
            if (vision == null || ai == null)
            {
                SetNotice("Vision components unavailable", 3f);
                return;
            }
            if (!ai.CanRequest)
            {
                SetNotice("Configure the AI endpoint in Settings first", 4f);
                _receiver.ToggleSettings();
                return;
            }
            if (_visionRoutine != null) StopCoroutine(_visionRoutine);
            _visionRoutine = StartCoroutine(CaptureAndAnalyze(vision, ai));
        }

        private IEnumerator CaptureAndAnalyze(QuestVisionService vision, QuestAiClient ai)
        {
            if (!vision.IsAvailable)
            {
                SetNotice("Looking for headset camera…", 2f);
                vision.RefreshProvider();
                yield return new WaitForSeconds(1f);
            }
            if (!vision.IsAvailable)
            {
                SetNotice("Headset camera unavailable", 3f);
                _visionRoutine = null;
                yield break;
            }
            if (!vision.IsAuthorized)
            {
                SetNotice("Requesting camera permission…", 2f);
                var granted = false;
                vision.RequestPermission(ok => granted = ok);
                yield return new WaitForSeconds(2f);
                if (!granted)
                {
                    SetNotice("Camera permission denied", 3f);
                    _visionRoutine = null;
                    yield break;
                }
            }
            if (!vision.StartCamera())
            {
                SetNotice("Camera start failed", 3f);
                _visionRoutine = null;
                yield break;
            }
            yield return new WaitForSeconds(0.6f);
            var frame = vision.CaptureSingleFrame();
            if (frame == null)
            {
                SetNotice("No frame captured", 3f);
                _visionRoutine = null;
                yield break;
            }
            SetNotice("Frame captured · analyzing…", 2f);
            ai.AnalyzeLastFrame();
            _visionRoutine = null;
        }

        private void OnMediaDeviceSelected(string deviceId)
        {
            if (_receiver == null || !_receiver.SelectDevice(deviceId)) return;
            SetNotice(_signaling != null && _signaling.IsConnecting
                ? "Device selected. Connecting in background…"
                : "Device selected. Choose Screen, Media, or Control.", 3f);
        }

        private void OpenSettings()
        {
            _receiver?.ToggleSettings();
            Hide();
        }

        private void OpenKeyboard()
        {
            if (_receiver == null || !_receiver.SupportsControl)
            {
                SetNotice("Control is unavailable on this device", 3f);
                return;
            }
            if (_receiver.ActiveDevice.Capabilities.HasSpatialCapabilities &&
                !_receiver.ActiveDevice.Capabilities.IsAuthorized("display.control"))
            {
                SetNotice("Control needs permission on this device", 3f);
                return;
            }
            if (!_receiver.IsControlConnected)
            {
                SetNotice("Control is connecting in background…", 3f);
                return;
            }
            if (_keyboardRoutine != null) StopCoroutine(_keyboardRoutine);
            _keyboardRoutine = StartCoroutine(ReadKeyboard());
        }

        private IEnumerator ReadKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported)
            {
                SetNotice("Native keyboard is unavailable on this build", 4f);
                Debug.LogWarning("[QuestPhoneStream] Native keyboard is unavailable on this platform");
                yield break;
            }
            var keyboard = TouchScreenKeyboard.Open(string.Empty, TouchScreenKeyboardType.Default, false, false, false);
            while (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Visible)
                yield return null;
            if (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Done && !string.IsNullOrEmpty(keyboard.text))
                _receiver?.controlChannel?.SendText(keyboard.text);
            _keyboardRoutine = null;
        }

        private void SetNotice(string message, float seconds)
        {
            _noticeText = message ?? string.Empty;
            _noticeUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
            if (_hint != null) _hint.text = _noticeText;
        }

        private bool HasActiveNotice => !string.IsNullOrEmpty(_noticeText) && Time.unscaledTime < _noticeUntil;

        private void OnStateChanged(ConnectionState state)
        {
            UpdateStatus(state);
            RefreshMediaDevices();
        }

        private void OnTargetChanged()
        {
            // Device selection changes the row label immediately, even when the
            // old and new reconnect attempts share WebSocketConnecting state.
            UpdateStatus(_signaling.State);
            RefreshMediaDevices();
        }

        private void OnActiveDeviceChanged(ActiveDeviceContext _)
        {
            UpdateStatus(_signaling.State);
            RefreshMediaDevices();
        }

        private string DeviceConnectionLabel(MediaDeviceInfo device)
        {
            if (!device.IsReady) return "○ Lost";
            var active = _receiver?.ActiveDevice;
            var isActive = active != null && string.Equals(device.deviceId, active.DeviceId, StringComparison.Ordinal);
            if (!isActive) return "● Ready";
            if (_signaling == null) return "✓ Selected";
            var state = _signaling.State;
            if (state == ConnectionState.Registered) return "✓ Found";
            if ((int)state >= (int)ConnectionState.SessionRequesting && (int)state < (int)ConnectionState.MediaConnected) return "⟳ Connecting";
            if (state == ConnectionState.MediaConnected) return _receiver != null && _receiver.HasVideoFrame ? "● Live" : "✓ Connected";
            if (ConnectionStatus.IsFailure(state)) return "✗ Failed";
            return "⟳ Connecting";
        }

        private void Update()
        {
            if (!_initialized || !IsVisible) return;
            if (!HasActiveNotice && !string.IsNullOrEmpty(_noticeText)) _noticeText = null;
            UpdateStatus(_signaling.State);
        }

        public void RefreshStatus() => UpdateStatus(_signaling.State);

        private void RefreshMediaDevices()
        {
            if (_mediaDeviceList == null || _receiver == null) return;
            var visible = new HashSet<string>();
            if (_receiver.mediaDiscovery != null)
            {
                foreach (var device in _receiver.mediaDiscovery.Devices)
                {
                    if (string.IsNullOrWhiteSpace(device.deviceId)) continue;
                    visible.Add(device.deviceId);
                    if (!_mediaDeviceButtons.TryGetValue(device.deviceId, out var button))
                    {
                        var selectedDeviceId = device.deviceId;
                        button = MakeButton(_mediaDeviceList, device.name, 0, 0, 1, 1, () => OnMediaDeviceSelected(selectedDeviceId));
                        var layout = button.gameObject.AddComponent<LayoutElement>();
                        layout.preferredHeight = 42;
                        layout.minHeight = 42;
                        _mediaDeviceButtons[device.deviceId] = button;
                    }
                    var label = button.GetComponentInChildren<Text>();
                    if (label != null)
                    {
                        var status = DeviceConnectionLabel(device);
                        var capabilities = CapabilityLabel(device);
                        label.text = (string.IsNullOrWhiteSpace(device.name) ? device.deviceId : device.name) +
                            "    " + status + "\n" + capabilities;
                    }
                    button.interactable = device.IsReady;
                    button.gameObject.SetActive(true);
                }
            }
            foreach (var pair in _mediaDeviceButtons)
                if (!visible.Contains(pair.Key)) pair.Value.gameObject.SetActive(false);
            if (_mediaDeviceEmptyText != null)
            {
                _mediaDeviceEmptyText.gameObject.SetActive(visible.Count == 0);
                _mediaDeviceEmptyText.text = _receiver.mediaDiscovery != null && _receiver.mediaDiscovery.IsDiscovering
                    ? "Searching for devices…" : "No devices found. Settings supports a manual URL.";
            }
        }

        private void UpdateStatus(ConnectionState state)
        {
            if (_phoneStatus == null || _receiver == null) return;
            var failed = ConnectionStatus.IsFailure(state);
            var active = _receiver.ActiveDevice;
            var phone = active == null ? "Waiting for device" : active.IsLost ? "Lost" :
                failed ? "Offline" : _receiver.IsPeerConnected ? "Connected" :
                state == ConnectionState.Registered ? "Found" :
                (int)state >= (int)ConnectionState.SessionRequesting ? "Connecting…" : "Searching…";
            _phoneStatus.text = "Device  ·  " + phone + (active == null ? string.Empty : "  " + active.Name);
            _screenStatus.text = "Screen  ·  " + ScreenStatus();
            _controlStatus.text = "Control  ·  " + ControlStatus();
            _mediaStatus.text = "Media  ·  " + (!_receiver.SupportsMedia ? "Unavailable" :
                _receiver.IsMediaReady ? "Ready" : _receiver.IsMediaStale ? "Stale" :
                _receiver.IsMediaChecking ? "Checking…" : "Unreachable");
            if (_phoneTab != null) _phoneTab.interactable = _receiver.SupportsScreen;
            if (_videosTab != null) _videosTab.interactable = _receiver.SupportsMedia;
            if (_keyboardButton != null) _keyboardButton.interactable = _receiver.SupportsControl;
            if (_hint != null && !HasActiveNotice)
                _hint.text = active == null
                    ? "Select a device. This does not open Screen or Media."
                    : active.IsLost
                    ? "Selected device was lost. Choose another device."
                    : "Selected: " + active.Name + ". Choose Screen, Media, or Keyboard.";
            SetTab(_phoneTab, !_videosSelected);
            SetTab(_videosTab, _videosSelected);
        }

        private string ScreenStatus()
        {
            if (!_receiver.SupportsScreen) return "Unavailable";
            return _receiver.HasVideoFrame ? "Ready" : _receiver.IsPeerConnected ? "Waiting" : "Connecting";
        }

        private string ControlStatus()
        {
            if (!_receiver.SupportsControl) return "Unavailable";
            var capabilities = _receiver.ActiveDevice.Capabilities;
            if (capabilities.HasSpatialCapabilities && !capabilities.IsAuthorized("display.control")) return "Needs permission";
            return _receiver.IsControlConnected ? "Ready" : "Connecting";
        }

        private string CapabilityLabel(MediaDeviceInfo device)
        {
            var active = _receiver?.ActiveDevice;
            var capabilities = active != null && string.Equals(active.DeviceId, device.deviceId, StringComparison.Ordinal)
                ? active.Capabilities : null;
            var screen = capabilities != null ? capabilities.Supports("display.publish") : device.HasCapability("screen");
            var media = capabilities != null
                ? capabilities.Supports("media.list") || capabilities.Supports("media.open")
                : device.HasCapability("media");
            var control = capabilities != null ? capabilities.Supports("display.control") : device.HasCapability("control");
            return "Screen " + (screen ? "✓" : "—") + "   Media " + (media ? "✓" : "—") + "   Control " + (control ? "✓" : "—");
        }

        private static void SetTab(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.targetGraphic as Graphic;
            if (image != null) image.color = selected ? new Color(0.16f, 0.55f, 0.34f, 1f) : new Color(0.14f, 0.18f, 0.26f, 1f);
        }

        private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.sizeDelta = Vector2.zero;
        }

        private static Button MakeButton(Transform parent, string label, float minX, float minY, float maxX, float maxY, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.14f, 0.18f, 0.26f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
            var text = MakeText(go.transform, label, 19, TextAnchor.MiddleCenter);
            Anchor(text.rectTransform, 0, 0, 1, 1);
            button.onClick.AddListener(action);
            return button;
        }

        private static UiText MakeText(Transform parent, string value, int size, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return new UiText(text, go.GetComponent<RectTransform>());
        }

        private readonly struct UiText
        {
            public readonly Text textComponent;
            public readonly RectTransform rectTransform;
            public UiText(Text text, RectTransform rect) { textComponent = text; rectTransform = rect; }
            public static implicit operator Text(UiText value) => value.textComponent;
        }

        private void OnDestroy()
        {
            if (_signaling != null) _signaling.StateChanged -= OnStateChanged;
            if (_signaling != null) _signaling.TargetChanged -= OnTargetChanged;
            if (_receiver != null) _receiver.ActiveDeviceChanged -= OnActiveDeviceChanged;
            if (_receiver != null && _receiver.mediaDiscovery != null) _receiver.mediaDiscovery.DevicesChanged -= RefreshMediaDevices;
            if (_keyboardRoutine != null) StopCoroutine(_keyboardRoutine);
        }
    }
}
