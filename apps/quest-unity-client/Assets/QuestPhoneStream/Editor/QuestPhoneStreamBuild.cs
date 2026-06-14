using System.IO;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using UnityEditor.XR.OpenXR;

namespace QuestPhoneStream.Editor
{
    public static class QuestPhoneStreamBuild
    {
        private const string SceneDir = "Assets/QuestPhoneStream/Scenes";
        private const string ScenePath = SceneDir + "/QuestPhoneStreamMvp.unity";
        private const string MaterialDir = "Assets/QuestPhoneStream/Materials";
        private const string PanelMaterialPath = MaterialDir + "/PhonePanel.mat";

        [MenuItem("QuestPhoneStream/Create MVP Scene")]
        public static void CreateMvpScene()
        {
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(MaterialDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Fallback Ray Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, -2.2f);
            camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(45f, 25f, 0f);

            var material = AssetDatabase.LoadAssetAtPath<Material>(PanelMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                material.color = Color.black;
                AssetDatabase.CreateAsset(material, PanelMaterialPath);
            }

            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "PhonePanel";
            panel.transform.position = new Vector3(0f, 1.45f, 1.2f);
            panel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            panel.transform.localScale = new Vector3(0.72f, 1.6f, 1f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (panel.GetComponent<Collider>() == null)
            {
                panel.AddComponent<MeshCollider>();
            }

            var app = new GameObject("QuestPhoneStreamApp");
            var signaling = app.AddComponent<QuestSignalingClient>();
            var control = app.AddComponent<ControlChannel>();
            var receiver = app.AddComponent<QuestWebRtcReceiver>();
            var mapper = panel.AddComponent<PanelInputMapper>();
            panel.AddComponent<PhonePanelController>();

            control.signaling = signaling;
            receiver.signaling = signaling;
            receiver.controlChannel = control;
            receiver.targetMaterial = material;
            mapper.rayCamera = camera;
            mapper.panelCollider = panel.GetComponent<Collider>();
            mapper.controlChannel = control;

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = scenes;
            AssetDatabase.SaveAssets();
        }

        public static void BuildAndroid()
        {
            if (!File.Exists(ScenePath))
            {
                CreateMvpScene();
            }
            else
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            }

            const string output = "Builds/QuestPhoneStream.apk";
            Directory.CreateDirectory("Builds");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            PlayerSettings.productName = "QuestPhoneStream";
            PlayerSettings.companyName = "QuestPhoneStream";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.questphonestream.quest");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            if (PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation)
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            else
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
#if UNITY_2021_3_OR_NEWER
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android, MergeDefines(
                PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android),
                "USE_INPUT_SYSTEM_POSE_CONTROL",
                "USE_STICK_CONTROL_THUMBSTICKS"));
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, MergeDefines(
                PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android),
                "USE_INPUT_SYSTEM_POSE_CONTROL",
                "USE_STICK_CONTROL_THUMBSTICKS"));
