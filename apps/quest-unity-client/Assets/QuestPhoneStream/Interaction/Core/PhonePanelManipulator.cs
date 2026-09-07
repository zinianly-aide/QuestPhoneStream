using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public enum PhonePanelInteractionState { Idle, ScreenTouch, OneHandGrab, TwoHandTransform }

    public sealed class PhonePanelManipulator : MonoBehaviour
    {
        [Min(0.1f)] public float defaultDistance = 1.5f;
        [Min(0.01f)] public float minScale = 0.5f;
        [Min(0.01f)] public float maxScale = 2.5f;
        public Renderer frameRenderer;
        public PhonePanelInteractionState State { get; private set; }
        public bool IsGrabActive => State == PhonePanelInteractionState.OneHandGrab || State == PhonePanelInteractionState.TwoHandTransform;

        public void ResetPose(Camera camera)
        {
            if (camera == null) return;
            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            transform.SetPositionAndRotation(camera.transform.position + forward.normalized * defaultDistance,
                Quaternion.LookRotation(forward.normalized, Vector3.up));
            SetUniformScale(1f);
            ClearInteractionState();
        }

        public void Apply(TransformEvent value)
        {
            State = value.activeGrabCount >= 2 ? PhonePanelInteractionState.TwoHandTransform :
                value.activeGrabCount == 1 ? PhonePanelInteractionState.OneHandGrab : PhonePanelInteractionState.Idle;
            if (value.activeGrabCount > 0)
            {
                transform.SetPositionAndRotation(value.position, value.rotation);
                SetUniformScale(value.uniformScale);
            }
            ApplyFeedback();
        }

        public void BeginScreenTouch() { if (!IsGrabActive) State = PhonePanelInteractionState.ScreenTouch; }
        public void EndScreenTouch() { if (State == PhonePanelInteractionState.ScreenTouch) State = PhonePanelInteractionState.Idle; }
        public void ClearInteractionState() { State = PhonePanelInteractionState.Idle; ApplyFeedback(); }
        public void SetUniformScale(float value) => transform.localScale = Vector3.one * Mathf.Clamp(value, minScale, maxScale);

        private void ApplyFeedback()
        {
            if (frameRenderer == null || frameRenderer.sharedMaterial == null || !frameRenderer.sharedMaterial.HasProperty("_Color")) return;
            frameRenderer.material.color = IsGrabActive ? new Color(0.25f, 0.85f, 1f, 1f) : Color.white;
        }
        private void OnDisable() => ClearInteractionState();
    }
}
