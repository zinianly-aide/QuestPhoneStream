using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor {
/// <summary>Two separate Unity GPU bakes, saves two complete lightmaps and SH
/// probe coefficient arrays; the second bake must not overwrite first atlas.
/// Day and birthday scenes share stable UV layout and 46 probe locations.</summary>
public static class HefuDualLightingBake {
 const string Scene="Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity";
 public const string Dir="Assets/Hefu/Generated/DualLighting/";
 const string Asset=Dir+"DayBirthdayLighting.asset";
 static HefuLightingPresets Get(){
  Directory.CreateDirectory(Dir);
  var result=AssetDatabase.LoadAssetAtPath<HefuLightingPresets>(Asset);
  if(!result){result=ScriptableObject.CreateInstance<HefuLightingPresets>();AssetDatabase.CreateAsset(result,Asset);}
  return result;
 }
 static Texture2D Copy(Texture2D source,string id){
  if(!source)return null;
  var path=AssetDatabase.GetAssetPath(source);
  string dest=Dir+id+System.IO.Path.GetExtension(path);
  if(path==dest)return source;
  if(AssetDatabase.LoadMainAssetAtPath(dest))AssetDatabase.DeleteAsset(dest);
  if(!AssetDatabase.CopyAsset(path,dest))throw new Exception("HEFU_DUAL_LIGHTMAP_COPY_FAILED "+path+" -> "+dest);
  AssetDatabase.ImportAsset(dest,ImportAssetOptions.ForceUpdate);
  var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(dest);
  if(!tex)throw new Exception("HEFU_DUAL_LIGHTMAP_IMPORT_FAILED "+dest);
  return tex;
 }
 static void Capture(bool birthday){
  var data=LightmapSettings.lightmaps;
  if(data==null||data.Length!=1||!data[0].lightmapColor||
     LightmapSettings.lightProbes==null||LightmapSettings.lightProbes.count<30)
   throw new Exception("HEFU_DUAL_BAKE_MISSING_LIGHTING");
  var presets=Get();
  var sh=LightmapSettings.lightProbes.bakedProbes;
  if(birthday){
   if(!presets.dayColor||presets.dayProbeSH==null||presets.dayProbeSH.Length!=sh.Length*27)
    throw new Exception("HEFU_DUAL_BIRTHDAY_REQUIRES_COMPLETED_DAY_BAKE");
   presets.birthdayColor=Copy(data[0].lightmapColor,"Birthday_Atlas");
   presets.birthdayDir=Copy(data[0].lightmapDir,"Birthday_Direction");
   presets.birthdayMask=Copy(data[0].shadowMask,"Birthday_ShadowMask");
   presets.birthdayProbeSH=HefuLightingPresets.Flatten(sh);
  }else{
   presets.dayColor=Copy(data[0].lightmapColor,"Day_Atlas");
   presets.dayDir=Copy(data[0].lightmapDir,"Day_Direction");
   presets.dayMask=Copy(data[0].shadowMask,"Day_ShadowMask");
   presets.dayProbeSH=HefuLightingPresets.Flatten(sh);
  }
  presets.probesPerPreset=sh.Length;
  EditorUtility.SetDirty(presets);
  var controller=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!controller)throw new Exception("HEFU_DUAL_FIXTURE_CONTROLLER_MISSING");
  controller.baked=presets;
  EditorUtility.SetDirty(controller);
  EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
  AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
  Debug.Log("HEFU_DUAL_CAPTURE_PASS mode="+(birthday?"Birthday":"Day")+
   " atlas="+data[0].lightmapColor.width+"x"+data[0].lightmapColor.height+
   " probes="+sh.Length+" readbackSH="+presets.probesPerPreset);
 }
 static void Bake(bool birthday){
  EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
  var controller=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!controller)throw new Exception("HEFU_DUAL_CONTROLLER_MISSING");
  controller.SetFixtureVisuals(birthday);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  HefuStaticGiBake.BakeCurrent(house);
  Capture(birthday);
  if(birthday){
   controller.ApplyMode(false); // APK always starts with day lighting.
   EditorSceneManager.SaveOpenScenes();
  }
 }
 public static void FinalizeScene(){
  EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
  var controller=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!controller)throw new Exception("HEFU_DUAL_FINALIZE_CONTROLLER_MISSING");
  controller.baked=Get();
  if(!controller.HasBakedPair)throw new Exception("HEFU_DUAL_FINALIZE_BOTH_ATLASES_REQUIRED");
  controller.ApplyMode(false);
  EditorUtility.SetDirty(controller);
  EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
  EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
  Debug.Log("HEFU_DUAL_LIGHTING_FINALIZED default=Day atlasPairReady=true probesEach="+controller.baked.probesPerPreset);
 }
 public static void BakeDay(){Bake(false);}
 public static void BakeBirthday(){Bake(true);}
}
}
