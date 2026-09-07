using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace QuestPhoneStream
{
    /// <summary>
    /// Owns the spatial pose of the phone panel. Screen input and frame grabbing
    /// deliberately have separate colliders and mutually exclusive states.
    /// </summary>
    public sealed class PhonePanelSpatialInteraction : MonoBehaviour
    {
        public enum InteractionState { Idle, ScreenTouch, OneHandGrab, TwoHandTransform }

        [Header("Separated interaction surfaces")]
        public Collider screenCollider;
        public Collider grabCollider;
        public Renderer frameRenderer;

        [Header("Placement")]
        [Min(0.1f)] public float defaultDistance = 1.5f;
        [Min(0.01f)] public float minScale = 0.5f;
        [Min(0.01f)] public float maxScale = 2.5f;

        [Header("Controller frame grab")]
        public XRRayInteractor leftRay;
        public XRRayInteractor rightRay;
        public InputAction leftGrabAction;
        public InputAction rightGrabAction;

        public InteractionState State { get; private set; }
        public bool IsScreenTouchActive => State == InteractionState.ScreenTouch;
        public bool IsGrabActive => State == InteractionState.OneHandGrab || State == InteractionState.TwoHandTransform;
        public bool IsFrameHover { get; private set; }

        private readonly Dictionary<string, GrabState> _grabs = new Dictionary<string, GrabState>();
        private MaterialPropertyBlock _frameProperties;
        private Color _frameBaseColor = Color.white;

        private sealed class GrabState
        {
            public Vector3 startPosition;
            public Quaternion startRotation;
        }

        private void Awake()
        {
            _frameProperties = new MaterialPropertyBlock();
            if (frameRenderer != null && frameRenderer.sharedMaterial != null && frameRenderer.sharedMaterial.HasProperty("_Color"))
                _frameBaseColor = frameRenderer.sharedMaterial.color;
        }

        private void Update()
        {
            ProcessController("left-controller", leftRay, leftGrabAction);
            ProcessController("right-controller", rightRay, rightGrabAction);
            UpdateFrameHover();
        }

        public void ResetPose(Camera camera)
        {
            if (camera == null) return;
            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            transform.position = camera.transform.position + forward * defaultDistance;
            // The quad's visible side faces local -Z in this scene setup.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            transform.localScale = Vector3.one;
            ClearInteractionState();
        }

        public bool TryBeginScreenTouch()
        {
            if (IsGrabActive) return false;
            State = InteractionState.ScreenTouch;
            return true;
        }

        public void EndScreenTouch()
        {
            if (State == InteractionState.ScreenTouch) State = InteractionState.Idle;
        }

        public bool TryBeginGrab(string sourceId, Vector3 grabPosition, Quaternion grabRotation)
        {
            if (string.IsNullOrEmpty(sourceId) || State == InteractionState.ScreenTouch) return false;
            if (_grabs.ContainsKey(sourceId)) return true;

            _grabs[sourceId] = new GrabState { startPosition = grabPosition, startRotation = grabRotation };
            if (_grabs.Count == 1)
            {
                State = InteractionState.OneHandGrab;
                _singleLastPosition = grabPosition;
                _singleLastRotation = grabRotation;
            }
            else
            {
                State = InteractionState.TwoHandTransform;
                CaptureTwoHandBaseline();
            }
            ApplyFrameFeedback();
            return true;
        }

        public void UpdateGrab(string sourceId, Vector3 grabPosition, Quaternion grabRotation)
        {
            if (!_grabs.TryGetValue(sourceId, out var grab)) return;
            grab.startPosition = grabPosition;
            grab.startRotation = grabRotation;

            if (_grabs.Count == 1)
            {
                // One hand moves the panel; orientation follows the hand/controller.
                transform.position += grabPosition - _singleLastPosition;
                transform.rotation = grabRotation * Quaternion.Inverse(_singleLastRotation) * transform.rotation;
                _singleLastPosition = grabPosition;
                _singleLastRotation = grabRotation;
            }
            else
            {
                ApplyTwoHandTransform();
            }
        }

        public void EndGrab(string sourceId)
        {
            if (!_grabs.Remove(sourceId)) return;
            if (_grabs.Count == 0)
            {
                State = InteractionState.Idle;
            }
            else
            {
                State = InteractionState.OneHandGrab;
                foreach (var pair in _grabs)
                {
                    _singleLastPosition = pair.Value.startPosition;
                    _singleLastRotation = pair.Value.startRotation;
                    break;
                }
            }
            ApplyFrameFeedback();
        }

        public void ClearInteractionState()
        {
            _grabs.Clear();
            State = InteractionState.Idle;
            ApplyFrameFeedback();
        }

        private Vector3 _singleLastPosition;
        private Quaternion _singleLastRotation = Quaternion.identity;
        private Vector3 _twoStartMidpoint;
        private Vector3 _twoStartDirection;
        private float _twoStartDistance;
        private Vector3 _twoStartPanelPosition;
        private Quaternion _twoStartPanelRotation;
        private float _twoStartScale;

        private void CaptureTwoHandBaseline()
        {
            var states = new List<GrabState>(_grabs.Values);
            var first = states[0];
            var second = states[1];
            _twoStartMidpoint = (first.startPosition + second.startPosition) * 0.5f;
            _twoStartDirection = second.startPosition - first.startPosition;
            _twoStartDistance = Mathf.Max(0.001f, _twoStartDirection.magnitude);
            _twoStartDirection /= _twoStartDistance;
            _twoStartPanelPosition = transform.position;
            _twoStartPanelRotation = transform.rotation;
            _twoStartScale = transform.localScale.x;
        }

        private void ApplyTwoHandTransform()
        {
            var states = new List<GrabState>(_grabs.Values);
            var first = states[0];
            var second = states[1];
            var direction = second.startPosition - first.startPosition;
            var distance = Mathf.Max(0.001f, direction.magnitude);
            direction /= distance;
            transform.position = _twoStartPanelPosition + ((first.startPosition + second.startPosition) * 0.5f - _twoStartMidpoint);
            transform.rotation = Quaternion.FromToRotation(_twoStartDirection, direction) * _twoStartPanelRotation;
            SetUniformScale(_twoStartScale * distance / _twoStartDistance);
        }

        private void ProcessController(string id, XRRayInteractor ray, InputAction action)
        {
            if (ray == null || action == null || grabCollider == null) return;
            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            var grabPose = new Pose(origin.position, origin.rotation);
            if (action.WasPressedThisFrame() && TryRaycastGrab(ray))
            {
                if (TryBeginGrab(id, grabPose.position, grabPose.rotation))
                {
                    _singleLastPosition = grabPose.position;
                    _singleLastRotation = grabPose.rotation;
                }
            }
            if (_grabs.ContainsKey(id) && action.IsPressed()) UpdateGrab(id, grabPose.position, grabPose.rotation);
            if (action.WasReleasedThisFrame()) EndGrab(id);
        }

        private bool TryRaycastGrab(XRRayInteractor ray)
        {
            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            return grabCollider.Raycast(new Ray(origin.position, origin.forward), out _, ray.maxRaycastDistance);
        }

        private void UpdateFrameHover()
        {
            if (grabCollider == null || IsScreenTouchActive)
            {
                IsFrameHover = false;
                ApplyFrameFeedback();
                return;
            }
            IsFrameHover = (leftRay != null && TryRaycastGrab(leftRay)) || (rightRay != null && TryRaycastGrab(rightRay));
            ApplyFrameFeedback();
        }

        private void ApplyFrameFeedback()
        {
            if (frameRenderer == null || _frameProperties == null || frameRenderer.sharedMaterial == null) return;
            if (!frameRenderer.sharedMaterial.HasProperty("_Color")) return;
            var color = IsGrabActive ? new Color(0.25f, 0.85f, 1f, 1f) : IsFrameHover ? new Color(0.7f, 0.9f, 1f, 1f) : _frameBaseColor;
            frameRenderer.GetPropertyBlock(_frameProperties);
            _frameProperties.SetColor("_Color", color);
            frameRenderer.SetPropertyBlock(_frameProperties);
        }

        public void SetUniformScale(float value)
        {
            var scale = Mathf.Clamp(value, minScale, maxScale);
            transform.localScale = Vector3.one * scale;
        }

        private void OnDisable() => ClearInteractionState();
    }
}
