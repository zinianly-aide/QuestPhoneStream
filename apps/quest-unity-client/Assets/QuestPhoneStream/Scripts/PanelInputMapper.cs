using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QuestPhoneStream
{
    /// <summary>
    /// Maps controller / head-gaze ray hits on the PhonePanel collider to
    /// Android touch coordinates sent over the WebRTC control data channel.
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

        private int _androidWidth = 720;
        private int _androidHeight = 1280;

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
            // Block click passthrough while the settings panel is open.
            if (settingsUI != null && settingsUI.IsVisible) return;

            if (clickAction != null && clickAction.WasPressedThisFrame())
            {
                TryClick();
            }
        }

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

        /// <summary>Cast the active ray (controller, fallback head gaze) and send a click on hit.</summary>
        public bool TryClick()
        {
            if (panelCollider == null || controlChannel == null) return false;

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
                Debug.LogWarning("[QuestPhoneStream] PanelInputMapper: no ray source available");
                return false;
            }

            if (!panelCollider.Raycast(ray, out RaycastHit hit, 20f))
            {
                Debug.Log($"[QuestPhoneStream] PanelInputMapper: ray missed panel");
                return false;
            }
            return SendClick(hit.textureCoord);
        }

        /// <summary>Convert panel UV to Android pixel coordinates and send over the control channel.</summary>
        public bool SendClick(Vector2 uv)
        {
            int x = Mathf.RoundToInt(Mathf.Clamp01(uv.x) * _androidWidth);
            int y = Mathf.RoundToInt((1f - Mathf.Clamp01(uv.y)) * _androidHeight);
            controlChannel.SendClick(x, y);
            Debug.Log($"[QuestPhoneStream] SendClick uv=({uv.x:F3},{uv.y:F3}) -> px=({x},{y}) res={_androidWidth}x{_androidHeight}");
            return true;
        }
    }
}