#endif
            EnsureAndroidExternalTools();
            EnsureAndroidOpenXrLoader();
            EnsureAndroidOpenXrFeatures();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"QuestPhoneStream Android build failed: {report.summary.result}");
            }

            Debug.Log($"QuestPhoneStream Android build succeeded: {output}");
        }

        private static void EnsureAndroidExternalTools()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var bundledJdk11 = Path.Combine(projectRoot, ".tools/jdk11/temurin-11.jdk/Contents/Home");
            var sdkRoot = FirstExistingDirectory(
                Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                Environment.GetEnvironmentVariable("ANDROID_HOME"),
                "/Users/anshi/Library/Android/sdk");
            var ndkRoot = FirstExistingDirectory(
                Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT"),
                Path.Combine(sdkRoot ?? string.Empty, "ndk/23.1.7779620"),
                Path.Combine(sdkRoot ?? string.Empty, "ndk/30.0.14904198"));
            var jdkRoot = FirstExistingDirectory(
                Environment.GetEnvironmentVariable("UNITY_JAVA_HOME"),
                bundledJdk11,
                "/Applications/Android Studio.app/Contents/jbr/Contents/Home",
                Environment.GetEnvironmentVariable("JAVA_HOME"),
                "/Library/Java/JavaVirtualMachines/temurin-21.jdk/Contents/Home");
            var gradlePath = FirstExistingDirectory(
                Environment.GetEnvironmentVariable("UNITY_GRADLE_PATH"),
                "/Users/anshi/ssd/Applications/Unity/PlaybackEngines/AndroidPlayer/Tools/gradle");

            SetAndroidToolPath("AndroidSDKRoot", sdkRoot);
            SetAndroidToolPath("AndroidNDKRoot", ndkRoot);
            SetAndroidToolPath("AndroidJavaRoot", jdkRoot);
            SetAndroidToolPath("AndroidGradleRoot", gradlePath);
            SetAndroidToolPath("JdkPath", jdkRoot);
            ApplyAndroidExternalToolsViaReflection(sdkRoot, ndkRoot, jdkRoot, gradlePath);

            SetEnvironmentPath("ANDROID_SDK_ROOT", sdkRoot);
            SetEnvironmentPath("ANDROID_HOME", sdkRoot);
            SetEnvironmentPath("ANDROID_NDK_ROOT", ndkRoot);
            SetEnvironmentPath("JAVA_HOME", jdkRoot);
            SetEnvironmentPath("JDK_HOME", jdkRoot);
            Environment.SetEnvironmentVariable("SKIP_JDK_VERSION_CHECK", "1");

            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetBool("NdkUseEmbedded", false);
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            EditorPrefs.SetBool("GradleUseEmbedded", false);
        }

        private static void ApplyAndroidExternalToolsViaReflection(string sdkRoot, string ndkRoot, string jdkRoot, string gradlePath)
        {
            var toolsType = FindEditorType("UnityEditor.Android.AndroidExternalToolsSettings");
            if (toolsType == null)
            {
                Debug.LogWarning("[QuestPhoneStreamBuild] UnityEditor.Android.AndroidExternalToolsSettings type not found.");
                return;
            }

            Debug.Log($"[QuestPhoneStreamBuild] Found {toolsType.FullName} in {toolsType.Assembly.GetName().Name}");

            TrySetStaticOrInstanceProperty(toolsType, "sdkRootPath", sdkRoot);
            TrySetStaticOrInstanceProperty(toolsType, "ndkRootPath", ndkRoot);
            TrySetStaticOrInstanceProperty(toolsType, "jdkRootPath", jdkRoot);
            TrySetStaticOrInstanceProperty(toolsType, "gradlePath", gradlePath);

            TrySetStaticOrInstanceField(toolsType, "m_AndroidSdkPath", sdkRoot);
            TrySetStaticOrInstanceField(toolsType, "m_AndroidNdkPath", ndkRoot);
            TrySetStaticOrInstanceField(toolsType, "m_JdkPath", jdkRoot);
            TrySetStaticOrInstanceField(toolsType, "m_GradlePath", gradlePath);
        }

        private static Type FindEditorType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static object GetAndroidExternalToolsInstance(Type toolsType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var instanceProperty = toolsType.GetProperty("instance", flags) ?? toolsType.GetProperty("Instance", flags);
            if (instanceProperty != null)
            {
                return instanceProperty.GetValue(null);
            }

            var getInstance = toolsType.GetMethod("GetInstance", flags);
            return getInstance != null ? getInstance.Invoke(null, null) : null;
        }

        private static void TrySetStaticOrInstanceProperty(Type type, string propertyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var property = type.GetProperty(propertyName, flags);
            if (property == null || !property.CanWrite)
            {
                Debug.Log($"[QuestPhoneStreamBuild] Reflection property not writable or missing: {propertyName}");
                return;
            }

            var target = property.GetGetMethod(true)?.IsStatic == true || property.GetSetMethod(true)?.IsStatic == true
                ? null
                : GetAndroidExternalToolsInstance(type);
            try
            {
                property.SetValue(target, value);
                Debug.Log($"[QuestPhoneStreamBuild] Reflection set {propertyName}={value}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QuestPhoneStreamBuild] Reflection could not set {propertyName}: {ex.GetBaseException().Message}");
            }
        }

        private static void TrySetStaticOrInstanceField(Type type, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var field = type.GetField(fieldName, flags);
            if (field == null)
            {
                Debug.Log($"[QuestPhoneStreamBuild] Reflection field missing: {fieldName}");
                return;
            }

            var target = field.IsStatic ? null : GetAndroidExternalToolsInstance(type);
            try
            {
                field.SetValue(target, value);
                Debug.Log($"[QuestPhoneStreamBuild] Reflection set {fieldName}={value}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QuestPhoneStreamBuild] Reflection could not set {fieldName}: {ex.GetBaseException().Message}");
            }
        }

        private static void SetAndroidToolPath(string editorPrefKey, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning($"[QuestPhoneStreamBuild] Android tool path not found for {editorPrefKey}.");
                return;
            }

            EditorPrefs.SetString(editorPrefKey, path);
            Debug.Log($"[QuestPhoneStreamBuild] {editorPrefKey}={path}");
        }

        private static void SetEnvironmentPath(string variableName, string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                Environment.SetEnvironmentVariable(variableName, path);
            }
        }

        private static string FirstExistingDirectory(params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void EnsureAndroidOpenXrLoader()
        {
            var generalSettings = LoadOrCreateXrBuildTargetSettings();
            if (!generalSettings.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                generalSettings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            }
            if (!generalSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                generalSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            var manager = generalSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (manager == null)
            {
                throw new Exception("Unable to create XR manager settings for Android.");
            }

            const string openXrLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
            if (!XRPackageMetadataStore.AssignLoader(manager, openXrLoader, BuildTargetGroup.Android))
            {
                Debug.Log("OpenXR loader was already assigned or could not be assigned automatically.");
            }

            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureAndroidOpenXrFeatures()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                throw new Exception("Unable to load OpenXR settings for Android.");
            }

            // Render mode: Single Pass Instanced (required for Meta Quest)
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            EnableFeature<MetaQuestFeature>(settings, "Meta Quest Support");
            EnableFeature<OculusTouchControllerProfile>(settings, "Oculus Touch Controller Profile");

            // Configure MetaQuestFeature target devices (required to pass "No Quest target devices selected" validation)
            // AddTargetDevice is safe to call repeatedly — it skips duplicates internally
            var metaQuestFeature = settings.GetFeature<MetaQuestFeature>();
            if (metaQuestFeature != null && metaQuestFeature.enabled)
            {
                metaQuestFeature.AddTargetDevice("quest2", "Quest 2", true);
                metaQuestFeature.AddTargetDevice("eureka", "Quest 3", true);
                EditorUtility.SetDirty(metaQuestFeature);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // Final pass: auto-fix any remaining validation issues for CI build
            ResolveRemainingValidationIssues();
        }

        /// <summary>
        /// Runs automatic fix-its for any remaining OpenXR project validation issues
        /// to ensure batch/CI builds don't fail on non-critical validations.
        /// </summary>
        private static void ResolveRemainingValidationIssues()
        {
            var failures = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(failures, BuildTargetGroup.Android);

            if (failures.Count == 0)
            {
                Debug.Log("[QuestPhoneStreamBuild] All OpenXR validation checks pass.");
                return;
            }

            foreach (var rule in failures)
            {
                if (rule.error)
                {
                    if (rule.fixItAutomatic && rule.fixIt != null)
                    {
                        Debug.Log($"[QuestPhoneStreamBuild] Auto-fixing validation error: {rule.message}");
                        rule.fixIt();
                    }
                    else
                    {
                        Debug.LogWarning($"[QuestPhoneStreamBuild] Non-automatic validation error remains: {rule.message}");
                    }
                }
                else
                {
                    Debug.Log($"[QuestPhoneStreamBuild] Validation warning (non-blocking): {rule.message}");
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnableFeature<TFeature>(OpenXRSettings settings, string featureName)
            where TFeature : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var feature = settings.GetFeature<TFeature>();
            if (feature == null)
            {
                Debug.LogWarning($"OpenXR feature not found: {featureName}");
                return;
            }

            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            Debug.Log($"Enabled OpenXR feature: {featureName}");
        }

        private static string MergeDefines(string currentDefines, params string[] requiredDefines)
        {
            var defines = currentDefines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var define in requiredDefines)
            {
                if (Array.IndexOf(defines, define) < 0)
                {
                    currentDefines = string.IsNullOrWhiteSpace(currentDefines)
                        ? define
                        : currentDefines + ";" + define;
                }
            }

            return currentDefines;
        }

        private static XRGeneralSettingsPerBuildTarget LoadOrCreateXrBuildTargetSettings()
        {
            var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            }

            Directory.CreateDirectory("Assets/XR");
            var settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(settings, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
