using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QuestPhoneStream.Interaction.Backends.XRI
{
    public sealed class XriManipulationSource : MonoBehaviour, IManipulationSource
    {
        public event Action<TransformEvent> TransformEventRaised;
        private readonly PanelGrabSolver _solver = new PanelGrabSolver();
        private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
        private Transform _panel;
        private Collider _handle;
        private XriRuntimeDependencies _dependencies;
        private SpatialPanelManipulator _manipulator;
        private Collider _surface;
        public void Configure(Transform panel, Collider handle, XriRuntimeDependencies dependencies)
        {
            _panel = panel; _handle = handle; _dependencies = dependencies;
            _manipulator = panel.GetComponent<SpatialPanelManipulator>();
            _surface = panel.GetComponent<SpatialPanelInteractionRouter>()?.screenCollider;
        }
        private void Update()
        {
            if (_panel == null || _handle == null || !_handle.enabled) { Cancel(); return; }
            Process(InteractionSourceType.LeftController, _dependencies?.leftRay, _dependencies?.leftGrab);
            Process(InteractionSourceType.RightController, _dependencies?.rightRay, _dependencies?.rightGrab);
            _subsystems.Clear(); SubsystemManager.GetSubsystems(_subsystems);
            var tracked = false;
            foreach (var hands in _subsystems)
            {
                if (!hands.running) continue;
                ProcessHand(InteractionSourceType.LeftHand, hands.leftHand);
                ProcessHand(InteractionSourceType.RightHand, hands.rightHand);
                tracked = true; break;
            }
            if (!tracked) { End(InteractionSourceType.LeftHand); End(InteractionSourceType.RightHand); }
            if (_solver.Count > 0) Emit(GrabPhase.Update);
        }
        private void Process(InteractionSourceType source, XRRayInteractor ray, UnityEngine.InputSystem.InputAction action)
        {
            if (ray == null || action == null || !ray.isActiveAndEnabled) { End(source); return; }
            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            var pose = new Pose(origin.position, origin.rotation);
            if (action.WasPressedThisFrame() && _handle.Raycast(new Ray(origin.position, origin.forward), out var hit, ray.maxRaycastDistance))
            {
                // The visible surface wins over a frame behind it: Grip on content is not Grab.
                if (_surface != null && _surface.enabled &&
                    _surface.Raycast(new Ray(origin.position, origin.forward), out var surfaceHit, hit.distance)) return;
                _solver.Begin(source, pose, hit.point, _panel);
                Emit(GrabPhase.Begin);
            }
            if (!action.IsPressed()) { End(source); return; }
            _solver.SetOrigin(source, pose);
        }
        private void ProcessHand(InteractionSourceType source, XRHand hand)
        {
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                Vector3.Distance(index.position, thumb.position) > .035f)
            { End(source); return; }
            var tracking = _dependencies?.trackingOrigin;
            var position = tracking != null ? tracking.TransformPoint(index.position) : index.position;
            var rotation = tracking != null ? tracking.rotation * index.rotation : index.rotation;
            var pose = new Pose(position, rotation);
            var handleDistance = Vector3.Distance(position, _handle.ClosestPoint(position));
            var overSurface = _surface != null && _surface.enabled &&
                Vector3.Distance(position, _surface.ClosestPoint(position)) < handleDistance;
            if (!_solver.Contains(source) && !overSurface && handleDistance <= .04f)
            { _solver.Begin(source, pose, position, _panel); Emit(GrabPhase.Begin); }
            _solver.SetOrigin(source, pose);
        }
        private void End(InteractionSourceType source)
        {
            if (!_solver.Contains(source)) return;
            // Apply the most recent sampled poses before capturing the remaining-hand baseline.
            Emit(GrabPhase.Update);
            _solver.End(source, _panel);
            Emit(GrabPhase.End);
        }
        private void Emit(GrabPhase phase)
        {
            if (_panel != null) TransformEventRaised?.Invoke(_solver.Evaluate(_panel, phase,
                _manipulator != null ? _manipulator.minScale : .5f, _manipulator != null ? _manipulator.maxScale : 2.5f));
        }
        public void Cancel() { _solver.Clear(); Emit(GrabPhase.End); }
        private void OnDisable() => Cancel();
    }
}
