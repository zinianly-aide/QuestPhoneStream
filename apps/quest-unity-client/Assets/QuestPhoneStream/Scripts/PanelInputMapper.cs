using UnityEngine;
using UnityEngine.InputSystem;

namespace QuestPhoneStream
{
    public sealed class PanelInputMapper : MonoBehaviour
    {
        public Camera rayCamera;
        public Collider panelCollider;
        public ControlChannel controlChannel;
        public InputActionProperty clickAction;
        public int androidWidth = 1280;
        public int androidHeight = 720;

        private void Reset()
        {
            panelCollider = GetComponent<Collider>();
            controlChannel = FindFirstObjectByType<ControlChannel>();
        }

        private void Update()
        {
            if (clickAction.action != null && clickAction.action.WasPressedThisFrame())
            {
                TryClickFromCenterRay();
            }
        }

        public bool TryClickFromCenterRay()
        {
            if (rayCamera == null || panelCollider == null || controlChannel == null) return false;
            var ray = new Ray(rayCamera.transform.position, rayCamera.transform.forward);
            if (!panelCollider.Raycast(ray, out RaycastHit hit, 20f)) return false;
            return SendClick(hit.textureCoord);
        }

        public bool SendClick(Vector2 uv)
        {
            int x = Mathf.RoundToInt(Mathf.Clamp01(uv.x) * androidWidth);
            int y = Mathf.RoundToInt((1f - Mathf.Clamp01(uv.y)) * androidHeight);
            controlChannel.SendClick(x, y);
            return true;
        }
    }
}

