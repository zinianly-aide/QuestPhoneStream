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
        private readonly Dictionary<InteractionSourceType, PointerModality> _active =
            new Dictionary<InteractionSourceType, PointerModality>();
        private Collider _screen;
        private XriRuntimeDependencies _dependencies;
        private XRHandSubsystem _handSubsystem;
        private readonly Dictionary<InteractionSourceType, Vector3> _last =
            new Dictionary<InteractionSourceType, Vector3>();
        [SerializeField] private float pressThreshold = .015f;
        [SerializeField] private float releaseThreshold = .04f;
        private bool _loggedHands;

        public void Configure(Collider screen, XriRuntimeDependencies dependencies)
        {
            _screen = screen;
            _dependencies = dependencies;
        }

        private void Update()
        {
            ProcessRay(InteractionSourceType.LeftController, _dependencies?.leftRay, _dependencies?.leftClick);
            ProcessRay(InteractionSourceType.RightController, _dependencies?.rightRay, _dependencies?.rightClick);
            var hands = ResolveHands();
            if (!_loggedHands)
            {
                _loggedHands = true;
                Debug.Log($"[QPS-Hands] subsystem={(hands != null ? (hands.running ? "running" : "stopped") : "null")} " +
                          $"leftRay={_dependencies?.leftRay != null} leftClick={_dependencies?.leftClick != null}");
            }
            if (hands != null)
            {
                ProcessHand(InteractionSourceType.LeftHand, hands.leftHand);
                ProcessHand(InteractionSourceType.RightHand, hands.rightHand);
            }
            else
            {
                EndGesture(InteractionSourceType.LeftHand, PointerModality.Poke, true);
                EndGesture(InteractionSourceType.RightHand, PointerModality.Poke, true);
            }
        }

        private void ProcessRay(InteractionSourceType source, XRRayInteractor ray, UnityEngine.InputSystem.InputAction action)
        {
            if (ray == null || action == null || !ray.isActiveAndEnabled)
            {
                EndGesture(source, PointerModality.Ray, true);
                return;
            }

            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            if (!TryHit(new Ray(origin.position, origin.forward), ray.maxRaycastDistance, out var hit))
            {
                if (IsActive(source, PointerModality.Ray) && !action.IsPressed())
                    EndGesture(source, PointerModality.Ray, false);
                return;
            }

            if (action.WasPressedThisFrame())
            {
                BeginGesture(source, PointerModality.Ray, hit.point, hit.normal);
                return;
            }

            if (IsActive(source, PointerModality.Ray))
            {
                if (action.WasReleasedThisFrame() || !action.IsPressed())
                {
                    EndGesture(source, PointerModality.Ray, false, hit.point, hit.normal);
                    return;
                }

                MoveGesture(source, PointerModality.Ray, hit.point, hit.normal);
                return;
            }

            Raise(source, PointerModality.Ray, InteractionPhase.HoverMove, hit.point, hit.normal);
        }

        private void ProcessHand(InteractionSourceType source, XRHand hand)
        {
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tip))
            {
                EndGesture(source, PointerModality.Poke, true);
                return;
            }

            if (_screen == null || !_screen.enabled)
            {
                EndGesture(source, PointerModality.Poke, true);
                return;
            }

            var tracking = _dependencies?.trackingOrigin;
            var point = tracking != null ? tracking.TransformPoint(tip.position) : tip.position;
            var pressed = IsActive(source, PointerModality.Poke);
            var nearest = _screen.ClosestPoint(point);
            var distance = Vector3.Distance(nearest, point);
            if (!PokeHysteresis.IsPressed(pressed, distance, pressThreshold, releaseThreshold))
            {
                EndGesture(source, PointerModality.Poke, false, nearest, -_screen.transform.forward);
                return;
            }

            if (pressed)
                MoveGesture(source, PointerModality.Poke, nearest, -_screen.transform.forward);
            else
                BeginGesture(source, PointerModality.Poke, nearest, -_screen.transform.forward);
        }

        private bool IsActive(InteractionSourceType source, PointerModality modality) =>
            _active.TryGetValue(source, out var activeModality) && activeModality == modality;

        private void BeginGesture(InteractionSourceType source, PointerModality modality, Vector3 position, Vector3 normal)
        {
            if (_active.TryGetValue(source, out var activeModality))
            {
                if (activeModality == modality) return;
                EndGesture(source, activeModality, true);
            }

            _active[source] = modality;
            _last[source] = position;
            Raise(source, modality, InteractionPhase.PressBegin, position, normal);
        }

        private void MoveGesture(InteractionSourceType source, PointerModality modality, Vector3 position, Vector3 normal)
        {
            if (!IsActive(source, modality)) return;
            _last[source] = position;
            Raise(source, modality, InteractionPhase.PressMove, position, normal);
        }

        private void EndGesture(InteractionSourceType source, PointerModality modality, bool cancel)
        {
            var point = _last.TryGetValue(source, out var last) ? last : Vector3.zero;
            var normal = _screen != null ? -_screen.transform.forward : Vector3.forward;
            EndGesture(source, modality, cancel, point, normal);
        }

        private void EndGesture(InteractionSourceType source, PointerModality modality, bool cancel,
            Vector3 position, Vector3 normal)
        {
            if (!IsActive(source, modality)) return;
            _active.Remove(source);
            _last.Remove(source);
            Raise(source, modality, cancel ? InteractionPhase.PressCancel : InteractionPhase.PressEnd, position, normal);
        }

        private bool TryHit(Ray ray, float distance, out RaycastHit hit)
        {
            hit = default;
            return _screen != null && _screen.Raycast(ray, out hit, distance);
        }

        private void Raise(InteractionSourceType source, PointerModality modality, InteractionPhase phase,
            Vector3 position, Vector3 normal) =>
            PointerEventRaised?.Invoke(new PointerEvent(source, modality, phase, position, normal, _screen, "PhoneScreen"));

        private XRHandSubsystem ResolveHands()
        {
            if (_handSubsystem != null && _handSubsystem.running) return _handSubsystem;
            _subsystems.Clear();
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var candidate in _subsystems)
                if (candidate != null && candidate.running)
                    return _handSubsystem = candidate;
            return null;
        }

        public void Cancel()
        {
            var active = new List<KeyValuePair<InteractionSourceType, PointerModality>>(_active);
            foreach (var gesture in active)
                EndGesture(gesture.Key, gesture.Value, true);
            _active.Clear();
            _last.Clear();
        }

        private void OnDisable() => Cancel();
    }
}
