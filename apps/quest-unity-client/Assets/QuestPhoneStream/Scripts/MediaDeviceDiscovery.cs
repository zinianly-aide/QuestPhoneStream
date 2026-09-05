using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class MediaDeviceInfo
    {
        public string deviceId { get; internal set; }
        public string name { get; internal set; }
        public string host { get; internal set; }
        public int port { get; internal set; }
        public bool IsReady { get; internal set; }

        public string BaseUrl => MediaDeviceDiscovery.BuildBaseUrl(host, port);
    }

    /// <summary>
    /// Discovers Android phones advertising the local media HTTP service.
    /// The bridge is intentionally limited to NSD; it does not pair or change
    /// the existing catalog, token or playback protocols.
    /// </summary>
    public sealed class MediaDeviceDiscovery : MonoBehaviour
    {
        public const string ServiceType = "_qps-media._tcp.";

        private readonly Dictionary<string, MediaDeviceInfo> _devices = new Dictionary<string, MediaDeviceInfo>();
        private readonly Dictionary<string, string> _serviceToDevice = new Dictionary<string, string>();
        private readonly Dictionary<string, HashSet<string>> _servicesByDevice = new Dictionary<string, HashSet<string>>();
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidNsdBridge _bridge;
#endif

        public bool IsDiscovering { get; private set; }
        public IEnumerable<MediaDeviceInfo> Devices => _devices.Values;
        public bool HasReadyDevice
        {
            get
            {
                foreach (var device in _devices.Values)
                    if (device.IsReady) return true;
                return false;
            }
        }
        public event Action DevicesChanged;

        public void StartDiscovery()
        {
            if (IsDiscovering) return;
            IsDiscovering = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _bridge = new AndroidNsdBridge(this);
                _bridge.Start();
            }
            catch (Exception error)
            {
                IsDiscovering = false;
                Debug.LogError($"[MediaDeviceDiscovery] NSD bridge start failed: {error.Message}");
            }
#else
            Debug.Log("[MediaDeviceDiscovery] NSD is available only on the Android Quest build; manual media URL remains available.");
#endif
        }

        public void StopDiscovery()
        {
            if (!IsDiscovering) return;
            IsDiscovering = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            _bridge?.Dispose();
            _bridge = null;
#endif
            foreach (var device in _devices.Values)
                device.IsReady = false;
            DevicesChanged?.Invoke();
        }

        public bool TryGetReadyDevice(string deviceId, out MediaDeviceInfo device)
        {
            if (!string.IsNullOrWhiteSpace(deviceId) && _devices.TryGetValue(deviceId, out device) && device.IsReady)
                return true;
            device = null;
            return false;
        }

        public static string BuildBaseUrl(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0) return string.Empty;
            var normalizedHost = host.Trim();
            if (normalizedHost.IndexOf(':') >= 0 && !(normalizedHost.StartsWith("[") && normalizedHost.EndsWith("]")))
                normalizedHost = "[" + normalizedHost.Trim('[', ']') + "]";
            return $"http://{normalizedHost}:{port}";
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnServiceFound(AndroidJavaObject serviceInfo)
        {
            _bridge?.Resolve(serviceInfo);
        }

        private void OnServiceResolved(string serviceKey, AndroidJavaObject serviceInfo)
        {
            var attributes = ReadAttributes(serviceInfo);
            var version = GetAttribute(attributes, "v");
            var deviceId = GetAttribute(attributes, "id");
            var capabilities = GetAttribute(attributes, "caps");
            if (version != "1" || string.IsNullOrWhiteSpace(deviceId) || capabilities != "media")
            {
                Debug.LogWarning($"[MediaDeviceDiscovery] Ignoring invalid service key={serviceKey} v={version} id={deviceId} caps={capabilities}");
                return;
            }

            var serviceName = CallString(serviceInfo, "getServiceName");
            var host = GetHostAddress(serviceInfo);
            var port = serviceInfo.Call<int>("getPort");
            if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(host) || port <= 0) return;

            if (_serviceToDevice.TryGetValue(serviceKey, out var previousId) && previousId != deviceId &&
                _servicesByDevice.TryGetValue(previousId, out var previousServices))
                previousServices.Remove(serviceKey);

            _serviceToDevice[serviceKey] = deviceId;
            if (!_servicesByDevice.TryGetValue(deviceId, out var services))
                _servicesByDevice[deviceId] = services = new HashSet<string>();
            services.Add(serviceKey);

            if (!_devices.TryGetValue(deviceId, out var device))
                _devices[deviceId] = device = new MediaDeviceInfo { deviceId = deviceId };
            device.name = GetAttribute(attributes, "name");
            if (string.IsNullOrWhiteSpace(device.name)) device.name = serviceName;
            device.host = host;
            device.port = port;
            device.IsReady = true;
            DevicesChanged?.Invoke();
        }

        private void OnServiceLost(AndroidJavaObject serviceInfo)
        {
            var serviceKey = CallString(serviceInfo, "getServiceName");
            if (string.IsNullOrWhiteSpace(serviceKey) || !_serviceToDevice.TryGetValue(serviceKey, out var deviceId)) return;
            _serviceToDevice.Remove(serviceKey);
            if (_servicesByDevice.TryGetValue(deviceId, out var services))
            {
                services.Remove(serviceKey);
                if (services.Count != 0) return;
            }
            if (_devices.TryGetValue(deviceId, out var device))
            {
                device.IsReady = false;
                DevicesChanged?.Invoke();
            }
        }

        private void OnDiscoveryFailed(string message)
        {
            IsDiscovering = false;
            Debug.LogError("[MediaDeviceDiscovery] " + message);
            DevicesChanged?.Invoke();
        }

        private static string CallString(AndroidJavaObject value, string method) =>
            value == null ? string.Empty : value.Call<string>(method) ?? string.Empty;

        private static string GetHostAddress(AndroidJavaObject serviceInfo)
        {
            if (serviceInfo == null) return string.Empty;
            using (var host = serviceInfo.Call<AndroidJavaObject>("getHost"))
                return CallString(host, "getHostAddress");
        }

        private static Dictionary<string, string> ReadAttributes(AndroidJavaObject serviceInfo)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (serviceInfo == null) return result;
            using (var attributes = serviceInfo.Call<AndroidJavaObject>("getAttributes"))
            using (var entries = attributes?.Call<AndroidJavaObject>("entrySet"))
            using (var iterator = entries?.Call<AndroidJavaObject>("iterator"))
            {
                while (iterator != null && iterator.Call<bool>("hasNext"))
                {
                    using (var entry = iterator.Call<AndroidJavaObject>("next"))
                    {
                        var key = CallString(entry, "getKey");
                        var value = entry?.Call<byte[]>("getValue");
                        if (!string.IsNullOrEmpty(key) && value != null)
                            result[key] = Encoding.UTF8.GetString(value);
                    }
                }
            }
            return result;
        }

        private static string GetAttribute(Dictionary<string, string> attributes, string key) =>
            attributes.TryGetValue(key, out var value) ? value?.Trim() : string.Empty;

        private sealed class AndroidNsdBridge : IDisposable
        {
            private readonly MediaDeviceDiscovery _owner;
            private AndroidJavaObject _manager;
            private DiscoveryListenerProxy _discoveryListener;
            private readonly Dictionary<string, ResolveListenerProxy> _resolveListeners = new Dictionary<string, ResolveListenerProxy>();

            public AndroidNsdBridge(MediaDeviceDiscovery owner) { _owner = owner; }

            public void Start()
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _manager = activity.Call<AndroidJavaObject>("getSystemService", "servicediscovery");
                if (_manager == null) throw new InvalidOperationException("NsdManager is unavailable");
                _discoveryListener = new DiscoveryListenerProxy(_owner);
                _manager.Call("discoverServices", ServiceType, 1, _discoveryListener);
            }

            public void Resolve(AndroidJavaObject serviceInfo)
            {
                var serviceKey = MediaDeviceDiscovery.CallString(serviceInfo, "getServiceName");
                if (string.IsNullOrWhiteSpace(serviceKey) || _resolveListeners.ContainsKey(serviceKey)) return;
                var listener = new ResolveListenerProxy(_owner, this, serviceKey);
                _resolveListeners[serviceKey] = listener;
                _manager?.Call("resolveService", serviceInfo, listener);
            }

            private void RemoveResolveListener(string serviceKey) => _resolveListeners.Remove(serviceKey);

            public void Dispose()
            {
                try
                {
                    if (_manager != null && _discoveryListener != null)
                        _manager.Call("stopServiceDiscovery", _discoveryListener);
                }
                catch (Exception error) { Debug.LogWarning("[MediaDeviceDiscovery] NSD stop failed: " + error.Message); }
                _resolveListeners.Clear();
                _discoveryListener = null;
                _manager?.Dispose();
                _manager = null;
            }

            private sealed class DiscoveryListenerProxy : AndroidJavaProxy
            {
                private readonly MediaDeviceDiscovery _owner;
                public DiscoveryListenerProxy(MediaDeviceDiscovery owner) : base("android.net.nsd.NsdManager$DiscoveryListener") { _owner = owner; }
                public void onDiscoveryStarted(string serviceType) { }
                public void onServiceFound(AndroidJavaObject serviceInfo) { _owner.OnServiceFound(serviceInfo); }
                public void onServiceLost(AndroidJavaObject serviceInfo) { _owner.OnServiceLost(serviceInfo); }
                public void onDiscoveryStopped(string serviceType) { }
                public void onStartDiscoveryFailed(string serviceType, int errorCode) => _owner.OnDiscoveryFailed($"NSD discovery start failed error={errorCode}");
                public void onStopDiscoveryFailed(string serviceType, int errorCode) => _owner.OnDiscoveryFailed($"NSD discovery stop failed error={errorCode}");
            }

            private sealed class ResolveListenerProxy : AndroidJavaProxy
            {
                private readonly MediaDeviceDiscovery _owner;
                private readonly AndroidNsdBridge _bridge;
                private readonly string _serviceKey;
                public ResolveListenerProxy(MediaDeviceDiscovery owner, AndroidNsdBridge bridge, string serviceKey) : base("android.net.nsd.NsdManager$ResolveListener")
                {
                    _owner = owner; _bridge = bridge; _serviceKey = serviceKey;
                }
                public void onServiceResolved(AndroidJavaObject serviceInfo)
                {
                    _bridge.RemoveResolveListener(_serviceKey);
                    _owner.OnServiceResolved(_serviceKey, serviceInfo);
                }
                public void onResolveFailed(AndroidJavaObject serviceInfo, int errorCode)
                {
                    _bridge.RemoveResolveListener(_serviceKey);
                    Debug.LogWarning($"[MediaDeviceDiscovery] NSD resolve failed service={_serviceKey} error={errorCode}");
                }
            }
        }
#endif

        private void OnDestroy() => StopDiscovery();
    }
}
