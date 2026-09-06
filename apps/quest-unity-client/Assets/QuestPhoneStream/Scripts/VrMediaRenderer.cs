using UnityEngine;

namespace QuestPhoneStream
{
    public enum VrBackend
    {
        SphereCustom,
        UnityPanoramic
    }

    /// <summary>
    /// Quest-side projection renderer. It consumes the same RenderTexture as the
    /// VideoPlayer and only changes the surface and sampling mode.
    /// </summary>
    public sealed class VrMediaRenderer : MonoBehaviour
    {
        public VrBackend vrBackend = VrBackend.UnityPanoramic;
        public Camera xrCamera;
        public FlatMediaRenderer flatRenderer;
        public Material vrMaterialTemplate;
        public Material panoramicMaterialTemplate;

        private GameObject _sphere;
        private Renderer _sphereRenderer;
        private Material _vrMaterial;
        private Material _panoramicMaterial;
        private Material _originalSkybox;
        private bool _skyboxSaved;
        private bool _vrVisible;
        private bool _panoramicVisible;
        private bool _lastSkyboxChanged;

        public bool IsVrVisible => vrBackend == VrBackend.UnityPanoramic
            ? _panoramicVisible && _panoramicMaterial != null && RenderSettings.skybox == _panoramicMaterial
            : IsSphereVisible;
        public bool IsPanoramicVisible => _panoramicVisible;
        public bool IsSphereVisible => _vrVisible && _sphere != null && _sphere.activeSelf;

