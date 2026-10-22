using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using QuestPhoneStream.Interaction;

namespace QuestPhoneStream
{
    /// <summary>
    /// SDK-neutral mapping only. Interaction backends provide pointer events to
    /// PhonePanelTouchController; this component maps panel hit positions to Android pixels.
    /// </summary>
    public sealed class PanelInputMapper : MonoBehaviour, IPhonePanelTouchMapper
    {
        public Camera rayCamera;
        public Collider panelCollider;
        public ControlChannel controlChannel;
        public SettingsUI settingsUI;
        [Tooltip("Minimum Android-pixel movement that is sent as a swipe.")]
        public int swipeThresholdPixels = 24;
        public bool showCursor = true;
        public Transform cursorIndicator;

        private XRRayInteractor _macScrollRay;
        private QuestWebRtcReceiver _receiver;
        private float _nextScrollAt;
        private const float ScrollIntervalSeconds = 0.085f;
        private const float ScrollDeadZone = 0.28f;

        public void ConfigureMacScroll(XRRayInteractor rightRay, QuestWebRtcReceiver receiver)
        {
            _macScrollRay = rightRay;
            _receiver = receiver;
        }

        /// <summary>Room locomotion must yield the right stick while targeting a live Mac screen.</summary>
        public bool IsMacScrollTargeted()
        {
            if (_receiver == null || !_receiver.CanSendMacSurfaceInput ||
                IsInputBlocked || controlChannel == null || !controlChannel.IsOpen ||
                _macScrollRay == null || !_macScrollRay.isActiveAndEnabled ||
                panelCollider == null || !panelCollider.enabled) return false;
            var origin = _macScrollRay.rayOriginTransform != null ?
                _macScrollRay.rayOriginTransform : _macScrollRay.transform;
            return TryMapHitToUv(new Ray(origin.position, origin.forward), out _);
        }

        // The hardware thumbstick is independent from XRI press/grab semantics.
        // Only emit wheel steps while the right controller ray intersects this
        // Mac screen; UI, Android media and frame grips retain their behavior.
        private void Update()
        {
            if (_receiver == null || !_receiver.CanSendMacSurfaceInput ||
                IsInputBlocked || controlChannel == null || !controlChannel.IsOpen ||
                _macScrollRay == null || !_macScrollRay.isActiveAndEnabled ||
                panelCollider == null || !panelCollider.enabled ||
                Time.unscaledTime < _nextScrollAt) return;

            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!device.isValid ||
                !device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return;
            var deltaX = ScrollSteps(stick.x);
            var deltaY = ScrollSteps(stick.y);
            if (deltaX == 0 && deltaY == 0) return;

            var origin = _macScrollRay.rayOriginTransform != null ?
                _macScrollRay.rayOriginTransform : _macScrollRay.transform;
            var ray = new Ray(origin.position, origin.forward);
            if (!TryMapHitToUv(ray, out var uv)) return;
            var point = MapUvToAndroidPixels(uv);
            controlChannel.SendScroll(point.x, point.y, deltaX, deltaY);
            _nextScrollAt = Time.unscaledTime + ScrollIntervalSeconds;
        }

        // Small stick deflections are ignored so hand tremors cannot scroll.
        public static int ScrollSteps(float axis)
        {
            if (Mathf.Abs(axis) <= ScrollDeadZone) return 0;
            var count = Mathf.Clamp(Mathf.CeilToInt(
                (Mathf.Abs(axis) - ScrollDeadZone) / (1f - ScrollDeadZone) * 3f), 1, 3);
            return axis > 0f ? count : -count;
        }

        private int _androidWidth = 720;
        private int _androidHeight = 1280;
        public int AndroidWidth => _androidWidth;
        public int AndroidHeight => _androidHeight;
        public bool IsInputBlocked => settingsUI != null && settingsUI.IsVisible;
        public int SwipeThresholdPixels => swipeThresholdPixels;

        private void Reset()
        {
            panelCollider = GetComponent<Collider>();
            controlChannel = FindFirstObjectByType<ControlChannel>();
        }

        public bool TryMapHitToUv(Ray ray, out Vector2 uv)
        {
            uv = default;
            if (panelCollider == null || !panelCollider.Raycast(ray, out RaycastHit hit, 20f)) return false;
            uv = hit.textureCoord;
            return true;
        }

        public bool TryMapWorldPointToUv(Vector3 worldPosition, Vector3 worldNormal, out Vector2 uv)
        {
            var normal = worldNormal.sqrMagnitude > 0.0001f ? worldNormal.normalized : -transform.forward;
            return TryMapHitToUv(new Ray(worldPosition + normal * 0.04f, -normal), out uv);
        }

        public Vector2Int MapUvToAndroidPixels(Vector2 uv)
        {
            return new Vector2Int(
                Mathf.RoundToInt(Mathf.Clamp01(uv.x) * _androidWidth),
                Mathf.RoundToInt((1f - Mathf.Clamp01(uv.y)) * _androidHeight));
        }

        public void SetAndroidResolution(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            _androidWidth = width;
            _androidHeight = height;
        }

        public void SendClick(Vector2Int point) => controlChannel?.SendClick(point.x, point.y);
        public void SendSwipe(Vector2Int start, Vector2Int end, int durationMs) => controlChannel?.SendSwipe(start.x, start.y, end.x, end.y, durationMs);
    }
}
