using UnityEngine;
using QuestPhoneStream.Interaction;

namespace QuestPhoneStream
{
    /// <summary>Flat-content aspect/orientation and projection gate; all manipulation belongs to the shared shell.</summary>
    public sealed class FlatMediaPanelController : MonoBehaviour
    {
        public Camera xrCamera;
        public Renderer panelRenderer;
        public float minScale = .5f, maxScale = 2.5f, scaleStep = .1f;
        public bool IsFlatActive { get; private set; }
        public float ScaleMultiplier => Shell != null ? Shell.Manipulator.transform.localScale.x : 1f;
        public float AspectRatio => _aspectRatio;
        public SpatialPanelShell Shell { get; private set; }
        private Collider _panelCollider;
        private float _aspectRatio = 16f / 9f, _baseLongSide = 1.6f;
        private bool _rotated;
        private Transform _contentParent;
        public void Initialize(Camera camera, Renderer targetRenderer)
        {
            xrCamera = camera;
            panelRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            if (Shell == null)
            {
                var root = new GameObject("FlatMediaSpatialPanelRoot");
                _contentParent = transform.parent;
                root.transform.SetParent(transform.parent, false);
                root.transform.SetPositionAndRotation(transform.position, transform.rotation);
                _baseLongSide = Mathf.Max(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));
                if (_baseLongSide < .01f) _baseLongSide = 1.6f;
                transform.SetParent(root.transform, true);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                _panelCollider = GetComponent<Collider>() ?? gameObject.AddComponent<BoxCollider>();
                Shell = root.AddComponent<SpatialPanelShell>();
                Shell.Initialize(transform, _panelCollider, camera, new MediaSurfaceInput());
                Shell.Manipulator.minScale = minScale; Shell.Manipulator.maxScale = maxScale;
            }
            ApplyAspectScale();
            SetProjection(ProjectionMode.Flat);
        }
        public void ConfigureBackend(object dependencies) => Shell?.ConfigureBackend(dependencies);
        public void SetVideoDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            _aspectRatio = width / (float)height; ApplyAspectScale();
        }
        public void SetProjection(ProjectionMode projection)
        {
            IsFlatActive = projection == ProjectionMode.Flat;
            // Keep dedicated VR rendering on its original hierarchy; only flat content enters the shell.
            if (Shell != null && IsFlatActive && transform.parent != Shell.transform)
            {
                transform.SetParent(Shell.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.Euler(0, 0, _rotated ? 90 : 0);
                ApplyAspectScale();
            }
            else if (Shell != null && !IsFlatActive && transform.parent == Shell.transform)
                transform.SetParent(_contentParent, true);
            if (_panelCollider != null) _panelCollider.enabled = IsFlatActive;
            if (panelRenderer != null) panelRenderer.enabled = IsFlatActive;
            Shell?.SetInteractive(IsFlatActive && isActiveAndEnabled);
        }
        public void ScaleUp() { if (IsFlatActive) Shell?.Manipulator.SetUniformScale(ScaleMultiplier + scaleStep); }
        public void ScaleDown() { if (IsFlatActive) Shell?.Manipulator.SetUniformScale(ScaleMultiplier - scaleStep); }
        public void RotateOrientation()
        {
            if (!IsFlatActive) return;
            _rotated = !_rotated;
            transform.localRotation = Quaternion.Euler(0, 0, _rotated ? 90 : 0);
            Shell?.RefreshFrame();
        }
        public void ResetPose()
        {
            if (!IsFlatActive || Shell == null) return;
            Shell.Manipulator.ResetPose(xrCamera);
        }
        private void ApplyAspectScale()
        {
            transform.localScale = _aspectRatio >= 1
                ? new Vector3(_baseLongSide, _baseLongSide / _aspectRatio, 1)
                : new Vector3(_baseLongSide * _aspectRatio, _baseLongSide, 1);
            Shell?.RefreshFrame();
        }
        private void OnEnable() => Shell?.SetInteractive(IsFlatActive);
        private void OnDisable() => Shell?.SetInteractive(false);
    }
}
