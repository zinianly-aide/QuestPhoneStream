using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QuestPhoneStream.Interaction.Backends.XRI
{
    public sealed class XriManipulationSource : MonoBehaviour, IManipulationSource
    {
        private struct Grab { public Vector3 position; public Quaternion rotation; }
        public event Action<TransformEvent> TransformEventRaised;
        private readonly Dictionary<InteractionSourceType, Grab> _grabs = new Dictionary<InteractionSourceType, Grab>();
        private readonly List<XRHandSubsystem> _handSubsystems = new List<XRHandSubsystem>();
        private Transform _panel;
        private Collider _handle;
        private XriRuntimeDependencies _dependencies;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _baseMidpoint, _baseDirection, _basePanelPosition;
        private Quaternion _basePanelRotation;
        private float _baseDistance, _baseScale;
        private XRHandSubsystem _hands;
        [SerializeField, Min(.001f)] private float handPinchDistance = .028f;
        [SerializeField, Min(.001f)] private float handHandleDistance = .04f;

        public void Configure(Transform panel, Collider handle, XriRuntimeDependencies dependencies)
        { _panel = panel; _handle = handle; _dependencies = dependencies; }
        private void Update()
        {
            Process(InteractionSourceType.LeftController, _dependencies?.leftRay, _dependencies?.leftGrab);
            Process(InteractionSourceType.RightController, _dependencies?.rightRay, _dependencies?.rightGrab);
            var hands = ResolveHands();
            if (hands != null) { ProcessHand(InteractionSourceType.LeftHand, hands.leftHand); ProcessHand(InteractionSourceType.RightHand, hands.rightHand); }
        }
        private void Process(InteractionSourceType source, XRRayInteractor ray, UnityEngine.InputSystem.InputAction action)
        {
            if (ray == null || action == null || _panel == null) return;
            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            if (action.WasPressedThisFrame() && HitsHandle(origin)) Begin(source, origin.position, origin.rotation);
            if (_grabs.ContainsKey(source) && action.IsPressed()) UpdateGrab(source, origin.position, origin.rotation);
            if (action.WasReleasedThisFrame()) End(source);
        }
        private bool HitsHandle(Transform origin) => _handle != null && _handle.Raycast(new Ray(origin.position, origin.forward), out _, 5f);
        private void Begin(InteractionSourceType source, Vector3 position, Quaternion rotation)
        {
            _grabs[source] = new Grab { position = position, rotation = rotation };
            if (_grabs.Count == 1) { _lastPosition = position; _lastRotation = rotation; }
            else CaptureTwoHandBaseline();
            Emit(GrabPhase.Begin);
        }
        private void UpdateGrab(InteractionSourceType source, Vector3 position, Quaternion rotation)
        {
            _grabs[source] = new Grab { position = position, rotation = rotation };
            if (_grabs.Count == 1)
            {
                var pose = new TransformEvent(GrabPhase.Update, _panel.position + position - _lastPosition,
                    rotation * Quaternion.Inverse(_lastRotation) * _panel.rotation, _panel.localScale.x, 1, source, source);
                _lastPosition = position; _lastRotation = rotation; TransformEventRaised?.Invoke(pose); return;
            }
            Emit(GrabPhase.Update);
        }
        private void End(InteractionSourceType source)
        {
            if (!_grabs.Remove(source)) return;
            if (_grabs.Count == 1) foreach (var pair in _grabs) { _lastPosition = pair.Value.position; _lastRotation = pair.Value.rotation; break; }
            Emit(GrabPhase.End);
        }
        private void ProcessHand(InteractionSourceType source, XRHand hand)
        {
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                Vector3.Distance(index.position, thumb.position) > handPinchDistance || !NearHandle(index.position))
            { End(source); return; }
            if (!_grabs.ContainsKey(source)) Begin(source, index.position, index.rotation);
            else UpdateGrab(source, index.position, index.rotation);
        }
        private bool NearHandle(Vector3 point) => _handle != null && Vector3.Distance(_handle.ClosestPoint(point), point) <= handHandleDistance;
        private XRHandSubsystem ResolveHands()
        {
            if (_hands != null && _hands.running) return _hands;
            _handSubsystems.Clear(); SubsystemManager.GetSubsystems(_handSubsystems);
            foreach (var candidate in _handSubsystems) if (candidate != null && candidate.running) return _hands = candidate;
            return null;
        }
        private void CaptureTwoHandBaseline()
        {
            var values = new List<Grab>(_grabs.Values); var a = values[0]; var b = values[1];
            _baseMidpoint = (a.position + b.position) * .5f; var delta = b.position - a.position;
            _baseDistance = Mathf.Max(.001f, delta.magnitude); _baseDirection = delta / _baseDistance;
            _basePanelPosition = _panel.position; _basePanelRotation = _panel.rotation; _baseScale = _panel.localScale.x;
        }
        private void Emit(GrabPhase phase)
        {
            if (_grabs.Count == 0) { TransformEventRaised?.Invoke(new TransformEvent(phase, _panel.position, _panel.rotation, _panel.localScale.x, 0, default, default)); return; }
            var values = new List<Grab>(_grabs.Values); var primary = default(InteractionSourceType); var secondary = default(InteractionSourceType);
            foreach (var pair in _grabs) { if (values.IndexOf(pair.Value) == 0) primary = pair.Key; else secondary = pair.Key; }
            if (_grabs.Count == 1) { TransformEventRaised?.Invoke(new TransformEvent(phase, _panel.position, _panel.rotation, _panel.localScale.x, 1, primary, primary)); return; }
            var a = values[0]; var b = values[1]; var delta = b.position - a.position; var distance = Mathf.Max(.001f, delta.magnitude);
            TransformEventRaised?.Invoke(new TransformEvent(phase, _basePanelPosition + ((a.position + b.position) * .5f - _baseMidpoint),
                Quaternion.FromToRotation(_baseDirection, delta / distance) * _basePanelRotation, _baseScale * distance / _baseDistance, 2, primary, secondary));
        }
        private void OnDisable() { _grabs.Clear(); if (_panel != null) Emit(GrabPhase.End); }
    }
}
