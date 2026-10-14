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
            // Diagnostic only on transitions; avoid hover-frame log spam.
            var diagnostic = pointer.phase == InteractionPhase.PressBegin ||
                pointer.phase == InteractionPhase.PressEnd;
            if (screenCollider == null || pointer.target != screenCollider)
            {
                if (diagnostic)
                    Debug.Log("[QPS-Route] ignored target mismatch panel=" + name);
                return;
            }
            if (manipulator != null && manipulator.IsGrabActive)
            {
                if (diagnostic) Debug.Log("[QPS-Route] blocked by grab panel=" + name);
                return;
            }
            if (touchController == null)
            {
                if (diagnostic) Debug.Log("[QPS-Route] missing input controller panel=" + name);
                return;
            }
            var accepted = touchController.Process(pointer);
            if (diagnostic)
                Debug.Log("[QPS-Route] panel=" + name + " phase=" + pointer.phase
                    + " accepted=" + accepted + " handler="
                    + (touchController.Handler == null ? "null" : touchController.Handler.GetType().Name));
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
