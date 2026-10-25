using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;

using UnityEngine;
namespace HefuVR.Editor
{
  // PRIVATE local exported Unity runtime. Never commit CAD/model bytes to QPS GitHub.
  public static class HefuUnityLibraryExport
  {
    public static void Export()
    {
      HefuAndroidBuilder.Prepare();
      BuildLibrary();
    }
    // Strict two-process mode: another Unity invocation must first run
    // HefuStaticGiBake.Bake against the generated scene. Avoid native GPU
    // lightmapper crash when Bake and Android BuildPlayer run in one process.
    public static void ExportPrepared()
    {
      HefuRoofGiQA.Validate();
      BuildLibrary();
    }
    static void BuildLibrary()
    {
      if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
          !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))
        throw new Exception("Android target unavailable");
      PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.questphonestream.agent");
      PlayerSettings.Android.minSdkVersion=(AndroidSdkVersions)29;
      PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
      PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
      PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});
      PlayerSettings.colorSpace=ColorSpace.Linear;
      PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
      AndroidExternalToolsSettings.jdkRootPath="/Volumes/ssd/HefuVR-Tools/java/jdk-11.0.32.1+1/Contents/Home";
      AndroidExternalToolsSettings.sdkRootPath=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"/Library/Android/sdk";
      AndroidExternalToolsSettings.ndkRootPath=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"/Library/Android/sdk/ndk/23.1.7779620";
      // Using Gradle export creates launcher + unityLibrary; only latter is integrated.
      var path=Path.GetFullPath("Builds/UnityLibraryExport");
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      EditorUserBuildSettings.exportAsGoogleAndroidProject=true;
      var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
         scenes=new[]{"Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",HefuMakerGalleryBuilder.ScenePath},
         locationPathName=path,target=BuildTarget.Android,
         options=BuildOptions.Development|BuildOptions.AcceptExternalModificationsToPlayer
      });
      if(report.summary.result != BuildResult.Succeeded || !File.Exists(Path.Combine(path,"unityLibrary/build.gradle")))
          throw new Exception("HEFU_UNITY_LIBRARY_EXPORT_FAILED "+report.summary.result);
      Debug.Log("HEFU_UNITY_LIBRARY_EXPORT_PASS "+path);
    }
  }
}
