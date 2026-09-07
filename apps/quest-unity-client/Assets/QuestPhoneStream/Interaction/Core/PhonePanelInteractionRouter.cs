using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public sealed class PhonePanelInteractionRouter : MonoBehaviour
    {
        public Collider screenCollider;
        public Collider grabCollider;
        public PhonePanelTouchController touchController;
        public PhonePanelManipulator manipulator;
        public InteractionBackendManager backendManager;

        public void Attach(IInteractionBackend backend)
        {
            Detach();
            if (backend == null) return;
            backend.Pointer.PointerEventRaised += RoutePointer;
            backend.Manipulation.TransformEventRaised += RouteTransform;
        }
        public void Detach()
        {
            var backend = backendManager != null ? backendManager.ActiveBackend : null;
            if (backend == null) return;
            backend.Pointer.PointerEventRaised -= RoutePointer;
            backend.Manipulation.TransformEventRaised -= RouteTransform;
        }
        public void RoutePointer(PointerEvent pointer)
        {
            if (pointer.target != screenCollider && pointer.targetId != "PhoneScreen") return;
            if (manipulator != null && manipulator.IsGrabActive) return;
            touchController?.Process(pointer);
        }
        public void RouteTransform(TransformEvent transformEvent)
        {
            if (transformEvent.activeGrabCount > 0) touchController?.Clear();
            manipulator?.Apply(transformEvent);
        }
        public void ClearInteractionState() { touchController?.Clear(); manipulator?.ClearInteractionState(); }
        private void OnDisable() => ClearInteractionState();
    }
}
