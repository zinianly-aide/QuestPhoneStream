using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public sealed class SpatialPanelShell : MonoBehaviour
    {
        public SpatialPanelManipulator Manipulator { get; private set; }
        public SpatialPanelInteractionRouter Router { get; private set; }
        public SurfaceInputController Input { get; private set; }
        public Transform Surface { get; private set; }
        private InteractionBackendManager _manager;
        private InteractionBackendContext _context;
        private Transform _handle;
        private bool _interactive = true;

        public void Initialize(Transform surface, Collider collider, Camera camera, IPanelSurfaceInputHandler handler)
        {
            Surface = surface;
            Manipulator = GetComponent<SpatialPanelManipulator>() ?? gameObject.AddComponent<SpatialPanelManipulator>();
            Input = GetComponent<SurfaceInputController>() ?? gameObject.AddComponent<SurfaceInputController>();
            Input.manipulator = Manipulator; Input.SetHandler(handler);
            Router = GetComponent<SpatialPanelInteractionRouter>() ?? gameObject.AddComponent<SpatialPanelInteractionRouter>();
            _manager = GetComponent<InteractionBackendManager>() ?? gameObject.AddComponent<InteractionBackendManager>();
            var frame = transform.Find("Frame");
            if (frame == null) { frame = new GameObject("Frame").transform; frame.SetParent(transform, false); }
            _handle = frame.Find("GrabHandle");
            if (_handle == null)
            {
                _handle = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                _handle.name = "GrabHandle"; _handle.SetParent(frame, false);
            }
            _handle.localScale = new Vector3(.9f, .08f, .05f);
            // Generous grab volume so a controller ray or bare-hand pinch near the
            // bar still hits, independent of the narrow visual cube.
            var hit = _handle.GetComponent<BoxCollider>();
            hit.size = new Vector3(1.25f, 3f, 4f);
            Manipulator.frameRenderer = _handle.GetComponent<Renderer>();
            Router.screenCollider = collider; Router.grabCollider = hit;
            Router.touchController = Input; Router.manipulator = Manipulator; Router.backendManager = _manager;
            _context = new InteractionBackendContext { panelRoot = gameObject, screenCollider = collider,
                grabCollider = hit, camera = camera };
            RefreshFrame();
        }
        public void ConfigureBackend(object dependencies)
        {
            if (_context == null) return;
            _context.runtimeDependencies = dependencies;
            RestartBackend();
        }
        public void SetInteractive(bool value)
        {
            _interactive = value;
            if (_handle != null) _handle.parent.gameObject.SetActive(value);
            if (!value) _manager?.ShutdownBackend();
            else RestartBackend();
        }
        private void RestartBackend()
        {
            if (!_interactive || !isActiveAndEnabled || _context?.runtimeDependencies == null || _manager.ActiveBackend != null) return;
            if (_manager.InitializeBackend(_context)) Router.Attach(_manager.ActiveBackend);
        }
        public void RefreshFrame()
        {
            if (_handle == null || Surface == null) return;
            var horizontal = Surface.localRotation * new Vector3(Surface.localScale.x, 0, 0);
            var vertical = Surface.localRotation * new Vector3(0, Surface.localScale.y, 0);
            var halfHeight = (Mathf.Abs(horizontal.y) + Mathf.Abs(vertical.y)) * .5f;
            // User-facing side is -Z (see XriPointerSource poke normal). +Z puts the
            // handle behind the panel where the ray can never hit it from the front.
            _handle.localPosition = new Vector3(Surface.localPosition.x, Surface.localPosition.y - halfHeight - .06f, -.04f);
        }
        private void OnEnable() => RestartBackend();
        private void OnDisable() => _manager?.ShutdownBackend();
    }
}
