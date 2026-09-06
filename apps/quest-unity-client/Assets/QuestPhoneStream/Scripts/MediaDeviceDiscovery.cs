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
        private readonly HashSet<string> _activeServiceKeys = new HashSet<string>();
        private readonly Dictionary<string, string> _serviceToDevice = new Dictionary<string, string>();
        private readonly Dictionary<string, HashSet<string>> _servicesByDevice = new Dictionary<string, HashSet<string>>();
        private int _discoveryGeneration;
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
#if UNITY_ANDROID && !UNITY_EDITOR
            var generation = ++_discoveryGeneration;
            var previousBridge = _bridge;
            _bridge = null;
            previousBridge?.Dispose();
            IsDiscovering = true;
            AndroidNsdBridge bridge = null;
            try
            {
                bridge = new AndroidNsdBridge(this, generation);
                _bridge = bridge;
                bridge.Start();
            }
            catch (Exception error)
            {
                IsDiscovering = false;
                ++_discoveryGeneration;
                if (ReferenceEquals(_bridge, bridge)) _bridge = null;
                bridge?.Dispose();
                ClearDiscoveredServices();
                Debug.LogError($"[MediaDeviceDiscovery] NSD bridge start failed: {error.Message}");
                DevicesChanged?.Invoke();
            }
#else
            ++_discoveryGeneration;
            IsDiscovering = true;
            Debug.Log("[MediaDeviceDiscovery] NSD is available only on the Android Quest build; manual media URL remains available.");
