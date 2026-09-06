using System;
using System.Reflection;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class QuestVisionFrame
    {
        [NonSerialized] public Texture2D texture;
        public long timestamp;
        public int width;
        public int height;
        public string source = "camera.rgb";

        public byte[] EncodeJpg(int quality = 85) => texture != null ? texture.EncodeToJPG(Mathf.Clamp(quality, 1, 100)) : Array.Empty<byte>();
    }

    public interface IQuestVisionProvider
    {
        bool IsAvailable { get; }
        bool IsAuthorized { get; }
        bool IsActive { get; }
        string StateText { get; }
        void Refresh();
        void RequestPermission(Action<bool> completion);
        bool StartCapture();
        void StopCapture();
        QuestVisionFrame CaptureFrame();
    }

    public static class QuestVisionPermissionGate
    {
        public static bool CanActivate(bool available, bool authorized, bool requested) => available && authorized && requested;
    }

    /// <summary>Compile-safe PCA adapter using the shared bounded provider cache.</summary>
    public sealed class MetaPassthroughCameraProvider : IQuestVisionProvider
    {
        public const string HeadsetCameraPermission = "horizonos.permission.HEADSET_CAMERA";
        public const float DiscoveryRetrySeconds = OptionalProviderDiscovery.DefaultRetrySeconds;
        private const string DiscoveryKey = "camera.rgb.provider";

        private readonly GameObject _owner;
        private Component _component;
        private Type _componentType;
        private MethodInfo _getTexture;
        private PropertyInfo _isPlaying;
        private PropertyInfo _isSupported;
        private PropertyInfo _timestamp;
        private bool _requestedActive;

        public MetaPassthroughCameraProvider(GameObject owner)
        {
            _owner = owner;
            Discover(force: false);
        }

        public bool IsAvailable
        {
            get
            {
                if (_component == null || _getTexture == null) return false;
                if (_isSupported == null) return true;
                try { return Convert.ToBoolean(_isSupported.GetValue(_component)); }
                catch { return false; }
            }
        }

        public bool IsAuthorized
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(HeadsetCameraPermission);
#else
                return false;
#endif
            }
        }

        public bool IsActive
        {
            get
            {
                if (!QuestVisionPermissionGate.CanActivate(IsAvailable, IsAuthorized, _requestedActive)) return false;
                if (_isPlaying == null) return _component != null && (_component as Behaviour)?.enabled == true;
                try { return Convert.ToBoolean(_isPlaying.GetValue(_component)); }
                catch { return false; }
            }
        }

        public string StateText => !IsAvailable ? "Unavailable (PCA provider not present)" :
            !IsAuthorized ? "Permission required" : IsActive ? "Active" : "Ready";

        public void Refresh() => Discover(force: false);

        public void RefreshExplicit()
        {
            OptionalProviderDiscovery.Refresh(DiscoveryKey);
            ClearBinding();
            Discover(force: true);
        }

        public void RequestPermission(Action<bool> completion)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(HeadsetCameraPermission))
            {
                completion?.Invoke(true);
                return;
            }
            var callbacks = new PermissionCallbacks();
            var completed = false;
            Action<bool> finish = granted =>
            {
                if (completed) return;
                completed = true;
                completion?.Invoke(granted);
            };
            callbacks.PermissionGranted += _ => finish(true);
            callbacks.PermissionDenied += _ => finish(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => finish(false);
            Permission.RequestUserPermission(HeadsetCameraPermission, callbacks);
#else
            completion?.Invoke(false);
#endif
        }

        public bool StartCapture()
        {
            if (_component == null) Discover(force: true);
            _requestedActive = true;
            if (!QuestVisionPermissionGate.CanActivate(IsAvailable, IsAuthorized, true)) return false;
            if (_component is Behaviour behaviour) behaviour.enabled = true;
            return true;
        }

        public void StopCapture()
        {
            _requestedActive = false;
            if (_component is Behaviour behaviour) behaviour.enabled = false;
        }

        public QuestVisionFrame CaptureFrame()
        {
            if (!IsActive || _getTexture == null) return null;
            Texture texture;
            try { texture = _getTexture.Invoke(_component, null) as Texture; }
            catch { return null; }
            if (texture == null || texture.width <= 0 || texture.height <= 0) return null;

            var copy = CopyTexture(texture);
            if (copy == null) return null;
            return new QuestVisionFrame
            {
                texture = copy,
                width = copy.width,
                height = copy.height,
                timestamp = ReadTimestampMs()
            };
        }

        private void Discover(bool force)
        {
            if (_component != null && _componentType != null && _getTexture != null) return;
            _componentType = OptionalProviderDiscovery.ResolveType(DiscoveryKey,
                type => type.Name == "PassthroughCameraAccess" && typeof(MonoBehaviour).IsAssignableFrom(type), force);
            if (_componentType == null) { ClearBinding(); return; }

            _component = UnityEngine.Object.FindObjectOfType(_componentType) as Component;
            if (_component == null && _owner != null)
            {
                try
                {
                    _component = _owner.GetComponent(_componentType) ?? _owner.AddComponent(_componentType);
                    if (_component is Behaviour created) created.enabled = false;
                }
                catch { _component = null; }
            }
            if (_component == null) { ClearBinding(keepType: true); return; }

            _getTexture = _componentType.GetMethod("GetTexture", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            _isPlaying = _componentType.GetProperty("IsPlaying", BindingFlags.Instance | BindingFlags.Public);
            _isSupported = _componentType.GetProperty("IsSupported", BindingFlags.Instance | BindingFlags.Public);
            _timestamp = _componentType.GetProperty("Timestamp", BindingFlags.Instance | BindingFlags.Public);
            if (_getTexture == null) ClearBinding(keepType: true);
        }

        private void ClearBinding(bool keepType = false)
        {
            _component = null;
            if (!keepType) _componentType = null;
            _getTexture = null;
            _isPlaying = null;
            _isSupported = null;
            _timestamp = null;
        }

        private long ReadTimestampMs()
        {
            if (_timestamp != null)
            {
                try
                {
                    var value = _timestamp.GetValue(_component);
                    if (value is DateTime dateTime) return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
                }
                catch { }
            }
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static Texture2D CopyTexture(Texture source)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                copy.Apply(false, false);
                return copy;
            }
            catch { return null; }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }

    public sealed class QuestVisionService : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        [Range(0.2f, 5f)] public float sampleHz = 1f;
        [Min(1f)] public float providerRefreshSeconds = MetaPassthroughCameraProvider.DiscoveryRetrySeconds;
        public bool sampledPreviewEnabled;

        private IQuestVisionProvider _provider;
        private float _nextSampleAt;
        private float _nextProviderRefreshAt;
        public QuestVisionFrame LastFrame { get; private set; }
        public event Action<QuestVisionFrame> FrameSampled;
        public bool IsAvailable => _provider != null && _provider.IsAvailable;
        public bool IsAuthorized => _provider != null && _provider.IsAuthorized;
        public bool IsActive => _provider != null && _provider.IsActive;
        public string CameraState => _provider?.StateText ?? "Unavailable";

        private void Awake()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            _provider = new MetaPassthroughCameraProvider(gameObject);
            _nextProviderRefreshAt = Time.unscaledTime + Mathf.Max(1f, providerRefreshSeconds);
        }

        private void Start() => RefreshCapabilityState();

        private void Update()
        {
            var now = Time.unscaledTime;
            if (now >= _nextProviderRefreshAt)
            {
                _nextProviderRefreshAt = now + Mathf.Max(1f, providerRefreshSeconds);
                _provider?.Refresh();
                RefreshCapabilityState();
            }

            if (!sampledPreviewEnabled || !IsActive || now < _nextSampleAt) return;
            _nextSampleAt = now + 1f / Mathf.Max(0.2f, sampleHz);
            CaptureSingleFrame();
        }

        public void RefreshProvider()
        {
            if (_provider is MetaPassthroughCameraProvider meta) meta.RefreshExplicit();
            else _provider?.Refresh();
            RefreshCapabilityState();
        }

        public void RequestPermission(Action<bool> completion = null)
        {
            if (_provider == null) { completion?.Invoke(false); return; }
            _provider.RequestPermission(granted =>
            {
                RefreshCapabilityState();
                completion?.Invoke(granted);
            });
        }

        public bool StartCamera()
        {
            var started = _provider != null && _provider.StartCapture();
            RefreshCapabilityState();
            return started;
        }

        public void StopCamera()
        {
            sampledPreviewEnabled = false;
            _provider?.StopCapture();
            RefreshCapabilityState();
        }

        public QuestVisionFrame CaptureSingleFrame()
        {
            var frame = _provider?.CaptureFrame();
            if (frame == null) return null;
            if (LastFrame?.texture != null && LastFrame.texture != frame.texture) Destroy(LastFrame.texture);
            LastFrame = frame;
            FrameSampled?.Invoke(frame);
            return frame;
        }

        public void SetSampledPreview(bool enabled, float requestedHz = 1f)
        {
            sampledPreviewEnabled = enabled;
            sampleHz = Mathf.Clamp(requestedHz, 0.2f, 5f);
            _nextSampleAt = 0f;
        }

        private void RefreshCapabilityState()
        {
            if (signaling == null || _provider == null) return;
            signaling.ReportCapabilityState("camera.rgb", available: _provider.IsAvailable,
                authorized: _provider.IsAuthorized, active: _provider.IsActive);
        }

        private void OnDestroy()
        {
            _provider?.StopCapture();
            if (LastFrame?.texture != null) Destroy(LastFrame.texture);
            if (signaling != null) signaling.ReportCapabilityState("camera.rgb", available: false, active: false);
        }
    }

    internal static class QuestVisionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var signaling in UnityEngine.Object.FindObjectsOfType<QuestSignalingClient>())
            {
                var service = signaling.GetComponent<QuestVisionService>() ?? signaling.gameObject.AddComponent<QuestVisionService>();
                service.signaling = signaling;
            }
        }
    }
}
