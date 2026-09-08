using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QuestPhoneStream.Interaction.Backends.XRI
{
    public sealed class XriPointerSource : MonoBehaviour, IPointerSource
    {
        public event Action<PointerEvent> PointerEventRaised;
        private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
        private readonly Dictionary<InteractionSourceType, bool> _pressed = new Dictionary<InteractionSourceType, bool>();
        private Collider _screen;
        private XriRuntimeDependencies _dependencies;
        private XRHandSubsystem _handSubsystem;
        private readonly Dictionary<InteractionSourceType, Vector3> _last = new Dictionary<InteractionSourceType, Vector3>();
        [SerializeField] private float pressThreshold = .008f;
        [SerializeField] private float releaseThreshold = .025f;

        public void Configure(Collider screen, XriRuntimeDependencies dependencies) { _screen = screen; _dependencies = dependencies; }
        private void Update()
        {
            ProcessRay(InteractionSourceType.LeftController, _dependencies?.leftRay, _dependencies?.leftClick);
            ProcessRay(InteractionSourceType.RightController, _dependencies?.rightRay, _dependencies?.rightClick);
            var hands = ResolveHands();
            if (hands != null) { ProcessHand(InteractionSourceType.LeftHand, hands.leftHand); ProcessHand(InteractionSourceType.RightHand, hands.rightHand); }
            else { EndHand(InteractionSourceType.LeftHand, true); EndHand(InteractionSourceType.RightHand, true); }
        }

        private void ProcessRay(InteractionSourceType source, XRRayInteractor ray, UnityEngine.InputSystem.InputAction action)
        {
            if (ray == null || action == null || !ray.isActiveAndEnabled) { EndHand(source, true); return; }
            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            if (!TryHit(new Ray(origin.position, origin.forward), ray.maxRaycastDistance, out var hit))
            {
                if (_pressed.TryGetValue(source, out var pressed) && pressed && !action.IsPressed())
                {
                    _pressed[source] = false;
                    Raise(source, PointerModality.Ray, InteractionPhase.PressEnd,
                        _last.TryGetValue(source, out var last) ? last : origin.position, -_screen.transform.forward);
                }
                return;
            }
            _last[source] = hit.point;
            var phase = action.WasPressedThisFrame() ? InteractionPhase.PressBegin : action.IsPressed() ? InteractionPhase.PressMove : InteractionPhase.HoverMove;
            Raise(source, PointerModality.Ray, phase, hit.point, hit.normal);
            _pressed[source] = action.IsPressed();
            if (action.WasReleasedThisFrame()) Raise(source, PointerModality.Ray, InteractionPhase.PressEnd, hit.point, hit.normal);
        }

        private void ProcessHand(InteractionSourceType source, XRHand hand)
        {
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tip))
            { EndHand(source, true); return; }
            if (_screen == null || !_screen.enabled) { EndHand(source, true); return; }
            var tracking = _dependencies?.trackingOrigin;
            var point = tracking != null ? tracking.TransformPoint(tip.position) : tip.position;
            var pressed = _pressed.TryGetValue(source, out var active) && active;
            var nearest = _screen.ClosestPoint(point);
            var distance = Vector3.Distance(nearest, point);
            if (!PokeHysteresis.IsPressed(pressed, distance, pressThreshold, releaseThreshold)) { EndHand(source); return; }
            _last[source] = nearest;
            Raise(source, PointerModality.Poke, pressed ? InteractionPhase.PressMove : InteractionPhase.PressBegin, nearest, -_screen.transform.forward);
            _pressed[source] = true;
        }
        private void EndHand(InteractionSourceType source, bool cancel = false)
        {
            if (!_pressed.TryGetValue(source, out var pressed) || !pressed) return;
            _pressed[source] = false;
            Raise(source, PointerModality.Poke, cancel ? InteractionPhase.PressCancel : InteractionPhase.PressEnd,
                _last.TryGetValue(source, out var point) ? point : Vector3.zero, _screen != null ? -_screen.transform.forward : Vector3.forward);
        }
        private bool TryHit(Ray ray, float distance, out RaycastHit hit)
        {
            hit = default;
            return _screen != null && _screen.Raycast(ray, out hit, distance);
        }
        private void Raise(InteractionSourceType source, PointerModality modality, InteractionPhase phase, Vector3 position, Vector3 normal) =>
            PointerEventRaised?.Invoke(new PointerEvent(source, modality, phase, position, normal, _screen, "PhoneScreen"));
        private XRHandSubsystem ResolveHands()
        {
            if (_handSubsystem != null && _handSubsystem.running) return _handSubsystem;
            _subsystems.Clear(); SubsystemManager.GetSubsystems(_subsystems);
            foreach (var candidate in _subsystems) if (candidate != null && candidate.running) return _handSubsystem = candidate;
            return null;
        }
        public void Cancel()
        {
            foreach (InteractionSourceType source in Enum.GetValues(typeof(InteractionSourceType)))
                EndHand(source, true);
            _pressed.Clear(); _last.Clear();
        }
        private void OnDisable() => Cancel();
    }
}
