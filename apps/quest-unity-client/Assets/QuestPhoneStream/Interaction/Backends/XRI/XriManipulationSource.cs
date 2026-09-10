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
        // Bare-hand grab must feel intentional: a real pinch, on the user-facing
        // side of the video, close to the surface. The inflated handle collider
        // is far too large for ClosestPoint and was latching grabs while waving.
        [SerializeField, Min(.01f)] private float handPinchDistance = .028f;
        [SerializeField, Min(.01f)] private float handGrabDistance = .045f;
        private bool _loggedDiagnostics;
        private bool _logGrip;

        public void Configure(Transform panel, Collider handle, XriRuntimeDependencies dependencies)
        {
            _panel = panel; _handle = handle; _dependencies = dependencies;
            _manipulator = panel.GetComponent<SpatialPanelManipulator>();
            _surface = panel.GetComponent<SpatialPanelInteractionRouter>()?.screenCollider;
        }

        private void Update()
        {
            if (_panel == null) { Cancel(); return; }
            if ((_handle == null || !_handle.enabled) && (_surface == null || !_surface.enabled))
            {
                Cancel();
                return;
            }
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
            if (!_loggedDiagnostics)
            {
                _loggedDiagnostics = true;
                var leftGrab = _dependencies?.leftGrab;
                var rightGrab = _dependencies?.rightGrab;
                Debug.Log($"[QPS-Grab] panel={_panel.name} handle={_handle != null && _handle.enabled} " +
                          $"surface={_surface != null && _surface.enabled} " +
                          $"handSubsystems={_subsystems.Count} tracked={tracked} " +
                          $"leftGrab={(leftGrab != null ? (leftGrab.enabled ? "enabled" : "disabled") : "null")} " +
                          $"rightGrab={(rightGrab != null ? (rightGrab.enabled ? "enabled" : "disabled") : "null")}");
            }
            if (!tracked) { End(InteractionSourceType.LeftHand); End(InteractionSourceType.RightHand); }
            if (_solver.Count > 0) Emit(GrabPhase.Update);
        }

        private void Process(InteractionSourceType source, XRRayInteractor ray, UnityEngine.InputSystem.InputAction action)
        {
            if (ray == null || action == null || !ray.isActiveAndEnabled) { End(source); return; }
            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            var pose = new Pose(origin.position, origin.rotation);
            if (action.WasPressedThisFrame())
            {
                var hitOk = HitsGrabbable(origin, ray.maxRaycastDistance, out var hit);
                if (_logGrip)
                    Debug.Log($"[QPS-Grab] grip pressed source={source} hit={hitOk} point={hit.point} dist={hit.distance}");
                if (hitOk)
                {
                    _solver.Begin(source, pose, hit.point, _panel);
                    Emit(GrabPhase.Begin);
                }
            }
            if (!action.IsPressed())
            {
                _solver.SetOrigin(source, pose);
                End(source);
                return;
            }
            _solver.SetOrigin(source, pose);
        }

        private bool HitsGrabbable(Transform origin, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            var ray = new Ray(origin.position, origin.forward);
            var handleHit = default(RaycastHit);
            var surfaceHit = default(RaycastHit);
            var hasHandle = _handle != null && _handle.enabled &&
                _handle.Raycast(ray, out handleHit, maxDistance);
            var hasSurface = _surface != null && _surface.enabled &&
                _surface.Raycast(ray, out surfaceHit, maxDistance);
            // Physics fallback covers MeshColliders whose per-collider Raycast misses thin quads.
            if (!hasHandle && !hasSurface &&
                Physics.Raycast(ray, out var worldHit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_surface != null && worldHit.collider == _surface) { hit = worldHit; return true; }
                if (_handle != null && worldHit.collider == _handle) { hit = worldHit; return true; }
                if (worldHit.collider != null && worldHit.collider.transform.IsChildOf(_panel))
                { hit = worldHit; return true; }
            }
            if (!hasSurface && !hasHandle) return false;
            if (hasSurface && (!hasHandle || surfaceHit.distance <= handleHit.distance))
            {
                hit = surfaceHit;
                return true;
            }
            hit = handleHit;
            return true;
        }

        private void ProcessHand(InteractionSourceType source, XRHand hand)
        {
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb))
            { End(source); return; }

            var pinch = Vector3.Distance(index.position, thumb.position);
            if (pinch > handPinchDistance) { End(source); return; }

            var tracking = _dependencies?.trackingOrigin;
            var position = tracking != null ? tracking.TransformPoint(index.position) : index.position;
            var rotation = tracking != null ? tracking.rotation * index.rotation : index.rotation;
            var pose = new Pose(position, rotation);

            if (_solver.Contains(source))
            {
                _solver.SetOrigin(source, pose);
                return;
            }

            if (!TryGetSurfaceTouch(position, out var closest, out var surfaceDistance)) return;
            if (surfaceDistance > handGrabDistance) return;

            _solver.Begin(source, pose, closest, _panel);
            Emit(GrabPhase.Begin);
            _solver.SetOrigin(source, pose);
        }

        /// <summary>
        /// True when the fingertip is on the user-facing side of the video plane
        /// and projects onto the surface collider (not merely near the fat handle).
        /// </summary>
        private bool TryGetSurfaceTouch(Vector3 position, out Vector3 closest, out float distance)
        {
            closest = default;
            distance = float.MaxValue;
            if (_surface == null || !_surface.enabled) return false;

            closest = _surface.ClosestPoint(position);
            // ClosestPoint returns the query point itself when already inside the collider.
            if (closest == position) closest = _surface.bounds.ClosestPoint(position);
            distance = Vector3.Distance(position, closest);
            if (distance <= float.Epsilon) return false;

            // User-facing side is -forward (see XriPointerSource poke normal).
            var inward = Vector3.Dot(position - closest, -_surface.transform.forward);
            if (inward <= 0f) return false;

            // Reject edge grazes: the touch point must lie inside the surface bounds
            // (slightly padded) so waving beside the panel cannot start a grab.
            var bounds = _surface.bounds;
            bounds.Expand(handGrabDistance * 2f);
            if (!bounds.Contains(closest)) return false;
            return true;
        }

        private void End(InteractionSourceType source)
        {
            if (!_solver.Contains(source)) return;
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
