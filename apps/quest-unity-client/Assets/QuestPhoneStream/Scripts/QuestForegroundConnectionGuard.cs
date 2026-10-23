using System;
using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>
    /// Opt-in foreground-only screen + Wi-Fi connection stability hints.
    /// Not a service, not an ADB proxy, and never overrides Horizon OS suspend/power policy.
    /// Reverts Unity screen timeout and releases native Wi-Fi lock on pause/disable/destroy.
    /// </summary>
    public sealed class QuestForegroundConnectionGuard : MonoBehaviour
    {
        public const string PreferenceKey = "QuestPhoneStream_KeepAwakeWhileActive_v1";
        public bool Enabled { get; private set; }
        public bool IsActive { get; private set; }
        public string State { get; private set; } = "Off";

        bool _paused;
        int _priorScreenTimeout;
        bool _capturedTimeout;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _wifiLock;
#endif
        void Awake()
        {
            Enabled = PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
            UpdateForegroundLock();
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            UpdateForegroundLock();
        }

        void OnEnable()
        {
            UpdateForegroundLock();
        }
        void OnApplicationPause(bool paused)
        {
            _paused = paused;
            UpdateForegroundLock();
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }

        void UpdateForegroundLock()
        {
            if (!Enabled || _paused || !isActiveAndEnabled)
            {
                Release();
                State = !Enabled ? "Off" : "Paused (released)";
                return;
            }
            if (IsActive) return;
            _priorScreenTimeout = Screen.sleepTimeout;
            _capturedTimeout = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            State = "Screen awake while app active";
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    if (wifi == null) throw new InvalidOperationException("Wi-Fi manager unavailable");
                    // WIFI_MODE_FULL_LOW_LATENCY=4 (Android 10+): foreground/screen-on only.
                    // Unlike legacy HIGH_PERF, modern Android limits this to the visible app.
                    _wifiLock = wifi.Call<AndroidJavaObject>("createWifiLock", 4, "QPS_ActiveOnly");
                    _wifiLock.Call("setReferenceCounted", false);
                    _wifiLock.Call("acquire");
                    State = "Screen + Wi-Fi (foreground)";
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("[QuestForegroundGuard] Wi-Fi lock unavailable: " + error.Message);
                try { _wifiLock?.Dispose(); } catch (Exception) { }
                _wifiLock = null;
                State = "Screen only (Wi-Fi lock unavailable)";
            }
#endif
            IsActive = true;
            Debug.Log("[QuestForegroundGuard] " + State);
        }

        void Release()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_wifiLock != null)
            {
                try
                {
                    if (_wifiLock.Call<bool>("isHeld")) _wifiLock.Call("release");
                }
                catch (Exception error) { Debug.LogWarning("[QuestForegroundGuard] Release: " + error.Message); }
                finally { _wifiLock.Dispose(); _wifiLock = null; }
            }
#endif
            if (_capturedTimeout)
            {
                Screen.sleepTimeout = _priorScreenTimeout;
                _capturedTimeout = false;
            }
            IsActive = false;
        }
    }
}
