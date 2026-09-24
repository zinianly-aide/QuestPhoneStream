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
        private QuestWebRtcReceiver _receiver;
        private SpatialPanelShell _shell;
        private int _lastVideoWidth;
        private int _lastVideoHeight;

        private void Awake()
        {
            _initialLocalScale = transform.localScale;
            _shell = GetComponent<SpatialPanelShell>();
        }

        private void LateUpdate()
        {
            if (followAnchor && anchor != null)
            {
                transform.position = anchor.position;
                transform.rotation = anchor.rotation;
            }

            RefreshVideoAspect();
        }

        private void RefreshVideoAspect()
        {
            if (_receiver == null) _receiver = FindFirstObjectByType<QuestWebRtcReceiver>();
            if (_receiver == null) return;
            if (_shell == null) _shell = GetComponent<SpatialPanelShell>();
            if (_shell == null || _shell.Surface == null) return;

            var width = _receiver.VideoWidth;
            var height = _receiver.VideoHeight;
            if (width <= 0 || height <= 0 || (width == _lastVideoWidth && height == _lastVideoHeight)) return;

            _shell.SetSurfaceAspect(width, height);
            _lastVideoWidth = width;
            _lastVideoHeight = height;
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
            var manipulator = GetComponent<SpatialPanelManipulator>();
            if (manipulator != null) manipulator.SetUniformScale(1f);
            else transform.localScale = _initialLocalScale;
        }

        public void ResetPose()
        {
            var manipulator = GetComponent<SpatialPanelManipulator>();
            if (manipulator == null) return;
            var camera = Camera.main ?? FindFirstObjectByType<Camera>();
            manipulator.ResetPose(camera);
        }

        public void Recenter() => ResetPose();

        private void SetUniformScale(float scale)
        {
            var manipulator = GetComponent<SpatialPanelManipulator>();
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
