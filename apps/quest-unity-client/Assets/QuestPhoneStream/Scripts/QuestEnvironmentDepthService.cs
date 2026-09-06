using System;
using System.Reflection;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class EnvironmentDepthSample
    {
        public string v = SpatialWire.Version;
        public string capability = "spatial.environment.depth";
        public string streamId;
        public long sequence;
        public long timestamp;
        public int width;
        public int height;
        public string space = "view";
        public string format = "metadata-only";
        public string ToJson() => JsonUtility.ToJson(this);
    }

    public interface IEnvironmentDepthProvider
    {
        bool IsAvailable { get; }
        bool IsAuthorized { get; }
        bool IsActive { get; }
        Texture DepthTexture { get; }
        string StateText { get; }
        void Refresh();
        bool StartDepth();
        void StopDepth();
    }

    public static class EnvironmentDepthCapabilityGate
    {
        public static bool CanActivate(bool available, bool authorized, bool requested) => available && authorized && requested;
    }

    /// <summary>Optional Quest/Meta depth adapter backed by bounded provider discovery.</summary>
    public sealed class MetaEnvironmentDepthProvider : IEnvironmentDepthProvider
    {
        private const string DiscoveryKey = "spatial.environment.depth.provider";
        private readonly GameObject _owner;
        private Component _component;
        private Type _componentType;
        private PropertyInfo _textureProperty;
        private PropertyInfo _supportedProperty;
        private PropertyInfo _authorizedProperty;
        private PropertyInfo _activeProperty;
        private bool _requested;

        public MetaEnvironmentDepthProvider(GameObject owner)
        {
            _owner = owner;
            Discover(force: false);
        }

        public bool IsAvailable
        {
            get
            {
                if (_component == null || _textureProperty == null) return false;
                if (_supportedProperty == null) return true;
                try { return Convert.ToBoolean(_supportedProperty.GetValue(_component)); } catch { return false; }
            }
        }

        public bool IsAuthorized
        {
            get
            {
                if (!IsAvailable) return false;
                if (_authorizedProperty == null) return true;
                try { return Convert.ToBoolean(_authorizedProperty.GetValue(_component)); } catch { return false; }
            }
        }

        public bool IsActive
        {
            get
            {
                if (!EnvironmentDepthCapabilityGate.CanActivate(IsAvailable, IsAuthorized, _requested)) return false;
                if (_activeProperty != null)
                    try { return Convert.ToBoolean(_activeProperty.GetValue(_component)); } catch { return false; }
                return (_component as Behaviour)?.enabled == true;
            }
        }

        public Texture DepthTexture
        {
            get
            {
                if (!IsActive || _textureProperty == null) return null;
                try { return _textureProperty.GetValue(_component) as Texture; } catch { return null; }
            }
        }

        public string StateText => !IsAvailable ? "Unavailable" : !IsAuthorized ? "Permission required" : IsActive ? "Active" : "Ready";
        public void Refresh() => Discover(force: false);

        public void RefreshExplicit()
        {
            OptionalProviderDiscovery.Refresh(DiscoveryKey);
            ClearBinding();
            Discover(force: true);
        }

        public bool StartDepth()
        {
            _requested = true;
            if (_component == null) Discover(force: true);
            if (!EnvironmentDepthCapabilityGate.CanActivate(IsAvailable, IsAuthorized, true)) return false;
            if (_component is Behaviour behaviour) behaviour.enabled = true;
            return true;
        }

        public void StopDepth()
        {
            _requested = false;
            if (_component is Behaviour behaviour) behaviour.enabled = false;
        }

        private void Discover(bool force)
        {
            if (_component != null && _textureProperty != null) return;
            _componentType = OptionalProviderDiscovery.ResolveType(DiscoveryKey, IsProviderType, force);
            if (_componentType == null) { ClearBinding(); return; }

            var texture = FindTextureProperty(_componentType);
            if (texture == null) { ClearBinding(); return; }
            var component = UnityEngine.Object.FindObjectOfType(_componentType) as Component;
            if (component == null && _owner != null)
            {
                try { component = _owner.GetComponent(_componentType) ?? _owner.AddComponent(_componentType); }
                catch { component = null; }
            }
            if (component == null) { ClearBinding(keepType: true); return; }

            _component = component;
            _textureProperty = texture;
            _supportedProperty = FindBoolProperty(_componentType, "IsSupported", "Supported", "isSupported");
            _authorizedProperty = FindBoolProperty(_componentType, "IsAuthorized", "IsPermissionGranted", "PermissionGranted");
            _activeProperty = FindBoolProperty(_componentType, "IsActive", "IsRunning", "IsPlaying");
        }

        private static bool IsProviderType(Type type) => typeof(Component).IsAssignableFrom(type) &&
            type.Name.IndexOf("EnvironmentDepth", StringComparison.OrdinalIgnoreCase) >= 0 && FindTextureProperty(type) != null;

        private void ClearBinding(bool keepType = false)
        {
            _component = null;
            if (!keepType) _componentType = null;
            _textureProperty = _supportedProperty = _authorizedProperty = _activeProperty = null;
        }

        private static PropertyInfo FindTextureProperty(Type type)
        {
            foreach (var name in new[] { "DepthTexture", "EnvironmentDepthTexture", "Texture", "depthTexture" })
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property != null && typeof(Texture).IsAssignableFrom(property.PropertyType)) return property;
            }
            return null;
        }

        private static PropertyInfo FindBoolProperty(Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.PropertyType == typeof(bool)) return property;
            }
            return null;
        }
    }

    public sealed class QuestEnvironmentDepthService : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestWebRtcReceiver receiver;
        public SpatialDataPlaneHub dataPlane;
        [Range(0.5f, 5f)] public float metadataHz = 1f;
        [Min(1f)] public float providerRefreshSeconds = OptionalProviderDiscovery.DefaultRetrySeconds;

        private IEnvironmentDepthProvider _provider;
        private readonly SpatialSubscriptionBook _subscriptions = new SpatialSubscriptionBook();
        private float _nextProbe;
        private float _nextSample;
        private long _sequence;

        public Texture DepthTexture => _provider?.DepthTexture;
        public string DepthState => _provider?.StateText ?? "Unavailable";
        public bool IsAvailable => _provider != null && _provider.IsAvailable;
        public bool IsAuthorized => _provider != null && _provider.IsAuthorized;
        public bool IsActive => _provider != null && _provider.IsActive;
        public int SubscriberCount => _subscriptions.Count;

        private void Awake()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            _provider = new MetaEnvironmentDepthProvider(gameObject);
            _nextProbe = Time.unscaledTime + Mathf.Max(1f, providerRefreshSeconds);
        }

        private void Start()
        {
            if (dataPlane == null) dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            if (signaling != null)
            {
                signaling.SubscriptionCreateRequested += OnSubscriptionCreate;
                signaling.SubscriptionCancelRequested += OnSubscriptionCancel;
                signaling.NegotiationInvalidated += OnNegotiationInvalidated;
            }
            RefreshCapability();
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (now >= _nextProbe)
            {
                _nextProbe = now + Mathf.Max(1f, providerRefreshSeconds);
                _provider?.Refresh();
                RefreshCapability();
            }
            if (!IsActive || dataPlane == null || !dataPlane.IsFastOpen || _subscriptions.Count == 0 || now < _nextSample) return;
            _nextSample = now + 1f / Mathf.Max(0.5f, metadataHz);
            var texture = DepthTexture;
            if (texture == null) return;
            foreach (var subscription in _subscriptions.Snapshot())
            {
                var packet = new EnvironmentDepthSample
                {
                    streamId = subscription.id,
                    sequence = _sequence++,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    width = texture.width,
                    height = texture.height
                };
                // Metadata only. DepthTexture never enters signaling or the network payload.
                dataPlane.TrySendFastJson(packet.ToJson(), packet.sequence);
            }
        }

        public bool StartDepth()
        {
            var started = _provider != null && _provider.StartDepth();
            RefreshCapability();
            return started;
        }

        public void StopDepth()
        {
            _provider?.StopDepth();
            RefreshCapability();
        }

        public void RefreshProvider()
        {
            if (_provider is MetaEnvironmentDepthProvider meta) meta.RefreshExplicit();
            else _provider?.Refresh();
            RefreshCapability();
        }

        private void OnSubscriptionCreate(SpatialEnvelope request)
        {
            if (request.payload?.capability != "spatial.environment.depth") return;
            if (!IsAvailable || !IsAuthorized)
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "capability_unavailable", "Environment depth provider is unavailable or unauthorized", true);
                return;
            }
            if (!dataPlane.EnsureFastChannel())
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "transport_unavailable", "Realtime Spatial DataChannel is unavailable", true);
                return;
            }
            var subscription = new SpatialTelemetrySubscription
            {
                id = Guid.NewGuid().ToString("N"), capability = "spatial.environment.depth",
                rateHz = Mathf.Clamp(request.payload.rateHz <= 0 ? metadataHz : request.payload.rateHz, 0.5f, 5f),
                nextAt = 0f, nextSequence = 0
            };
            if (!_subscriptions.Add(subscription)) return;
            metadataHz = subscription.rateHz;
            _ = signaling.SendSubscriptionCreatedAsync(request, subscription.id, subscription.rateHz,
                "qps.depth.metadata+json", "webrtc.datachannel", "unreliable_unordered");
            RefreshCapability();
        }

        private void OnSubscriptionCancel(SpatialEnvelope request)
        {
            if (request.payload?.capability != "spatial.environment.depth") return;
            if (_subscriptions.Remove(request.payload.subscriptionId, out _))
                _ = signaling.SendSubscriptionClosedAsync(request, request.payload.subscriptionId);
            RefreshCapability();
        }

        private void RefreshCapability() => signaling?.ReportCapabilityState("spatial.environment.depth",
            available: IsAvailable, authorized: IsAuthorized, active: IsActive);

        private void OnNegotiationInvalidated()
        {
            _subscriptions.Clear();
            RefreshCapability();
        }

        private void OnDestroy()
        {
            _subscriptions.Clear();
            _provider?.StopDepth();
            if (signaling != null)
            {
                signaling.SubscriptionCreateRequested -= OnSubscriptionCreate;
                signaling.SubscriptionCancelRequested -= OnSubscriptionCancel;
                signaling.NegotiationInvalidated -= OnNegotiationInvalidated;
                signaling.ReportCapabilityState("spatial.environment.depth", active: false);
            }
        }
    }

    internal static class QuestEnvironmentDepthBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var service = receiver.GetComponent<QuestEnvironmentDepthService>() ?? receiver.gameObject.AddComponent<QuestEnvironmentDepthService>();
                service.receiver = receiver;
                service.signaling = receiver.signaling;
                service.dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            }
        }
    }
}
