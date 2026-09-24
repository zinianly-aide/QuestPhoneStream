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
        private bool _suppressManipulationUntilRelease;

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
            if (_suppressManipulationUntilRelease)
            {
                if (transformEvent.activeGrabCount == 0)
                    _suppressManipulationUntilRelease = false;
                return;
            }

            // A Surface press that already owns the gesture wins over a later Grip/pinch.
            // Suppress the whole manipulation gesture until every grab is released so it
            // cannot suddenly start after the Surface gesture ends while Grip is still held.
            if (transformEvent.activeGrabCount > 0 && touchController != null && touchController.IsActive)
            {
                _suppressManipulationUntilRelease = true;
                return;
            }

            manipulator?.Apply(transformEvent);
        }

        public void ClearInteractionState()
        {
            _suppressManipulationUntilRelease = false;
            touchController?.Clear();
            manipulator?.ClearInteractionState();
        }

        protected virtual void OnDisable() => ClearInteractionState();
    }
}