#endif
        }

        public void StopDiscovery()
        {
            var wasDiscovering = IsDiscovering;
            IsDiscovering = false;
            ++_discoveryGeneration;
#if UNITY_ANDROID && !UNITY_EDITOR
            var bridge = _bridge;
            _bridge = null;
            bridge?.Dispose();
#endif
            var changed = ClearDiscoveredServices();
            if (wasDiscovering || changed) DevicesChanged?.Invoke();
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
        private bool IsCurrent(AndroidNsdBridge bridge, int generation) =>
            IsDiscovering && generation == _discoveryGeneration && ReferenceEquals(_bridge, bridge);

        private void OnServiceFound(AndroidNsdBridge bridge, int generation, AndroidJavaObject serviceInfo)
        {
            if (!IsCurrent(bridge, generation)) return;
            var serviceKey = CallString(serviceInfo, "getServiceName");
            if (string.IsNullOrWhiteSpace(serviceKey)) return;
            _activeServiceKeys.Add(serviceKey);
            bridge.Resolve(serviceInfo);
        }

        private void OnServiceResolved(AndroidNsdBridge bridge, int generation, string serviceKey, AndroidJavaObject serviceInfo)
        {
            bridge.RemoveResolveListener(serviceKey);
            if (!IsCurrent(bridge, generation) || !_activeServiceKeys.Contains(serviceKey)) return;
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

        private void OnServiceLost(AndroidNsdBridge bridge, int generation, AndroidJavaObject serviceInfo)
        {
            if (!IsCurrent(bridge, generation)) return;
            var serviceKey = CallString(serviceInfo, "getServiceName");
            if (string.IsNullOrWhiteSpace(serviceKey) || !_activeServiceKeys.Remove(serviceKey)) return;
            if (!_serviceToDevice.TryGetValue(serviceKey, out var deviceId)) return;
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

        private void OnDiscoveryFailed(AndroidNsdBridge bridge, int generation, string message)
        {
            if (!IsCurrent(bridge, generation)) return;
            IsDiscovering = false;
            ++_discoveryGeneration;
            _bridge = null;
            bridge.Dispose();
            ClearDiscoveredServices();
            Debug.LogError("[MediaDeviceDiscovery] " + message);
            DevicesChanged?.Invoke();
        }

        private void OnServiceResolveFailed(AndroidNsdBridge bridge, int generation, string serviceKey, int errorCode)
        {
            bridge.RemoveResolveListener(serviceKey);
            if (!IsCurrent(bridge, generation)) return;
            Debug.LogWarning($"[MediaDeviceDiscovery] NSD resolve failed service={serviceKey} error={errorCode}");
        }

        private bool ClearDiscoveredServices()
        {
            var changed = _activeServiceKeys.Count != 0 || _serviceToDevice.Count != 0;
            _activeServiceKeys.Clear();
            _serviceToDevice.Clear();
            _servicesByDevice.Clear();
            foreach (var device in _devices.Values)
            {
                if (device.IsReady) changed = true;
                device.IsReady = false;
            }
            return changed;
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
            private readonly int _generation;
            private AndroidJavaObject _manager;
            private AndroidJavaObject _multicastLock;
            private bool _multicastLockHeld;
            private DiscoveryListenerProxy _discoveryListener;
            private readonly Dictionary<string, ResolveListenerProxy> _resolveListeners = new Dictionary<string, ResolveListenerProxy>();

            public AndroidNsdBridge(MediaDeviceDiscovery owner, int generation)
            {
                _owner = owner;
                _generation = generation;
            }

            public void Start()
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    TryAcquireMulticastLock(activity);
                    _manager = activity.Call<AndroidJavaObject>("getSystemService", "servicediscovery");
                }
                if (_manager == null) throw new InvalidOperationException("NsdManager is unavailable");
                _discoveryListener = new DiscoveryListenerProxy(_owner, this, _generation);
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
                finally
                {
                    _resolveListeners.Clear();
                    _discoveryListener = null;
                    try { _manager?.Dispose(); }
                    catch (Exception error) { Debug.LogWarning("[MediaDeviceDiscovery] NSD manager dispose failed: " + error.Message); }
                    _manager = null;
                    ReleaseMulticastLock();
                }
            }

            private void TryAcquireMulticastLock(AndroidJavaObject activity)
            {
                if (activity == null) return;
                try
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 31) return;
                    }
                    using (var wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                    {
                        if (wifiManager == null) return;
                        _multicastLock = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "QuestPhoneStreamNSD");
                    }
                    if (_multicastLock == null) return;
                    _multicastLock.Call("setReferenceCounted", false);
                    _multicastLock.Call("acquire");
                    _multicastLockHeld = true;
                }
                catch (Exception error)
                {
                    Debug.LogWarning("[MediaDeviceDiscovery] MulticastLock acquire failed: " + error.Message);
                    ReleaseMulticastLock();
                }
            }

            private void ReleaseMulticastLock()
            {
                if (_multicastLock == null) return;
                try
                {
                    if (_multicastLockHeld) _multicastLock.Call("release");
                }
                catch (Exception error) { Debug.LogWarning("[MediaDeviceDiscovery] MulticastLock release failed: " + error.Message); }
                finally
                {
                    _multicastLock.Dispose();
                    _multicastLock = null;
                    _multicastLockHeld = false;
                }
            }

            private sealed class DiscoveryListenerProxy : AndroidJavaProxy
            {
                private readonly MediaDeviceDiscovery _owner;
                private readonly AndroidNsdBridge _bridge;
                private readonly int _generation;
                public DiscoveryListenerProxy(MediaDeviceDiscovery owner, AndroidNsdBridge bridge, int generation) : base("android.net.nsd.NsdManager$DiscoveryListener")
                {
                    _owner = owner;
                    _bridge = bridge;
                    _generation = generation;
                }
                public void onDiscoveryStarted(string serviceType) { }
                public void onServiceFound(AndroidJavaObject serviceInfo) =>
                    UnityMainThread.Enqueue(() => _owner.OnServiceFound(_bridge, _generation, serviceInfo));
                public void onServiceLost(AndroidJavaObject serviceInfo) =>
                    UnityMainThread.Enqueue(() => _owner.OnServiceLost(_bridge, _generation, serviceInfo));
                public void onDiscoveryStopped(string serviceType) { }
                public void onStartDiscoveryFailed(string serviceType, int errorCode) =>
                    UnityMainThread.Enqueue(() => _owner.OnDiscoveryFailed(_bridge, _generation, $"NSD discovery start failed error={errorCode}"));
                public void onStopDiscoveryFailed(string serviceType, int errorCode) =>
                    UnityMainThread.Enqueue(() => _owner.OnDiscoveryFailed(_bridge, _generation, $"NSD discovery stop failed error={errorCode}"));
            }

            private sealed class ResolveListenerProxy : AndroidJavaProxy
            {
                private readonly MediaDeviceDiscovery _owner;
                private readonly AndroidNsdBridge _bridge;
                private readonly string _serviceKey;
                private readonly int _generation;
                public ResolveListenerProxy(MediaDeviceDiscovery owner, AndroidNsdBridge bridge, string serviceKey) : base("android.net.nsd.NsdManager$ResolveListener")
                {
                    _owner = owner; _bridge = bridge; _serviceKey = serviceKey; _generation = bridge._generation;
                }
                public void onServiceResolved(AndroidJavaObject serviceInfo) =>
                    UnityMainThread.Enqueue(() => _owner.OnServiceResolved(_bridge, _generation, _serviceKey, serviceInfo));
                public void onResolveFailed(AndroidJavaObject serviceInfo, int errorCode) =>
                    UnityMainThread.Enqueue(() => _owner.OnServiceResolveFailed(_bridge, _generation, _serviceKey, errorCode));
            }
        }
#endif

        private void OnDestroy() => StopDiscovery();
    }
}
