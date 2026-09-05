using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>
    /// Quest-side projection renderer. It consumes the same RenderTexture as the
    /// VideoPlayer and only changes the surface and sampling mode.
    /// </summary>
    public sealed class VrMediaRenderer : MonoBehaviour
    {
        public Camera xrCamera;
        public FlatMediaRenderer flatRenderer;

        private GameObject _sphere;
        private Renderer _sphereRenderer;
        private Material _vrMaterial;
        private bool _vrVisible;

        public bool IsVrVisible => _vrVisible && _sphere != null && _sphere.activeSelf;

        public void Initialize(Camera camera, FlatMediaRenderer flat)
        {
            xrCamera = camera;
            flatRenderer = flat;
        }

        public void Apply(RenderTexture texture, ProjectionMode projection, int fov, StereoMode stereo, EyeOrder eyeOrder)
        {
            if (texture == null) return;
            if (projection == ProjectionMode.Flat)
            {
                HideVr();
                flatRenderer?.SetTexture(texture);
                if (flatRenderer?.targetRenderer != null) flatRenderer.targetRenderer.enabled = true;
                return;
            }

            EnsureSphere();
            if (_sphereRenderer == null || _vrMaterial == null) return;
            if (flatRenderer?.targetRenderer != null) flatRenderer.targetRenderer.enabled = false;
            _sphere.transform.position = xrCamera != null ? xrCamera.transform.position : transform.position;
            var facing = xrCamera == null ? Vector3.forward : Vector3.ProjectOnPlane(xrCamera.transform.forward, Vector3.up);
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            _sphere.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            _sphere.transform.localScale = Vector3.one * 20f;
            _vrMaterial.SetTexture("_MainTex", texture);
            _vrMaterial.SetFloat("_Fov", fov == 180 ? 180f : 360f);
            _vrMaterial.SetFloat("_Stereo", stereo == StereoMode.Sbs ? 1f : 0f);
            _vrMaterial.SetFloat("_EyeOrder", eyeOrder == EyeOrder.Rl ? 1f : 0f);
            _sphere.SetActive(true);
            _vrVisible = true;
        }

        public void HideVr()
        {
            if (_sphere != null) _sphere.SetActive(false);
            if (_vrMaterial != null) _vrMaterial.SetTexture("_MainTex", null);
            _vrVisible = false;
        }

        public void Release() => HideVr();

        private void EnsureSphere()
        {
            if (_sphere != null) return;
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "VrMediaSphere";
            // The MediaPanel parent is a portrait Quad with non-uniform scale;
            // keep the VR sphere under the receiver parent so it remains round.
            _sphere.transform.SetParent(transform.parent, true);
            var collider = _sphere.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            _sphereRenderer = _sphere.GetComponent<Renderer>();
            var shader = Shader.Find("QuestPhoneStream/VRMediaStereo");
            if (shader == null)
            {
                Debug.LogError("[QuestPhoneStream] VR media shader is unavailable");
                _sphere.SetActive(false);
                return;
            }
            _vrMaterial = new Material(shader) { name = "QuestPhoneStream VR Media (Runtime)" };
            _sphereRenderer.sharedMaterial = _vrMaterial;
            _sphere.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_sphere != null) Destroy(_sphere);
            if (_vrMaterial != null) Destroy(_vrMaterial);
        }
    }
}
