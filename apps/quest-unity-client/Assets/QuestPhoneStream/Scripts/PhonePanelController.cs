using UnityEngine;
using QuestPhoneStream.Interaction;

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
            var manipulator = GetComponent<PhonePanelManipulator>();
            if (manipulator != null) manipulator.SetUniformScale(1f);
            else transform.localScale = _initialLocalScale;
        }

        public void ResetPose()
        {
            var manipulator = GetComponent<PhonePanelManipulator>();
            if (manipulator == null) return;
            var camera = Camera.main ?? FindFirstObjectByType<Camera>();
            manipulator.ResetPose(camera);
        }

        public void Recenter() => ResetPose();

        private void SetUniformScale(float scale)
        {
            var manipulator = GetComponent<PhonePanelManipulator>();
            if (manipulator != null)
            {
                manipulator.SetUniformScale(scale);
                return;
            }
            float clamped = Mathf.Clamp(scale, minScale, maxScale);
            transform.localScale = new Vector3(clamped, clamped, clamped);
        }
    }
}
