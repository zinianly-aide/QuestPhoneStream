using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class PhonePanelController : MonoBehaviour
    {
        public Transform anchor;
        public float minScale = 0.35f;
        public float maxScale = 1.4f;
        public float scaleStep = 0.05f;
        public bool followAnchor;

        private Vector3 _initialLocalScale;

        private void Awake()
        {
            _initialLocalScale = transform.localScale;
        }

        private void LateUpdate()
        {
            if (followAnchor && anchor != null)
            {
                transform.position = anchor.position;
                transform.rotation = anchor.rotation;
            }
        }

        public void SetFollowAnchor(bool enabled)
        {
            followAnchor = enabled;
        }

        public void ScaleUp()
        {
            SetUniformScale(transform.localScale.x + scaleStep);
        }

        public void ScaleDown()
        {
            SetUniformScale(transform.localScale.x - scaleStep);
        }

        public void ResetScale()
        {
            var spatial = GetComponent<PhonePanelSpatialInteraction>();
            if (spatial != null) spatial.SetUniformScale(1f);
            else transform.localScale = _initialLocalScale;
        }

        public void ResetPose()
        {
            var spatial = GetComponent<PhonePanelSpatialInteraction>();
            if (spatial == null) return;
            var camera = Camera.main ?? FindFirstObjectByType<Camera>();
            spatial.ResetPose(camera);
        }

        public void Recenter() => ResetPose();

        private void SetUniformScale(float scale)
        {
            var spatial = GetComponent<PhonePanelSpatialInteraction>();
            if (spatial != null)
            {
                spatial.SetUniformScale(scale);
                return;
            }
            float clamped = Mathf.Clamp(scale, minScale, maxScale);
            transform.localScale = new Vector3(clamped, clamped, clamped);
        }
    }
}
