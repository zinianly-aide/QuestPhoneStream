using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Build.Reporting;
using HefuVR;

namespace HefuVR.Editor
{
    public static class HefuAndroidBuilder
    {
        const string input="Assets/Hefu/Scenes/Hefu_DesignLab.unity";
        const string output="Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity";
        static string ApkFile => "Builds/HefuVR-AndroidPhone-v"+PlayerSettings.bundleVersion+".apk";
        [MenuItem("Hefu Android/Prepare phone scene")]
        public static void Prepare()
        {
            var scene=EditorSceneManager.OpenScene(input,OpenSceneMode.Single);
            GameObject rig=GameObject.Find("XR_Rig_WorldOrigin");
            if(rig)UnityEngine.Object.DestroyImmediate(rig);
            GameObject house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
            if(!house)throw new Exception("CAD architecture missing");
            // DesignLab prototype only: a tall coloured swatch was left next
            // to the dining table in shipping scenes. Never delete any CAD.
            var swatch=GameObject.Find("DECOR_Accent_Color_Swatch");
            if(swatch)UnityEngine.Object.DestroyImmediate(swatch);
            var rug=GameObject.Find("DECOR_Preview_Rug");
            if(rug){
                // Place the sample rug in the living room, not under dining.
                var sofa=GameObject.Find("Move_Sofa");
                var coffee=GameObject.Find("Move_Coffee_Table");
                if(!sofa||!coffee)throw new Exception("HEFU_REALISM_FURNITURE_ANCHORS_MISSING");
                rug.transform.position=new Vector3((sofa.transform.position.x+coffee.transform.position.x)*.5f,
                    .012f,(sofa.transform.position.z+coffee.transform.position.z)*.5f);
                rug.transform.localScale=new Vector3(2.45f,.018f,2.84f);
                var renderer=rug.GetComponent<Renderer>();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Hefu/Textures/PhotoReference/PhotoRug.png");
                if(!renderer||!texture)throw new Exception("HEFU_REALISM_RUG_TEXTURE_MISSING");
                const string matPath="Assets/Hefu/Generated/FurnitureDetailAssets/RenderQualityRug.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!material){System.IO.Directory.CreateDirectory("Assets/Hefu/Generated/FurnitureDetailAssets");
                    material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,matPath);}
                material.mainTexture=texture;
                material.mainTextureScale=new Vector2(3.5f,4f);
                material.color=new Color(.95f,.94f,.91f);
                material.SetFloat("_Glossiness",.13f);
                material.SetFloat("_Metallic",0f);
                EditorUtility.SetDirty(material);
                renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=true;
            }
            var daylight=GameObject.Find("BakedLike_Sun_VisualPreview");
            if(daylight){
                var light=daylight.GetComponent<Light>();
                if(light){light.color=new Color(1f,.962f,.912f);light.intensity=.94f;
                    light.shadows=LightShadows.Soft;light.shadowStrength=.56f;light.shadowBias=.045f;
                    light.shadowNormalBias=.35f;}
            }
            QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowDistance=9f;
            QualitySettings.shadowResolution=ShadowResolution.Medium;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.72f,.715f,.695f);
            Debug.Log("HEFU_REALISM_SCENE_CLEAN_PASS swatchRemoved=true wovenRugMoved=true softShadows=true");
            GameObject go=new GameObject("Android_Touch_Orbit_Camera");
            go.tag="MainCamera";
            Camera cam=go.AddComponent<Camera>();
            cam.nearClipPlane=.04f;cam.farClipPlane=120f;cam.fieldOfView=65f;
            go.AddComponent<AudioListener>();
            var viewer=go.AddComponent<HefuMobileViewer>();
            viewer.cam=cam;viewer.house=house.transform;
            viewer.photo=go.AddComponent<HefuPhotoComparison>();
            viewer.photo.captureCamera=cam;
            HefuNorthEntrancePhotoLayout.Align(house.transform);
            viewer.entryDoor=HefuEntryDoorBuilder.InsertIntoMobile(house.transform);
            viewer.interiorDoors=HefuInteriorDoorBuilder.InsertAll(house.transform,out viewer.balconySlidingDoor);
            HefuBathroomFinishBuilder.Insert(house.transform);
            HefuBalconyArchBuilder.Insert(house.transform);
            viewer.diningSideboard=HefuDiningSideboardBuilder.InsertIntoMobile(house.transform);
            HefuInRoomPlacement.PlaceAll(house.transform);
            HefuFurnitureDetailBuilder.Apply(house.transform);
            HefuP1FurnitureFinishBuilder.Apply(house.transform);
            HefuPhotoMaterialFinish.Apply(house.transform);
            HefuCabinetPBRFinish.Apply(house.transform);
            HefuSceneAtmosphereBuilder.Apply(house.transform);
            HefuM1StructureFinish.Apply(house.transform);
            HefuP2InteriorLightBuilder.Apply(house.transform);
            HefuDaylightExposureBuilder.Apply(house.transform);
            HefuWarmReferenceStyleBuilder.Apply(house.transform);
            HefuPracticalLampBuilder.Apply(house.transform,cam);
            HefuM2MaterialFinish.Apply(house.transform);
            HefuM2SoftDecorBuilder.Apply(house.transform,viewer);
            HefuPlasterWallBuilder.Apply(house.transform);
            HefuArchitecturalTrimBuilder.Apply(house.transform);
            HefuBakedRoomReflections.Apply(house.transform);
            HefuCadCeilingBuilder.Apply(house.transform, cam);
            // Validate B2.3 against the actual phone hierarchy, not the Quest's
            // separately exported CAD bundle. Never save the generated art into
            // the base phone scene; runtime toggles it non-destructively.
            int baseRenderers = house.GetComponentsInChildren<Renderer>(true).Length;
            var birthdaySmoke = HefuBirthdayScene.Build(house.transform);
            if (birthdaySmoke.AnchorCount != 5 || birthdaySmoke.DecorationCount < 115)
                throw new Exception("HEFU_PHONE_BIRTHDAY_QA_FAILED: wrong anchors/decor count");
            int fineBouquets=0, fineStrings=0, photoFrames=0;
            foreach(var mesh in house.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mesh.name.Contains("Birthday_Detail_RoseBouquet"))fineBouquets++;
                if(mesh.name.Contains("Birthday_Detail_BalloonSatinStrings"))fineStrings++;
                if(mesh.name.Contains("Birthday_Detail_PhotoFrameMoulding"))photoFrames++;
            }
            if(fineBouquets!=5 || fineStrings!=3 || photoFrames!=3)
                throw new Exception("HEFU_B26_DETAIL_QA_FAILED bouquet="+fineBouquets+
                    " strings="+fineStrings+" frames="+photoFrames);
            Debug.Log("HEFU_PHONE_BIRTHDAY_QA_PASS anchors="+birthdaySmoke.AnchorCount+
                " decor="+birthdaySmoke.DecorationCount);
            birthdaySmoke.ClearDecorations();
            UnityEngine.Object.DestroyImmediate(birthdaySmoke.gameObject);
            if (house.GetComponentsInChildren<Renderer>(true).Length != baseRenderers)
                throw new Exception("HEFU_PHONE_BIRTHDAY_QA_FAILED: generated decor leaked into base phone scene");
            Debug.Log("HEFU_PHONE_BIRTHDAY_QA_CLEAN_PASS renderers="+baseRenderers);
            viewer.decor=UnityEngine.Object.FindObjectOfType<HefuDecorSwitcher>();
            if(!viewer.decor)throw new Exception("Decoration style controller missing");
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,output))throw new Exception("Mobile scene save failed");
            Debug.Log("HEFU_MOBILE_SCENE_READY renderers="+house.GetComponentsInChildren<Renderer>().Length+
                " movable="+house.GetComponentsInChildren<HefuMovable>().Length);
            HefuMakerGalleryBuilder.Prepare();
            EditorBuildSettings.scenes=new[]{
                new EditorBuildSettingsScene(output,true),
                new EditorBuildSettingsScene(HefuMakerGalleryBuilder.ScenePath,true)
            };
        }
        [MenuItem("Hefu Android/Build phone APK")]
        public static void BuildAPK()
        {
            Prepare();
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android &&
               !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))
                throw new Exception("Android build target switch failed");
            PlayerSettings.productName="Hefu Home Design";
            // Release version comes from the Unity ProjectSettings > Player.
            if(!Regex.IsMatch(PlayerSettings.bundleVersion, @"^\d+\.\d+\.\d+$"))
                throw new Exception("Invalid Android release version: "+PlayerSettings.bundleVersion);
            string apk=ApkFile;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.hefu.homedesign");
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            // Android 7.0 (API 24) device also supported by Unity 2022 / GLES3.
            PlayerSettings.Android.minSdkVersion=(AndroidSdkVersions)24;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            AndroidExternalToolsSettings.jdkRootPath="/Volumes/ssd/HefuVR-Tools/java/jdk-11.0.32.1+1/Contents/Home";
            AndroidExternalToolsSettings.sdkRootPath=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"/Library/Android/sdk";
            AndroidExternalToolsSettings.ndkRootPath=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"/Library/Android/sdk/ndk/23.1.7779620";
            // Unity as a Library exports Gradle; standalone Hefu Home must reset that
            // global editor flag or BuildPlayer creates a directory instead of APK.
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            Directory.CreateDirectory("Builds");AssetDatabase.SaveAssets();
            var options=new BuildPlayerOptions {scenes=new[]{output,HefuMakerGalleryBuilder.ScenePath},locationPathName=apk,target=BuildTarget.Android,options=BuildOptions.Development};
            var report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("HEFU_PHONE_APK_FAILED: "+report.summary.result);
            Debug.Log("HEFU_PHONE_APK_OK "+Path.GetFullPath(apk)+" bytes="+new FileInfo(apk).Length);
        }
        [MenuItem("Hefu Android/Build and auto-publish APK to GitHub")]
        public static void BuildAndPublishAPK()
        {
            // Only a successful build can be sent to the publisher.
            BuildAPK();
            string artifact=Path.GetFullPath(ApkFile);
            string script=Path.GetFullPath("Scripts/publish_apk.sh");
            if(!File.Exists(script))throw new Exception("Missing GitHub publisher: "+script);
            var start=new System.Diagnostics.ProcessStartInfo("/bin/bash")
            {
                Arguments="\""+script+"\" \""+artifact+"\"",
                WorkingDirectory=Path.GetFullPath("."),
                UseShellExecute=false
            };
            using(var process=System.Diagnostics.Process.Start(start))
            {
                if(process==null)throw new Exception("Failed to start APK publisher");
                if(!process.WaitForExit(240000))
                {
                    process.Kill();
                    throw new Exception("APK publishing timed out; inspect the draft and Actions logs");
                }
                if(process.ExitCode!=0)throw new Exception("HEFU_GITHUB_PUBLISH_FAILED exitCode="+process.ExitCode);
            }
            Debug.Log("HEFU_GITHUB_AUTO_PUBLISH_QUEUED "+artifact);
        }

    }
}
