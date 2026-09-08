using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public class SpatialPanelInteractionRouter : MonoBehaviour
    {
        public Collider screenCollider;
        public Collider grabCollider;
        public SurfaceInputController touchController;
        public SpatialPanelManipulator manipulator;
        public InteractionBackendManager backendManager;
        private IInteractionBackend _attached;

        public void Attach(IInteractionBackend backend)
        {
            Detach();
            if (backend == null) return;
            _attached = backend;
            backend.Pointer.PointerEventRaised += RoutePointer;
            backend.Manipulation.TransformEventRaised += RouteTransform;
        }
        public void Detach()
        {
            var backend = _attached;
            _attached = null;
            if (backend == null) return;
            backend.Pointer.PointerEventRaised -= RoutePointer;
            backend.Manipulation.TransformEventRaised -= RouteTransform;
        }
        public void RoutePointer(PointerEvent pointer)
        {
            if (screenCollider == null || pointer.target != screenCollider) return;
            if (manipulator != null && manipulator.IsGrabActive) return;
            touchController?.Process(pointer);
        }
        public void RouteTransform(TransformEvent transformEvent)
        {
            if (transformEvent.activeGrabCount > 0) touchController?.Clear();
            manipulator?.Apply(transformEvent);
        }
        public void ClearInteractionState() { touchController?.Clear(); manipulator?.ClearInteractionState(); }
        protected virtual void OnDisable() => ClearInteractionState();
    }
}
