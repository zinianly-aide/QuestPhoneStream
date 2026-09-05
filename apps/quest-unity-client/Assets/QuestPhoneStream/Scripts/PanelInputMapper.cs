using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QuestPhoneStream
{
    /// <summary>
    /// Maps controller / head-gaze ray hits on the PhonePanel collider to
    /// Android touch coordinates sent over the WebRTC control data channel.
    ///
    /// Gesture model:
    ///   Trigger pressed  → record start UV (must hit panel)
    ///   held             → track current UV each frame
    ///   Trigger released → if moved &gt; threshold → swipe, else → click
    /// </summary>
    public sealed class PanelInputMapper : MonoBehaviour
    {
        [Header("Ray Source")]
        public Camera rayCamera; // fallback: head gaze when no controller is wired
        public XRRayInteractor controllerInteractor; // right-hand controller ray (set at runtime)

        [Header("Targets")]
        public Collider panelCollider;
        public ControlChannel controlChannel;

        [Header("Input")]
        public InputAction clickAction; // trigger press (set at runtime)

        [Header("Gate")]
        public SettingsUI settingsUI; // blocks panel clicks while settings panel is visible

        [Header("Gesture")]
        [Tooltip("Minimum pixel distance (in Android screen space) between press and release to count as a swipe instead of a click.")]
        public int swipeThresholdPixels = 24;

        [Header("Cursor Highlight")]
        [Tooltip("Shows a live marker at the controller ray hit point on the panel.")]
        public bool showCursor = true;
        [Tooltip("Optional prefab/transform to use as the cursor. If null, a red sphere is created at runtime.")]
        public Transform cursorIndicator;

        private int _androidWidth = 720;
        private int _androidHeight = 1280;
        private GameObject _runtimeCursor;

        // Gesture state
        private bool _gestureActive;
        private Vector2 _gestureStartUv;
        private Vector2 _lastUv;
        private float _gestureStartTime;

        public int AndroidWidth => _androidWidth;
        public int AndroidHeight => _androidHeight;

        private void Reset()
        {
            panelCollider = GetComponent<Collider>();
            controlChannel = FindFirstObjectByType<ControlChannel>();
        }

        private void Update()
        {
            // Lazy-resolve settings UI if not wired at init time.
            if (settingsUI == null) settingsUI = FindFirstObjectByType<SettingsUI>();

            // Live cursor highlight at the ray hit point (helps debug coordinate mapping).
            UpdateCursor();

            // Block touch passthrough while the settings panel is open.
            if (settingsUI != null && settingsUI.IsVisible)
            {
                // Cancel any in-progress gesture so we don't send a stale swipe later.
                _gestureActive = false;
                return;
            }

            if (clickAction == null) return;

            if (clickAction.WasPressedThisFrame())
                BeginGesture();

            if (!_gestureActive) return;

            // Track the latest ray hit UV while the trigger is held down.
            // If the ray leaves the panel, keep the last valid position (so a
            // swipe that briefly goes off-panel still completes sensibly).
            if (TryGetPanelUv(out var currentUv))
                _lastUv = currentUv;

            if (clickAction.WasReleasedThisFrame())
                EndGesture();
        }

        // ── Gesture lifecycle ─────────────────────────────────────────────

        private void BeginGesture()
        {
            if (!TryGetPanelUv(out var uv))
            {
                Debug.Log("[QuestPhoneStream] Gesture begin: ray missed panel, ignoring press");
                return;
            }

            _gestureActive = true;
            _gestureStartUv = uv;
            _lastUv = uv;
            _gestureStartTime = Time.unscaledTime;
            Debug.Log($"[QuestPhoneStream] Gesture begin uv=({uv.x:F3},{uv.y:F3})");
        }

        private void EndGesture()
        {
            if (!_gestureActive) return;
            _gestureActive = false;

            var start = ToAndroidPixels(_gestureStartUv);
            var end = ToAndroidPixels(_lastUv);

            int dx = end.x - start.x;
            int dy = end.y - start.y;
            int distSq = dx * dx + dy * dy;
            int thresholdSq = swipeThresholdPixels * swipeThresholdPixels;

            if (distSq >= thresholdSq)
            {
                int durationMs = Mathf.Clamp(
                    Mathf.RoundToInt((Time.unscaledTime - _gestureStartTime) * 1000f),
                    100, 2000);

                controlChannel.SendSwipe(start.x, start.y, end.x, end.y, durationMs);
                Debug.Log($"[QuestPhoneStream] Swipe ({start.x},{start.y})→({end.x},{end.y}) " +
                          $"dist={Mathf.Sqrt(distSq):F0}px dur={durationMs}ms res={_androidWidth}x{_androidHeight}");
            }
            else
            {
                controlChannel.SendClick(start.x, start.y);
                Debug.Log($"[QuestPhoneStream] Click ({start.x},{start.y}) " +
                          $"dist={Mathf.Sqrt(distSq):F0}px res={_androidWidth}x{_androidHeight}");
            }
        }

        // ── Ray / UV helpers ──────────────────────────────────────────────

        /// <summary>Cast the active ray (controller, fallback head gaze) and return the panel UV at the hit point.</summary>
        private bool TryGetPanelUv(out Vector2 uv)
        {
            uv = default;
            if (panelCollider == null) return false;

            Ray ray;
            if (controllerInteractor != null)
            {
                var origin = controllerInteractor.rayOriginTransform != null
                    ? controllerInteractor.rayOriginTransform
                    : controllerInteractor.transform;
                ray = new Ray(origin.position, origin.forward);
            }
            else if (rayCamera != null)
            {
                ray = new Ray(rayCamera.transform.position, rayCamera.transform.forward);
            }
            else
            {
                return false;
            }

            if (!panelCollider.Raycast(ray, out RaycastHit hit, 20f))
                return false;

            uv = hit.textureCoord;
            return true;
        }

        /// <summary>Convert panel UV (0-1, origin bottom-left) to Android pixel coordinates (origin top-left).</summary>
        private Vector2Int ToAndroidPixels(Vector2 uv)
        {
            int x = Mathf.RoundToInt(Mathf.Clamp01(uv.x) * _androidWidth);
            int y = Mathf.RoundToInt((1f - Mathf.Clamp01(uv.y)) * _androidHeight);
            return new Vector2Int(x, y);
        }

        // ── Resolution ────────────────────────────────────────────────────

        /// <summary>Update the target Android resolution from the incoming video texture.</summary>
        public void SetAndroidResolution(int width, int height)
        {
            if (width > 0 && height > 0 && (width != _androidWidth || height != _androidHeight))
            {
                _androidWidth = width;
                _androidHeight = height;
                Debug.Log($"[QuestPhoneStream] PanelInputMapper android resolution -> {width}x{height}");
            }
        }

        // ── Cursor highlight ───────────────────────────────────────────────

        /// <summary>Create a visible cursor marker if none was assigned in the inspector.</summary>
        private void EnsureRuntimeCursor()
        {
            if (cursorIndicator != null || _runtimeCursor != null) return;
            try
            {
                _runtimeCursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _runtimeCursor.name = "CursorIndicator";
                _runtimeCursor.transform.localScale = Vector3.one * 0.032f;
                // Remove the sphere's collider so it never blocks the panel raycast.
                var col = _runtimeCursor.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var r = _runtimeCursor.GetComponent<Renderer>();
                if (r != null && r.material != null)
                {
                    r.material.color = new Color(1f, 0.15f, 0.15f, 1f);
                }
                _runtimeCursor.SetActive(false);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[QuestPhoneStream] Failed to create cursor: {e.Message}");
            }
        }

        /// <summary>Raycast every frame and position the cursor marker at the hit point on the panel.</summary>
        private void UpdateCursor()
        {
            if (!showCursor || panelCollider == null) return;
            EnsureRuntimeCursor();
            var cursorGo = cursorIndicator != null ? cursorIndicator.gameObject : _runtimeCursor;
            if (cursorGo == null) return;

            Ray ray;
            if (controllerInteractor != null)
            {
                var origin = controllerInteractor.rayOriginTransform != null
                    ? controllerInteractor.rayOriginTransform
                    : controllerInteractor.transform;
                ray = new Ray(origin.position, origin.forward);
            }
            else if (rayCamera != null)
            {
                ray = new Ray(rayCamera.transform.position, rayCamera.transform.forward);
            }
            else { cursorGo.SetActive(false); return; }

            if (panelCollider.Raycast(ray, out RaycastHit hit, 20f))
            {
                cursorGo.SetActive(true);
                // Offset well in front of the panel so the entire sphere is visible
                // (sphere radius ~0.016m at scale 0.032; offset 0.035m prevents z-fighting).
                cursorGo.transform.position = hit.point + hit.normal * 0.035f;
                cursorGo.transform.rotation = Quaternion.LookRotation(-hit.normal);
            }
            else
            {
                cursorGo.SetActive(false);
            }
        }

        private void OnDisable()
        {
            _gestureActive = false;
        }

        private void OnDestroy()
        {
            if (_runtimeCursor != null) Destroy(_runtimeCursor);
        }
    }
}
