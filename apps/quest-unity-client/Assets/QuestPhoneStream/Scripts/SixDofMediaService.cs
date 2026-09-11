using System;
using System.Reflection;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class SixDofMediaBounds
    {
        public float centerX, centerY, centerZ;
        public float sizeX, sizeY, sizeZ;
    }

    [Serializable]
    public sealed class SixDofMediaDescriptor
    {
        public string format;
        public string manifestUrl;
        public string referenceSpace = "local";
        public SixDofMediaBounds bounds;

        public bool IsSixDof => IsSixDofFormat(format);

        public static SixDofMediaDescriptor From(MediaItemDto item) => new SixDofMediaDescriptor
        {
            format = item?.spatialFormat,
            manifestUrl = item?.manifestUrl,
            referenceSpace = string.IsNullOrWhiteSpace(item?.referenceSpace) ? "local" : item.referenceSpace,
            bounds = item?.spatialBounds
        };

        public static bool IsSixDofFormat(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            switch (value.Trim().ToLowerInvariant())
            {
                case "6dof":
                case "volumetric":
                case "mpeg-vv":
                case "v3c": return true;
                default: return false;
            }
        }
    }

    public interface ISixDofMediaProvider
    {
        bool IsAvailable { get; }
        bool IsPlaying { get; }
        string StateText { get; }
        void Refresh();
        bool Open(string url, SixDofMediaDescriptor descriptor);
        void Stop();
    }

    /// <summary>
    /// Optional external volumetric decoder adapter. No decoder is bundled; provider
    /// discovery is bounded/cached and only binds an existing external component.
    /// </summary>
    public sealed class ReflectionSixDofMediaProvider : ISixDofMediaProvider
    {
        private const string DiscoveryKey = "media.6dof.provider";
        private Component _component;
        private Type _componentType;
        private MethodInfo _open, _play, _stop;
        private PropertyInfo _supported, _playing;

        public ReflectionSixDofMediaProvider() { }

        public bool IsAvailable
        {
            get
            {
                if (_component == null || _open == null) return false;
                if (_supported == null) return true;
                try { return Convert.ToBoolean(_supported.GetValue(_component)); } catch { return false; }
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (!IsAvailable) return false;
                if (_playing != null)
                    try { return Convert.ToBoolean(_playing.GetValue(_component)); } catch { return false; }
                return (_component as Behaviour)?.enabled == true;
            }
        }

        public string StateText => !IsAvailable ? "Unavailable · external provider required" : IsPlaying ? "Playing" : "Ready · external provider";
        public void Refresh() => Discover(force: false);

        public void RefreshExplicit()
        {
            OptionalProviderDiscovery.Refresh(DiscoveryKey);
            ClearBinding();
            Discover(force: true);
        }

        public bool Open(string url, SixDofMediaDescriptor descriptor)
        {
            if (_component == null) Discover(force: true);
            if (!IsAvailable || string.IsNullOrWhiteSpace(url) || descriptor == null || !descriptor.IsSixDof) return false;
            try
            {
                if (_component is Behaviour behaviour) behaviour.enabled = true;
                _open.Invoke(_component, new object[] { url });
                _play?.Invoke(_component, null);
                return true;
            }
            catch { return false; }
        }

        public void Stop()
        {
            if (_component == null) return;
            try { _stop?.Invoke(_component, null); } catch { }
            if (_component is Behaviour behaviour) behaviour.enabled = false;
        }

        private void Discover(bool force)
        {
            if (_component != null && _open != null) return;
            _componentType = OptionalProviderDiscovery.ResolveType(DiscoveryKey, IsProviderTypeForDiscovery, force);
            if (_componentType == null) { ClearBinding(); return; }

            var open = FindStringMethod(_componentType, "Open", "Load", "OpenUrl", "LoadUrl");
            if (open == null) { ClearBinding(); return; }

            // External decoder providers must be instantiated by their SDK/scene.
            // Never AddComponent a type discovered only through reflection.
            var component = UnityEngine.Object.FindObjectOfType(_componentType) as Component;
            if (component == null) { ClearBinding(keepType: true); return; }

            _component = component;
            _open = open;
            _play = _componentType.GetMethod("Play", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            _stop = _componentType.GetMethod("Stop", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            _supported = FindBoolProperty(_componentType, "IsSupported", "Supported");
            _playing = FindBoolProperty(_componentType, "IsPlaying", "Playing");
        }

        public static bool IsProviderTypeForDiscovery(Type type)
        {
            if (type == null || type.Assembly == typeof(SixDofMediaService).Assembly) return false;
            if (!typeof(Component).IsAssignableFrom(type)) return false;
            var name = type.Name;
            if (name != "SixDofVideoRenderer" && name != "VolumetricVideoRenderer" && name != "VolumetricMediaRenderer") return false;
            return FindStringMethod(type, "Open", "Load", "OpenUrl", "LoadUrl") != null;
        }

        private void ClearBinding(bool keepType = false)
        {
            _component = null;
            if (!keepType) _componentType = null;
            _open = _play = _stop = null;
            _supported = _playing = null;
        }

        private static MethodInfo FindStringMethod(Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
                if (method != null) return method;
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

    public sealed class SixDofMediaService : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        [Min(1f)] public float providerRefreshSeconds = OptionalProviderDiscovery.DefaultRetrySeconds;
        private ISixDofMediaProvider _provider;
        private float _nextProviderRefresh;

        public bool IsAvailable => _provider != null && _provider.IsAvailable;
        public bool IsPlaying => _provider != null && _provider.IsPlaying;
        public string StateText => _provider?.StateText ?? "Unavailable · external provider required";

        private void Awake()
        {
            if (signaling == null) signaling = GetComponentInParent<QuestSignalingClient>();
            _nextProviderRefresh = Time.unscaledTime + Mathf.Max(1f, providerRefreshSeconds);
        }

        private void Start() => RefreshCapability();

        private void Update()
        {
            if (Time.unscaledTime < _nextProviderRefresh) return;
            _nextProviderRefresh = Time.unscaledTime + Mathf.Max(1f, providerRefreshSeconds);
            EnsureProvider().Refresh();
            RefreshCapability();
        }

        public void RefreshProvider()
        {
            var provider = EnsureProvider();
            if (provider is ReflectionSixDofMediaProvider reflection) reflection.RefreshExplicit();
            else provider.Refresh();
            RefreshCapability();
        }

        public bool TryPlay(MediaItemDto item, string url)
        {
            var descriptor = SixDofMediaDescriptor.From(item);
            var started = descriptor.IsSixDof && EnsureProvider().Open(url, descriptor);
            RefreshCapability();
            return started;
        }

        public void StopPlayback()
        {
            _provider?.Stop();
            RefreshCapability();
        }

        private ISixDofMediaProvider EnsureProvider()
        {
            if (_provider == null) _provider = new ReflectionSixDofMediaProvider();
            return _provider;
        }

        private void RefreshCapability() => signaling?.ReportCapabilityState("media.6dof.render",
            available: IsAvailable, authorized: IsAvailable, active: IsPlaying);

        private void OnDestroy()
        {
            _provider?.Stop();
            signaling?.ReportCapabilityState("media.6dof.render", active: false);
        }
    }

    internal static class SixDofMediaBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var service = receiver.GetComponent<SixDofMediaService>() ?? receiver.gameObject.AddComponent<SixDofMediaService>();
                service.signaling = receiver.signaling;
            }
        }
    }
}
