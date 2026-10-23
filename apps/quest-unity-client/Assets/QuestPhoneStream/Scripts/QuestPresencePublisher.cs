using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>
    /// Quest's own LAN presence beacon. Separate from Android/Quest OS ADB TLS mDNS:
    /// advertising _qps-quest._tcp. NEVER asserts or opens the ADB debugging port.
    /// Uses the same Android NsdManager mechanism as the historical phone advertiser.
    /// </summary>
    public sealed class QuestPresencePublisher : MonoBehaviour
    {
        public const string ServiceType = "_qps-quest._tcp.";
        public const string Platform = "quest";
        public const string Capabilities = "scene";
        const string IdKey = "QuestPhoneStream_QuestPresenceId_v1";
        public static string StablePresenceId(string persisted) =>
            Guid.TryParse(persisted, out var value) ? value.ToString("N") : Guid.NewGuid().ToString("N");
        public bool IsPublished { get; private set; }
        public string State { get; private set; } = "Inactive";
        public int Port { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _manager;
        RegistrationListener _listener;
        TcpListener _tcp;
        bool _requested;

        sealed class RegistrationListener : AndroidJavaProxy
        {
            readonly QuestPresencePublisher _owner;
            public RegistrationListener(QuestPresencePublisher owner)
                : base("android.net.nsd.NsdManager$RegistrationListener") { _owner = owner; }
            public void onServiceRegistered(AndroidJavaObject service)
            {
                UnityMainThread.Enqueue(() => {
                    if (!_owner || !_owner._requested) return;
                    _owner.IsPublished = true;
                    _owner.State = "Published";
                    Debug.Log("[QuestPresence] registered type=" + ServiceType + " port=" + _owner.Port);
                });
            }
            public void onRegistrationFailed(AndroidJavaObject service, int code)
            {
                UnityMainThread.Enqueue(() => {
                    if (!_owner) return;
                    _owner.State = "Registration failed " + code;
                    Debug.LogWarning("[QuestPresence] NSD registration failed code=" + code);
                });
            }
            public void onServiceUnregistered(AndroidJavaObject service) { }
            public void onUnregistrationFailed(AndroidJavaObject service, int code)
            {
                Debug.LogWarning("[QuestPresence] NSD unregister failed code=" + code);
            }
        }
        void AcceptAndClose(IAsyncResult result)
        {
            try
            {
                var client = _tcp?.EndAcceptTcpClient(result);
                client?.Close(); // Presence only: never accept commands, tokens or ADB traffic.
                if (_tcp != null) _tcp.BeginAcceptTcpClient(AcceptAndClose, null);
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            catch (Exception error) { Debug.LogWarning("[QuestPresence] accept: " + error.Message); }
        }
#endif
        void Start() { StartPublishing(); }
        void OnApplicationPause(bool paused)
        {
            if (paused) StopPublishing(); else StartPublishing();
        }
        void OnDestroy() { StopPublishing(); }

        public void StartPublishing()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_requested) return;
            try
            {
                var id = StablePresenceId(PlayerPrefs.GetString(IdKey, ""));
                PlayerPrefs.SetString(IdKey, id);
                PlayerPrefs.Save();
                _tcp = new TcpListener(IPAddress.Any, 0);
                _tcp.Start(2);
                Port = ((IPEndPoint)_tcp.LocalEndpoint).Port;
                _tcp.BeginAcceptTcpClient(AcceptAndClose, null);
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    _manager = activity.Call<AndroidJavaObject>("getSystemService", "servicediscovery");
                if (_manager == null) throw new InvalidOperationException("NsdManager unavailable");
                var service = new AndroidJavaObject("android.net.nsd.NsdServiceInfo");
                using (service)
                {
                    service.Call("setServiceName", "QuestPhoneStream Quest " + id.Substring(0, 8));
                    service.Call("setServiceType", ServiceType);
                    service.Call("setPort", Port);
                    service.Call("setAttribute", "v", "1");
                    service.Call("setAttribute", "id", id);
                    service.Call("setAttribute", "name", "QuestPhoneStream Quest");
                    service.Call("setAttribute", "platform", Platform);
                    service.Call("setAttribute", "caps", Capabilities);
                    // No signalingUrl or adbPort: Quest is a client, and OS owns ADB.
                    _listener = new RegistrationListener(this);
                    _requested = true;
                    _manager.Call("registerService", service, 1, _listener);
                }
                State = "Registering";
                Debug.Log("[QuestPresence] NSD requested " + ServiceType + " port=" + Port);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[QuestPresence] publish failed: " + error.Message);
                StopPublishing();
                State = "Unavailable: " + error.GetType().Name;
            }
#else
            State = "Android Quest only";
#endif
        }
        public void StopPublishing()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _requested = false;
            if (_manager != null && _listener != null)
                try { _manager.Call("unregisterService", _listener); }
                catch (Exception error) { Debug.LogWarning("[QuestPresence] unregister: " + error.Message); }
            _listener = null;
            try { _manager?.Dispose(); } catch (Exception) { }
            _manager = null;
            try { _tcp?.Stop(); } catch (Exception) { }
            _tcp = null;
#endif
            IsPublished = false;
            Port = 0;
            State = "Stopped";
        }
    }
}
