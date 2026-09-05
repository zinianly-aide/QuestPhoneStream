using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace QuestPhoneStream
{
    /// <summary>
    /// Flat-video-only pose, aspect-ratio and interaction controller for the
    /// runtime MediaPanel Quad. VR projections are gated out here so they stay
    /// owned by VrMediaRenderer.
    /// </summary>
    public sealed class FlatMediaPanelController : MonoBehaviour
    {
        public Camera xrCamera;
        public Renderer panelRenderer;
        public XRGrabInteractable grabInteractable;
        public float minScale = 0.5f;
        public float maxScale = 2.5f;
        public float scaleStep = 0.1f;

        public bool IsFlatActive { get; private set; }
        public float ScaleMultiplier => _scaleMultiplier;
        public float AspectRatio => _aspectRatio;

        private Collider _panelCollider;
        private Rigidbody _rigidbody;
        private float _aspectRatio = 16f / 9f;
        private float _baseLongSide = 1.6f;
        private float _scaleMultiplier = 1f;
        private bool _rotated;
        private bool _initialized;

        public void Initialize(Camera camera, Renderer targetRenderer)
        {
            xrCamera = camera;
            panelRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            _panelCollider = GetComponent<Collider>();
            if (!_initialized)
            {
                var localScale = transform.localScale;
                _baseLongSide = Mathf.Max(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y));
                if (_baseLongSide < 0.01f) _baseLongSide = 1.6f;
                _initialized = true;
            }
            EnsureXrGrab();
            ApplyAspectScale();
            SetProjection(ProjectionMode.Flat);
        }

        public void SetVideoDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            _aspectRatio = width / (float)height;
            ApplyAspectScale();
        }

        public void SetProjection(ProjectionMode projection)
        {
            IsFlatActive = projection == ProjectionMode.Flat;
            EnsureXrGrab();
            if (grabInteractable != null) grabInteractable.enabled = IsFlatActive;
            if (_panelCollider != null) _panelCollider.enabled = IsFlatActive;
            if (panelRenderer != null) panelRenderer.enabled = IsFlatActive;
        }

        public void ScaleUp()
        {
            if (!IsFlatActive) return;
            _scaleMultiplier = Mathf.Clamp(_scaleMultiplier + scaleStep, minScale, maxScale);
            ApplyAspectScale();
        }

        public void ScaleDown()
        {
            if (!IsFlatActive) return;
            _scaleMultiplier = Mathf.Clamp(_scaleMultiplier - scaleStep, minScale, maxScale);
            ApplyAspectScale();
        }

        public void RotateOrientation()
        {
            if (!IsFlatActive) return;
            _rotated = !_rotated;
            transform.Rotate(Vector3.forward, 90f, Space.Self);
            ApplyAspectScale();
        }

        public void ResetPose()
        {
            if (!IsFlatActive || xrCamera == null) return;
            transform.SetParent(xrCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 1.5f);
            transform.localRotation = Quaternion.Euler(0f, 0f, _rotated ? 90f : 0f);
            ApplyAspectScale();
        }

        private void ApplyAspectScale()
        {
            if (!_initialized) return;
            var longSide = _baseLongSide * _scaleMultiplier;
            float width;
            float height;
            if (_aspectRatio >= 1f)
            {
                width = longSide;
                height = longSide / _aspectRatio;
            }
            else
            {
                height = longSide;
                width = longSide * _aspectRatio;
            }
            transform.localScale = new Vector3(width, height, 1f);
        }

        private void EnsureXrGrab()
        {
            if (_panelCollider == null) _panelCollider = GetComponent<Collider>();
            if (_panelCollider == null) _panelCollider = gameObject.AddComponent<BoxCollider>();
            if (_panelCollider is MeshCollider meshCollider) meshCollider.convex = true;
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null) _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            if (grabInteractable == null) grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable == null) grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
        }
    }
}
