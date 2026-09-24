using UnityEngine;
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
