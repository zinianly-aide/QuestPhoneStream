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
        public string serviceType { get; internal set; }
        public string capabilities { get; internal set; }
        public string streamId { get; internal set; }
        public string signalingUrl { get; internal set; }
        public bool IsReady { get; internal set; }

        public string BaseUrl => MediaDeviceDiscovery.BuildBaseUrl(host, port);

        public bool HasCapability(string capability)
        {
            if (string.IsNullOrWhiteSpace(capability) || string.IsNullOrWhiteSpace(capabilities)) return false;
            foreach (var value in capabilities.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (string.Equals(value.Trim(), capability.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>
    /// Discovers Android devices advertising capability and endpoint metadata.
    /// The bridge is intentionally limited to NSD; it does not pair or change
    /// the existing catalog, token or playback protocols.
    /// </summary>
    public sealed class MediaDeviceDiscovery : MonoBehaviour
    {
        public const string UnifiedServiceType = "_qps-device._tcp.";
        public const string LegacyServiceType = "_qps-media._tcp.";
        public const string ServiceType = LegacyServiceType;
        public static readonly string[] DiscoveryServiceTypes = { UnifiedServiceType, LegacyServiceType };

        private readonly Dictionary<string, MediaDeviceInfo> _devices = new Dictionary<string, MediaDeviceInfo>();
        private readonly HashSet<string> _activeServiceKeys = new HashSet<string>();
        private readonly Dictionary<string, string> _serviceToDevice = new Dictionary<string, string>();
        private readonly Dictionary<string, HashSet<string>> _servicesByDevice = new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, string> _serviceCapabilities = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _serviceStreamIds = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _serviceSignalingUrls = new Dictionary<string, string>();
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
            Debug.Log($"[MediaDeviceDiscovery] StartDiscovery called, type={ServiceType}");
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
            Debug.Log($"[MediaDeviceDiscovery] StopDiscovery called, wasDiscovering={IsDiscovering}");
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

        public static bool ShouldAcceptResolvedCallback(bool currentGeneration, bool serviceIsActive) =>
            currentGeneration && serviceIsActive;

#if UNITY_ANDROID && !UNITY_EDITOR
        private bool IsCurrent(AndroidNsdBridge bridge, int generation) =>
            IsDiscovering && generation == _discoveryGeneration && ReferenceEquals(_bridge, bridge);

        private void OnServiceFound(AndroidNsdBridge bridge, int generation, AndroidJavaObject serviceInfo)
        {
            if (!IsCurrent(bridge, generation)) return;
            var serviceKey = GetServiceKey(serviceInfo);
            if (string.IsNullOrWhiteSpace(serviceKey)) return;
            if (!_activeServiceKeys.Add(serviceKey))
            {
                Debug.Log($"[MediaDeviceDiscovery] Duplicate service ignored key={serviceKey}");
                return;
            }
            Debug.Log($"[MediaDeviceDiscovery] Service found key={serviceKey}");
            if (!bridge.Resolve(serviceInfo, serviceKey))
            {
                _activeServiceKeys.Remove(serviceKey);
                Debug.LogWarning($"[MediaDeviceDiscovery] Resolve dispatch failed; service is retryable key={serviceKey}");
                DevicesChanged?.Invoke();
            }
        }

        private void OnServiceResolved(AndroidNsdBridge bridge, int generation, string serviceKey, AndroidJavaObject serviceInfo)
        {
            bridge.RemoveResolveListener(serviceKey);
            var currentGeneration = IsCurrent(bridge, generation);
            var serviceIsActive = _activeServiceKeys.Contains(serviceKey);
            if (!ShouldAcceptResolvedCallback(currentGeneration, serviceIsActive))
            {
                Debug.Log($"[MediaDeviceDiscovery] {(currentGeneration ? "Late resolve" : "Stale callback")} ignored key={serviceKey}");
                return;
            }
            var attributes = ReadAttributes(serviceInfo);
            var version = GetAttribute(attributes, "v");
            var deviceId = GetAttribute(attributes, "id");
            var capabilities = GetAttribute(attributes, "caps");
            var streamId = GetAttribute(attributes, "streamId");
            var signalingUrl = GetAttribute(attributes, "signalingUrl");
            Debug.Log($"[MediaDeviceDiscovery] Service resolved key={serviceKey} v={version} id={deviceId} caps={capabilities} attrCount={attributes.Count}");
            if (version != "1" || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(capabilities))
            {
                Debug.LogWarning($"[MediaDeviceDiscovery] Ignoring invalid service key={serviceKey} v={version} id={deviceId} caps={capabilities}");
                return;
            }

            var serviceName = CallString(serviceInfo, "getServiceName");
            var serviceType = GetServiceType(serviceInfo);
            var host = GetHostAddress(serviceInfo);
            var port = serviceInfo.Call<int>("getPort");
            if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(host) || port <= 0) return;

            if (_serviceToDevice.TryGetValue(serviceKey, out var previousId) && previousId != deviceId &&
                _servicesByDevice.TryGetValue(previousId, out var previousServices))
            {
                previousServices.Remove(serviceKey);
                _serviceCapabilities.Remove(serviceKey);
                _serviceStreamIds.Remove(serviceKey);
                _serviceSignalingUrls.Remove(serviceKey);
            }

            _serviceToDevice[serviceKey] = deviceId;
            _serviceCapabilities[serviceKey] = capabilities;
            _serviceStreamIds[serviceKey] = streamId;
            _serviceSignalingUrls[serviceKey] = signalingUrl;
            if (!_servicesByDevice.TryGetValue(deviceId, out var services))
                _servicesByDevice[deviceId] = services = new HashSet<string>();
            services.Add(serviceKey);

            if (!_devices.TryGetValue(deviceId, out var device))
                _devices[deviceId] = device = new MediaDeviceInfo { deviceId = deviceId };
            device.name = GetAttribute(attributes, "name");
            if (string.IsNullOrWhiteSpace(device.name)) device.name = serviceName;
            device.host = host;
            device.port = port;
            device.serviceType = PreferServiceType(device.serviceType, serviceType);
            device.capabilities = MergeCapabilitiesForDevice(deviceId);
            device.streamId = GetPreferredServiceValue(deviceId, _serviceStreamIds);
            device.signalingUrl = GetPreferredServiceValue(deviceId, _serviceSignalingUrls);
            device.IsReady = true;
            Debug.Log($"[MediaDeviceDiscovery] Device READY name={device.name} id={deviceId} host={host} port={port} baseUrl={device.BaseUrl}");
            DevicesChanged?.Invoke();
        }

        private void OnServiceLost(AndroidNsdBridge bridge, int generation, AndroidJavaObject serviceInfo)
        {
            if (!IsCurrent(bridge, generation))
            {
                Debug.Log("[MediaDeviceDiscovery] Stale callback ignored: service lost");
                return;
            }
            var serviceKey = GetServiceKey(serviceInfo);
            if (string.IsNullOrWhiteSpace(serviceKey)) return;
            bridge.RemoveResolveListener(serviceKey);
            if (!_activeServiceKeys.Remove(serviceKey)) return;
            if (!_serviceToDevice.TryGetValue(serviceKey, out var deviceId))
            {
                Debug.Log($"[MediaDeviceDiscovery] Service lost before resolve key={serviceKey}");
                return;
            }
            _serviceToDevice.Remove(serviceKey);
            _serviceCapabilities.Remove(serviceKey);
            _serviceStreamIds.Remove(serviceKey);
            _serviceSignalingUrls.Remove(serviceKey);
            if (_servicesByDevice.TryGetValue(deviceId, out var services))
            {
                services.Remove(serviceKey);
                Debug.Log($"[MediaDeviceDiscovery] Service lost key={serviceKey} deviceId={deviceId} remainingServices={services.Count}");
                if (services.Count != 0)
                {
                    if (_devices.TryGetValue(deviceId, out var remainingDevice))
                    {
                        remainingDevice.capabilities = MergeCapabilitiesForDevice(deviceId);
                        remainingDevice.serviceType = PreferredServiceTypeForDevice(deviceId);
                        remainingDevice.streamId = GetPreferredServiceValue(deviceId, _serviceStreamIds);
                        remainingDevice.signalingUrl = GetPreferredServiceValue(deviceId, _serviceSignalingUrls);
                        Debug.Log($"[MediaDeviceDiscovery] Device metadata updated after service loss id={deviceId} serviceType={remainingDevice.serviceType} caps={remainingDevice.capabilities}");
                    }
                    DevicesChanged?.Invoke();
                    return;
                }
                _servicesByDevice.Remove(deviceId);
            }
            if (_devices.TryGetValue(deviceId, out var device))
            {
                device.IsReady = false;
                Debug.Log($"[MediaDeviceDiscovery] Device LOST id={deviceId} device.IsReady={device.IsReady}");
                DevicesChanged?.Invoke();
            }
        }

        private void OnDiscoveryFailed(AndroidNsdBridge bridge, int generation, string serviceType, string message)
        {
            if (!IsCurrent(bridge, generation)) return;
            bridge.MarkDiscoveryFailed(serviceType);
            Debug.LogWarning($"[MediaDeviceDiscovery] {message} type={serviceType}");
            if (bridge.HasActiveDiscovery) return;
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
            if (!IsCurrent(bridge, generation))
            {
                Debug.Log($"[MediaDeviceDiscovery] Stale callback ignored: resolve failed key={serviceKey}");
                return;
            }
            _activeServiceKeys.Remove(serviceKey);
            _serviceCapabilities.Remove(serviceKey);
            _serviceStreamIds.Remove(serviceKey);
            _serviceSignalingUrls.Remove(serviceKey);
            Debug.LogWarning($"[MediaDeviceDiscovery] NSD resolve failed service={serviceKey} error={errorCode}");
            DevicesChanged?.Invoke();
        }

#endif
        private bool ClearDiscoveredServices()
        {
            var changed = _activeServiceKeys.Count != 0 || _serviceToDevice.Count != 0;
            _activeServiceKeys.Clear();
            _serviceToDevice.Clear();
            _servicesByDevice.Clear();
            _serviceCapabilities.Clear();
            _serviceStreamIds.Clear();
            _serviceSignalingUrls.Clear();
            foreach (var device in _devices.Values)
            {
                if (device.IsReady) changed = true;
                device.IsReady = false;
                device.capabilities = string.Empty;
                device.serviceType = string.Empty;
                device.streamId = string.Empty;
                device.signalingUrl = string.Empty;
            }
            return changed;
        }
#if UNITY_ANDROID && !UNITY_EDITOR

        private static string GetServiceType(AndroidJavaObject serviceInfo)
        {
            var type = CallString(serviceInfo, "getServiceType");
            return string.IsNullOrWhiteSpace(type) ? LegacyServiceType : type;
        }

        private static string GetServiceKey(AndroidJavaObject serviceInfo) =>
            GetServiceType(serviceInfo) + "|" + CallString(serviceInfo, "getServiceName");

        private static string PreferServiceType(string current, string candidate) =>
            string.IsNullOrWhiteSpace(current) || candidate == UnifiedServiceType ? candidate : current;

        private string PreferredServiceTypeForDevice(string deviceId)
        {
            if (!_servicesByDevice.TryGetValue(deviceId, out var services)) return string.Empty;
            foreach (var serviceKey in services)
                if (serviceKey.StartsWith(UnifiedServiceType + "|", StringComparison.Ordinal)) return UnifiedServiceType;
            return LegacyServiceType;
        }

        private string MergeCapabilitiesForDevice(string deviceId)
        {
            if (!_servicesByDevice.TryGetValue(deviceId, out var services)) return string.Empty;
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var serviceKey in services)
            {
                if (!_serviceCapabilities.TryGetValue(serviceKey, out var value)) continue;
                foreach (var capability in value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    values.Add(capability.Trim());
            }
            return string.Join(",", values);
        }

        private string GetPreferredServiceValue(string deviceId, Dictionary<string, string> values)
        {
            if (!_servicesByDevice.TryGetValue(deviceId, out var services)) return string.Empty;
            var fallback = string.Empty;
            foreach (var serviceKey in services)
            {
                if (!values.TryGetValue(serviceKey, out var value) || string.IsNullOrWhiteSpace(value)) continue;
                if (serviceKey.StartsWith(UnifiedServiceType + "|", StringComparison.Ordinal)) return value;
                if (string.IsNullOrWhiteSpace(fallback)) fallback = value;
            }
            return fallback;
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
                        var sbyteValue = entry?.Call<sbyte[]>("getValue");
                        if (!string.IsNullOrEmpty(key) && sbyteValue != null)
                        {
                            var byteValue = new byte[sbyteValue.Length];
                            Buffer.BlockCopy(sbyteValue, 0, byteValue, 0, sbyteValue.Length);
                            result[key] = Encoding.UTF8.GetString(byteValue);
                        }
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
            private readonly Dictionary<string, DiscoveryListenerProxy> _discoveryListeners = new Dictionary<string, DiscoveryListenerProxy>();
            private readonly Dictionary<string, ResolveListenerProxy> _resolveListeners = new Dictionary<string, ResolveListenerProxy>();
            private readonly HashSet<string> _startedDiscoveryTypes = new HashSet<string>();

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
                Debug.Log($"[MediaDeviceDiscovery] NsdManager obtained, discovering {DiscoveryServiceTypes.Length} service types");
                foreach (var serviceType in DiscoveryServiceTypes)
                {
                    var listener = new DiscoveryListenerProxy(_owner, this, _generation, serviceType);
                    _discoveryListeners[serviceType] = listener;
                    try
                    {
                        _startedDiscoveryTypes.Add(serviceType);
                        _manager.Call("discoverServices", serviceType, 1, listener);
                    }
                    catch (Exception error)
                    {
                        _startedDiscoveryTypes.Remove(serviceType);
                        _discoveryListeners.Remove(serviceType);
                        Debug.LogWarning($"[MediaDeviceDiscovery] NSD discovery dispatch failed type={serviceType}: {error.Message}");
                    }
                }
                if (_startedDiscoveryTypes.Count == 0)
                    throw new InvalidOperationException("NSD discovery could not start for any service type");
            }

            public bool Resolve(AndroidJavaObject serviceInfo, string serviceKey)
            {
                if (_manager == null || string.IsNullOrWhiteSpace(serviceKey) || _resolveListeners.ContainsKey(serviceKey)) return false;
                var listener = new ResolveListenerProxy(_owner, this, serviceKey);
                _resolveListeners[serviceKey] = listener;
                try
                {
                    _manager?.Call("resolveService", serviceInfo, listener);
                    return true;
                }
                catch (Exception error)
                {
                    _resolveListeners.Remove(serviceKey);
                    Debug.LogWarning($"[MediaDeviceDiscovery] NSD resolve dispatch failed service={serviceKey}: {error.Message}");
                    return false;
                }
            }

            public void RemoveResolveListener(string serviceKey) => _resolveListeners.Remove(serviceKey);

            public void MarkDiscoveryStarted(string serviceType) => _startedDiscoveryTypes.Add(serviceType);
            public void MarkDiscoveryStopped(string serviceType) => _startedDiscoveryTypes.Remove(serviceType);
            public void MarkDiscoveryFailed(string serviceType) => _startedDiscoveryTypes.Remove(serviceType);
            public bool HasActiveDiscovery => _startedDiscoveryTypes.Count != 0;

            public void Dispose()
            {
                try
                {
                    if (_manager != null)
                        foreach (var listener in _discoveryListeners.Values)
                            try { _manager.Call("stopServiceDiscovery", listener); }
                            catch (Exception error) { Debug.LogWarning("[MediaDeviceDiscovery] NSD stop failed: " + error.Message); }
                }
                finally
                {
                    _resolveListeners.Clear();
                    _discoveryListeners.Clear();
                    _startedDiscoveryTypes.Clear();
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
                private readonly string _serviceType;
                public DiscoveryListenerProxy(MediaDeviceDiscovery owner, AndroidNsdBridge bridge, int generation, string serviceType) : base("android.net.nsd.NsdManager$DiscoveryListener")
                {
                    _owner = owner;
                    _bridge = bridge;
                    _generation = generation;
                    _serviceType = serviceType;
                }
                public void onDiscoveryStarted(string serviceType)
                {
                    _bridge.MarkDiscoveryStarted(_serviceType);
                    Debug.Log($"[MediaDeviceDiscovery] NSD discovery started type={serviceType}");
                }
                public void onServiceFound(AndroidJavaObject serviceInfo)
                {
                    var name = MediaDeviceDiscovery.CallString(serviceInfo, "getServiceName");
                    Debug.Log($"[MediaDeviceDiscovery] NSD service found name={name}");
                    UnityMainThread.Enqueue(() => _owner.OnServiceFound(_bridge, _generation, serviceInfo));
                }
                public void onServiceLost(AndroidJavaObject serviceInfo) =>
                    UnityMainThread.Enqueue(() => _owner.OnServiceLost(_bridge, _generation, serviceInfo));
                public void onDiscoveryStopped(string serviceType)
                {
                    _bridge.MarkDiscoveryStopped(_serviceType);
                    Debug.Log($"[MediaDeviceDiscovery] NSD discovery stopped type={serviceType}");
                }
                public void onStartDiscoveryFailed(string serviceType, int errorCode)
                {
                    _bridge.MarkDiscoveryFailed(_serviceType);
                    Debug.LogError($"[MediaDeviceDiscovery] NSD start discovery failed type={serviceType} error={errorCode}");
                    UnityMainThread.Enqueue(() => _owner.OnDiscoveryFailed(_bridge, _generation, serviceType, $"NSD discovery start failed error={errorCode}"));
                }
                public void onStopDiscoveryFailed(string serviceType, int errorCode) =>
                    UnityMainThread.Enqueue(() => _owner.OnDiscoveryFailed(_bridge, _generation, serviceType, $"NSD discovery stop failed error={errorCode}"));
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