        /// <summary>
        /// Converts a camera's horizontal forward direction to the Unity
        /// Skybox/Panoramic _Rotation value. The built-in shader rotates the
        /// panorama around Y before sampling, so the matching sign is the
        /// camera yaw: +90 degrees camera yaw maps to +90 degrees material yaw.
        /// </summary>
        public static float CameraYawForForward(Vector3 cameraForward)
        {
            var horizontal = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (horizontal.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
        }

        public static float PanoramicRotationForForward(Vector3 cameraForward) =>
            Mathf.Repeat(CameraYawForForward(cameraForward), 360f);

        public void Initialize(Camera camera, FlatMediaRenderer flat, Material materialTemplate = null, Material panoramicTemplate = null)
        {
            xrCamera = camera;
            flatRenderer = flat;
            if (materialTemplate != null) vrMaterialTemplate = materialTemplate;
            if (panoramicTemplate != null) panoramicMaterialTemplate = panoramicTemplate;
        }

        public void Apply(RenderTexture texture, ProjectionMode projection, int fov, StereoMode stereo, EyeOrder eyeOrder)
        {
            if (texture == null)
            {
                Debug.LogError($"[VrMediaRenderer] Apply failed: texture=null projection={projection} fov={fov} stereo={stereo} eye={eyeOrder} shader={ShaderName()} sphereVisible={IsVrVisible}");
                return;
            }
            _lastSkyboxChanged = false;
            if (projection == ProjectionMode.Flat)
            {
                HideVr();
                flatRenderer?.SetTexture(texture);
                if (flatRenderer?.targetRenderer != null) flatRenderer.targetRenderer.enabled = true;
                LogApply(projection, fov, stereo, eyeOrder);
                return;
            }

            HideSphere();
            if (vrBackend == VrBackend.UnityPanoramic)
            {
                if (!ApplyUnityPanoramic(texture, fov, stereo, eyeOrder))
                {
                    RestoreOriginalSkybox();
                    flatRenderer?.SetTexture(texture);
                    if (flatRenderer?.targetRenderer != null) flatRenderer.targetRenderer.enabled = true;
                    Debug.LogError($"[VrMediaRenderer] Apply failed: UnityPanoramic backend is unavailable; VR media was not shown. " +
                        $"projection={projection} fov={fov} stereo={stereo} eye={eyeOrder} shader={ShaderName()} sphereVisible={IsSphereVisible}");
                    LogApply(projection, fov, stereo, eyeOrder);
                    return;
                }
                if (flatRenderer?.targetRenderer != null) flatRenderer.targetRenderer.enabled = false;
                LogApply(projection, fov, stereo, eyeOrder);
                return;
            }

            RestoreOriginalSkybox();
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
            HideSphere();
            RestoreOriginalSkybox();
        }

        public void Release() => HideVr();

        public void ExitVr() => HideVr();

        private void LateUpdate()
        {
            if (vrBackend != VrBackend.SphereCustom || !IsVrVisible || xrCamera == null) return;
            _sphere.transform.position = xrCamera.transform.position;
        }

        private bool ApplyUnityPanoramic(RenderTexture texture, int fov, StereoMode stereo, EyeOrder eyeOrder)
        {
            if (!EnsurePanoramicMaterial()) return false;
            if (!_skyboxSaved)
            {
                _originalSkybox = RenderSettings.skybox;
                _skyboxSaved = true;
            }

            _panoramicMaterial.SetTexture("_MainTex", texture);
            if (_panoramicMaterial.HasProperty("_Mapping")) _panoramicMaterial.SetFloat("_Mapping", 1f); // Latitude Longitude
            if (_panoramicMaterial.HasProperty("_ImageType")) _panoramicMaterial.SetFloat("_ImageType", fov == 180 ? 1f : 0f); // 180 / 360
            if (_panoramicMaterial.HasProperty("_Layout")) _panoramicMaterial.SetFloat("_Layout", stereo == StereoMode.Sbs ? 1f : 0f); // None / Side by Side
            if (_panoramicMaterial.HasProperty("_MirrorOnBack")) _panoramicMaterial.SetFloat("_MirrorOnBack", 0f);
            if (eyeOrder == EyeOrder.Rl && stereo == StereoMode.Sbs)
            {
                Debug.LogWarning("[VrMediaRenderer] UnityPanoramic backend does not support RL eye order yet; use SphereCustom for RL.");
            }

            _lastSkyboxChanged = RenderSettings.skybox != _panoramicMaterial;
            RenderSettings.skybox = _panoramicMaterial;
            _panoramicVisible = true;
            return RecenterPanoramic();
        }

        public bool RecenterPanoramic()
        {
            if (vrBackend != VrBackend.UnityPanoramic || !_panoramicVisible ||
                _panoramicMaterial == null || !_panoramicMaterial.HasProperty("_Rotation"))
            {
                Debug.LogWarning("[VrMediaRenderer] RecenterPanoramic ignored: UnityPanoramic backend is not active.");
                return false;
            }
            if (xrCamera == null)
            {
                Debug.LogError("[VrMediaRenderer] RecenterPanoramic failed: xrCamera is unavailable.");
                return false;
            }

            var oldRotation = _panoramicMaterial.GetFloat("_Rotation");
            var newRotation = PanoramicRotationForForward(xrCamera.transform.forward);
            _panoramicMaterial.SetFloat("_Rotation", newRotation);
            Debug.Log($"[VrMediaRenderer] RecenterPanoramic oldRotation={oldRotation:F1} -> newRotation={newRotation:F1} " +
                $"cameraYaw={CameraYawForForward(xrCamera.transform.forward):F1}");
            return true;
        }

        private bool EnsurePanoramicMaterial()
        {
            if (_panoramicMaterial != null && _panoramicMaterial.shader != null &&
                _panoramicMaterial.shader.name == "Skybox/Panoramic") return true;

            var shader = panoramicMaterialTemplate != null ? panoramicMaterialTemplate.shader : null;
            if (shader == null) shader = Shader.Find("Skybox/Panoramic");
            if (shader == null || shader.name != "Skybox/Panoramic")
            {
                Debug.LogError("[VrMediaRenderer] UnityPanoramic shader is unavailable. Assign the explicit UnityPanoramic.mat asset or include Skybox/Panoramic in the build.");
                return false;
            }

            if (_panoramicMaterial != null) Destroy(_panoramicMaterial);
            _panoramicMaterial = panoramicMaterialTemplate != null
                ? new Material(panoramicMaterialTemplate)
                : new Material(shader);
            _panoramicMaterial.name = "QuestPhoneStream Unity Panoramic (Runtime)";
            return true;
        }

        private void HideSphere()
        {
            if (_sphere != null) _sphere.SetActive(false);
            if (_vrMaterial != null) _vrMaterial.SetTexture("_MainTex", null);
            _vrVisible = false;
        }

        private void RestoreOriginalSkybox()
        {
            if (!_skyboxSaved)
            {
                _panoramicVisible = false;
                return;
            }

            if (_panoramicMaterial != null && _panoramicMaterial.HasProperty("_MainTex"))
                _panoramicMaterial.SetTexture("_MainTex", null);
            RenderSettings.skybox = _originalSkybox;
            _originalSkybox = null;
            _skyboxSaved = false;
            _panoramicVisible = false;
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

        private string ShaderName() => vrBackend == VrBackend.UnityPanoramic
            ? _panoramicMaterial?.shader?.name ?? panoramicMaterialTemplate?.shader?.name ?? "<unavailable>"
            : _vrMaterial?.shader?.name ?? vrMaterialTemplate?.shader?.name ?? "<unavailable>";

        private void LogApply(ProjectionMode projection, int fov, StereoMode stereo, EyeOrder eyeOrder)
        {
            var cameraYaw = CameraYawForLog();
            var panoramicRotation = PanoramicRotationForLog();
            var imageType = PanoramicFloat("_ImageType");
            var layout = PanoramicFloat("_Layout");
            Debug.Log($"[VrMediaRenderer] Apply backend={vrBackend} projection={projection} fov={fov} stereo={stereo} eyeOrder={eyeOrder} " +
                $"cameraYaw={cameraYaw} panoramicRotation={panoramicRotation} shader={ShaderName()} " +
                $"_MainTex assigned={PanoramicTextureAssigned()} _ImageType={imageType} _Layout={layout} " +
                $"sphereVisible={IsSphereVisible} panoramicVisible={_panoramicVisible} " +
                $"skyboxChanged={_lastSkyboxChanged}");
        }

        private string CameraYawForLog() => xrCamera == null ? "<unavailable>" : CameraYawForForward(xrCamera.transform.forward).ToString("F1");

        private string PanoramicRotationForLog() => _panoramicMaterial == null || !_panoramicMaterial.HasProperty("_Rotation")
            ? "<unavailable>" : _panoramicMaterial.GetFloat("_Rotation").ToString("F1");

        private string PanoramicFloat(string property) => _panoramicMaterial == null || !_panoramicMaterial.HasProperty(property)
            ? "<unavailable>" : _panoramicMaterial.GetFloat(property).ToString("F0");

        private bool PanoramicTextureAssigned() => _panoramicMaterial != null && _panoramicMaterial.HasProperty("_MainTex") &&
            _panoramicMaterial.GetTexture("_MainTex") != null;

        private void OnDisable() => HideVr();

        private void OnDestroy()
        {
            if (_sphere != null) Destroy(_sphere);
            if (_vrMaterial != null) Destroy(_vrMaterial);
            if (_panoramicMaterial != null) Destroy(_panoramicMaterial);
        }
    }
}
