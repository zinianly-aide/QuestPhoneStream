using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using HefuVR;

namespace HefuVR.Editor {
/// Separate scene/settings and native Unity process for every M5 GI/AO bake.
/// Never calls HefuStaticGiBake.Prepare or changes the original M4 scene.
public static class HefuM5GiAoExperiment {
 const string Original="Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity";
 const string SceneDir="Assets/Hefu/Scenes/M5GiAoExperiments/";
 const string SettingsDir="Assets/Hefu/Generated/M5GiAoExperiments/";
 const string Out="ABTestReports/M5_GI_AO/";
 const int Movables=17,StaticShell=20,ProbeCount=46;
 static string Scene(bool ao)=>SceneDir+(ao?"M5_GI20_AO065":"M5_GI20_NoAO")+".unity";
 static string Settings(bool ao)=>SettingsDir+(ao?"M5_GI20_AO065":"M5_GI20_NoAO")+".lighting";
 static void Guard(string stage){
   var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
   if(!house)throw new Exception(stage+" CAD missing");
   var moving=house.GetComponentsInChildren<HefuMovable>(true);
   if(moving.Length!=Movables)throw new Exception(stage+" movers="+moving.Length);
   if(moving.Any(m=>m.GetComponentsInChildren<MeshRenderer>(true).Any(r=>
      GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.ContributeGI))))
      throw new Exception(stage+" movable furniture marked static");
   int fixedCount=house.GetComponentsInChildren<MeshRenderer>(true).Count(r=>
      GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.ContributeGI));
   if(fixedCount!=StaticShell)throw new Exception(stage+" staticCAD="+fixedCount);
   if(!LightmapSettings.lightProbes || LightmapSettings.lightProbes.count!=ProbeCount)
      throw new Exception(stage+" probes must equal 46");
   foreach(var wallName in new[]{"35_CAD_wall_35","42_CAD_wall_42"}){
     var wall=house.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name==wallName);
     if(wall && GameObjectUtility.AreStaticEditorFlagsSet(wall.gameObject,StaticEditorFlags.ContributeGI))
       throw new Exception(stage+" duplicate CAD wall static "+wallName);
   }
 }
 static void ApplyAO(LightingSettings ls,bool enabled){
   // Unity 2022 Built-in LightingSettings serialized fields.
   var s=new SerializedObject(ls);
   var enabledProp=s.FindProperty("m_AO");
   var dist=s.FindProperty("m_AOMaxDistance");
   var indirect=s.FindProperty("m_CompAOExponent");
   var direct=s.FindProperty("m_CompAOExponentDirect");
   if(enabledProp==null||dist==null||indirect==null||direct==null)
     throw new Exception("M5_UNSUPPORTED_AO_FIELDS");
   enabledProp.boolValue=enabled;dist.floatValue=.65f;
   indirect.floatValue=enabled?.55f:0f;
   direct.floatValue=enabled?.05f:0f;
   s.ApplyModifiedPropertiesWithoutUndo();
 }
 static void Create(bool enabled){
   string scenePath=Scene(enabled), settingsPath=Settings(enabled);
   if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) ||
      AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath))
     throw new Exception("M5_OUTPUT_EXISTS_ABORT "+scenePath);
   if(!AssetDatabase.CopyAsset(Original,scenePath))throw new Exception("M5_COPY_SCENE_FAILED");
   var sc=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
   Guard("M5_STAGE");
   string source=AssetDatabase.GetAssetPath(Lightmapping.lightingSettings);
   if(String.IsNullOrEmpty(source)||!AssetDatabase.CopyAsset(source,settingsPath))
     throw new Exception("M5_COPY_SETTINGS_FAILED "+source);
   var ls=AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
   ls.bakedGI=true;ls.realtimeEnvironmentLighting=false;
   ls.lightmapResolution=20f;ls.lightmapMaxSize=512;ls.lightmapPadding=5;
   ls.directSampleCount=48;ls.indirectSampleCount=128;ls.environmentSampleCount=48;
   ApplyAO(ls,enabled);
   Lightmapping.lightingSettings=ls;
   EditorUtility.SetDirty(ls);EditorSceneManager.MarkSceneDirty(sc);
   if(!EditorSceneManager.SaveScene(sc))throw new Exception("M5_STAGE_SAVE_FAILED");
   AssetDatabase.SaveAssets();
   Debug.Log("HEFU_M5_STAGE_PASS mode="+(enabled?"AO":"NoAO")+
      " density=20 maxAtlas=512 aoDistance=.65 static=20 movers=17 probes=46 scene="+scenePath);
 }
 public static void Stage(){
   Directory.CreateDirectory(SceneDir);Directory.CreateDirectory(SettingsDir);
   Directory.CreateDirectory(Out);
   Create(false);Create(true);
   Debug.Log("HEFU_M5_STAGE_ALL_PASS independentScenes=2 originalUntouched=true");
 }
 static void Bake(bool ao){
   string path=Scene(ao);
   if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
     throw new Exception("M5_STAGE_REQUIRED "+path);
   var sc=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   Guard("M5_PREBAKE");
   if(!Lightmapping.lightingSettings||
      AssetDatabase.GetAssetPath(Lightmapping.lightingSettings)!=Settings(ao))
     throw new Exception("M5_SETTINGS_NOT_BOUND");
   var check=new SerializedObject(Lightmapping.lightingSettings).FindProperty("m_AO");
   if(check==null||check.boolValue!=ao)throw new Exception("M5_AO_MISMATCH");
   var begin=DateTime.UtcNow;
   Debug.Log("HEFU_M5_BAKE_START mode="+(ao?"AO":"NoAO")+" density=20 maxAtlas=512");
   bool ok=Lightmapping.Bake();
   var maps=LightmapSettings.lightmaps;
   var probes=LightmapSettings.lightProbes;
   string data=Lightmapping.lightingDataAsset?
     AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset):"none";
   Debug.Log("HEFU_M5_BAKE_METRICS mode="+(ao?"AO":"NoAO")+
      " success="+ok+" seconds="+(DateTime.UtcNow-begin).TotalSeconds.ToString("F1")+
      " atlases="+(maps!=null?maps.Length:0)+" firstWidth="+
      (maps!=null&&maps.Length>0&&maps[0].lightmapColor?maps[0].lightmapColor.width:0)+
      " probes="+(probes?probes.count:0)+" lightingData="+data);
   if(!ok||maps==null||maps.Length<1||maps.Length>4||!probes||probes.count!=ProbeCount||
      data=="none"||data.Contains("/Hefu_Mobile_Viewer/"))
     throw new Exception("HEFU_M5_BAKE_INVALID_NO_PROMOTION");
   Guard("M5_POSTBAKE");
   if(!EditorSceneManager.SaveScene(sc))throw new Exception("M5_BAKE_SAVE_FAILED");
   AssetDatabase.SaveAssets();
   Debug.Log("HEFU_M5_BAKE_PASS mode="+(ao?"AO":"NoAO")+" scene="+path);
 }
 public static void BakeNoAO(){Bake(false);}
 public static void BakeAO(){Bake(true);}
 static void Capture(string path,string label){
   EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   Guard("M5_CAPTURE");
   var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
   var roof=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
   if(!viewer||!viewer.cam)throw new Exception("M5_VIEWER_MISSING");
   // Do not call FixtureLighting.ApplyMode: this would restore the old M4 GI.
   var go=new GameObject("M5_TEMP_CAPTURE_CAMERA");
   var cam=go.AddComponent<Camera>();cam.CopyFrom(viewer.cam);cam.enabled=false;
   cam.fieldOfView=HefuM4PresentationPlan.Fov;cam.allowHDR=false;
   cam.nearClipPlane=.04f;cam.farClipPlane=110f;
   var rt=new RenderTexture(2048,1365,24);
   var image=new Texture2D(2048,1365,TextureFormat.RGB24,false);
   var prev=RenderTexture.active;
   try{
     Directory.CreateDirectory(Out);rt.Create();cam.targetTexture=rt;
     foreach(var pose in HefuM4PresentationPlan.Views){
       cam.transform.position=pose.eye;
       cam.transform.rotation=Quaternion.LookRotation((pose.target-pose.eye).normalized);
       if(roof)roof.UpdateVisibility(pose.eye);
       cam.Render();cam.Render();RenderTexture.active=rt;
       image.ReadPixels(new Rect(0,0,2048,1365),0,0);image.Apply();
       var png=image.EncodeToPNG();
       if(png.Length<50000)throw new Exception("M5_EMPTY_IMAGE "+pose.id);
       var file=Out+label+"_"+pose.id+".png";
       File.WriteAllBytes(file,png);
       Debug.Log("HEFU_M5_CAPTURE_FRAME "+file+" bytes="+png.Length);
     }
   } finally {
     cam.targetTexture=null;RenderTexture.active=prev;rt.Release();
     UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
     UnityEngine.Object.DestroyImmediate(go);
   }
 }
 public static void CaptureOriginal(){Capture(Original,"Baseline_M4Lighting");}
 public static void CaptureNoAO(){Capture(Scene(false),"GI20_NoAO");}
 public static void CaptureAO(){Capture(Scene(true),"GI20_AO065");}
}
}
