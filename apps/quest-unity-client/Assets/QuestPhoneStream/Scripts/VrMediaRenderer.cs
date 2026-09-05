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
        public Material vrMaterialTemplate;

        private GameObject _sphere;
        private Renderer _sphereRenderer;
        private Material _vrMaterial;
        private bool _vrVisible;

        public bool IsVrVisible => _vrVisible && _sphere != null && _sphere.activeSelf;

        public void Initialize(Camera camera, FlatMediaRenderer flat, Material materialTemplate = null)
        {
            xrCamera = camera;
            flatRenderer = flat;
            if (materialTemplate != null) vrMaterialTemplate = materialTemplate;
        }

        public void Apply(RenderTexture texture, ProjectionMode projection, int fov, StereoMode stereo, EyeOrder eyeOrder)
        {
            if (texture == null)
            {
                Debug.LogError($"[VrMediaRenderer] Apply failed: texture=null projection={projection} fov={fov} stereo={stereo} eye={eyeOrder} shader={ShaderName()} sphereVisible={IsVrVisible}");
                return;
            }
            if (projection == ProjectionMode.Flat)
            {
                HideVr();
                flatRenderer?.SetTexture(texture);
                if (flatRenderer?.targetRenderer != null) flatRenderer.targetRenderer.enabled = true;
                LogApply(projection, fov, stereo, eyeOrder);
                return;
            }

            if (!EnsureSphere())
            {
                if (flatRenderer?.targetRenderer != null) flatRenderer.targetRenderer.enabled = false;
                Debug.LogError($"[VrMediaRenderer] Apply failed: VR shader unavailable projection={projection} fov={fov} stereo={stereo} eye={eyeOrder} shader={ShaderName()} sphereVisible={IsVrVisible}");
                return;
            }
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
            LogApply(projection, fov, stereo, eyeOrder);
        }

        public void HideVr()
        {
            if (_sphere != null) _sphere.SetActive(false);
            if (_vrMaterial != null) _vrMaterial.SetTexture("_MainTex", null);
            _vrVisible = false;
        }

        public void Release() => HideVr();

        private void LateUpdate()
        {
            if (!IsVrVisible || xrCamera == null) return;
            _sphere.transform.position = xrCamera.transform.position;
        }

        private bool EnsureSphere()
        {
            if (_sphere != null) return _sphereRenderer != null && _vrMaterial != null;
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "VrMediaSphere";
            // The MediaPanel parent is a portrait Quad with non-uniform scale;
            // keep the VR sphere under the receiver parent so it remains round.
            _sphere.transform.SetParent(transform.parent, true);
            var collider = _sphere.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            _sphereRenderer = _sphere.GetComponent<Renderer>();
            var shader = vrMaterialTemplate != null ? vrMaterialTemplate.shader : null;
            if (shader == null) shader = Shader.Find("QuestPhoneStream/VRMediaStereo");
            if (shader == null)
            {
                Debug.LogError("[VrMediaRenderer] VR media shader is unavailable. Assign VRMediaStereo.mat to vrMaterialTemplate or include QuestPhoneStream/VRMediaStereo in the build.");
                _sphere.SetActive(false);
                return false;
            }
            _vrMaterial = vrMaterialTemplate != null && vrMaterialTemplate.shader != null
                ? new Material(vrMaterialTemplate)
                : new Material(shader);
            _vrMaterial.name = "QuestPhoneStream VR Media (Runtime)";
            _sphereRenderer.sharedMaterial = _vrMaterial;
            _sphere.SetActive(false);
            return true;
        }

        private string ShaderName() => _vrMaterial?.shader?.name ?? vrMaterialTemplate?.shader?.name ?? "<unavailable>";

        private void LogApply(ProjectionMode projection, int fov, StereoMode stereo, EyeOrder eyeOrder)
        {
            Debug.Log($"[VrMediaRenderer] Apply projection={projection} fov={fov} stereo={stereo} eye={eyeOrder} shader={ShaderName()} sphereVisible={IsVrVisible}");
        }

        private void OnDestroy()
        {
            if (_sphere != null) Destroy(_sphere);
            if (_vrMaterial != null) Destroy(_vrMaterial);
        }
    }
}
