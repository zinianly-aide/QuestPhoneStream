using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor{
public static class HefuM3QualityQA{
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2").transform;
  var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
  var q=viewer?viewer.quality:null;
  if(!q||q.roomProbes==null||q.roomProbes.Length!=2||!q.sceneCamera)
   throw new Exception("HEFU_M3_QA_CONTROLLER_MISSING");
  var a=q.roomProbes[0];var b=q.roomProbes[1];
  if(a==b||a.mode!=ReflectionProbeMode.Custom||b.mode!=ReflectionProbeMode.Custom||
     a.resolution!=128||b.resolution!=128||a.customBakedTexture==null||b.customBakedTexture==null)
   throw new Exception("HEFU_M3_QA_BAKED_LOCAL_CUBEMAPS_MISSING");
  var ca=a.customBakedTexture as Cubemap;var cb=b.customBakedTexture as Cubemap;
  if(ca==null||cb==null||ca.width!=128||cb.width!=128||ca==cb)
   throw new Exception("HEFU_M3_QA_CUBE_FORMAT_OR_SHARED_REFERENCE");
  // The earlier Lightmapping.BakeReflectionProbe produced TWO IDENTICAL EXRs.
  // Verify the new actual scene captures differ on 6 faces, not just filenames.
  int different=0;double variation=0;
  foreach(var f in new[]{CubemapFace.PositiveX,CubemapFace.NegativeX,CubemapFace.PositiveY,
                          CubemapFace.NegativeY,CubemapFace.PositiveZ,CubemapFace.NegativeZ}){
   var pa=ca.GetPixels(f);var pb=cb.GetPixels(f);
   if(pa.Length!=pb.Length||pa.Length!=128*128)throw new Exception("HEFU_M3_QA_CUBE_BAD_FACE");
   for(int i=0;i<pa.Length;i+=31){
    var delta=Mathf.Abs(pa[i].r-pb[i].r)+Mathf.Abs(pa[i].g-pb[i].g)+Mathf.Abs(pa[i].b-pb[i].b);
    variation+=delta;if(delta>.03f)different++;
   }
  }
  if(different<30||variation<2)throw new Exception("HEFU_M3_QA_CAPTURE_STILL_IDENTICAL="+different);
  if(house.GetComponentsInChildren<HefuMovable>(true).Length!=17)
   throw new Exception("HEFU_M3_QA_FURNITURE_IDS_CHANGED");
  if(q.GetComponent<HefuM2DailyDecor>()==null||viewer.dailyDecor==null)
   throw new Exception("HEFU_M3_QA_M2_DAILY_DECOR_MISSING");
  if(!UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>().HasBakedPair)
   throw new Exception("HEFU_M3_QA_DUAL_GI_MISSING");
  var beforeQuality=QualitySettings.GetQualityLevel();
  int savedAA=QualitySettings.antiAliasing,savedLights=QualitySettings.pixelLightCount;
  float savedDistance=QualitySettings.shadowDistance;
  var savedShadows=QualitySettings.shadows;
  var savedRes=QualitySettings.shadowResolution;
  var savedAniso=QualitySettings.anisotropicFiltering;
  int savedMips=QualitySettings.masterTextureLimit;
  bool savedRealtime=QualitySettings.realtimeReflectionProbes;
  int savedFrameRate=Application.targetFrameRate;
  bool savedHdr=q.sceneCamera.allowHDR;
  int pref=PlayerPrefs.GetInt("Hefu_M3_QualityTier_v1",-22);
  int hudPref=PlayerPrefs.GetInt("Hefu_M3_PerformanceHUD_v1",-23);
  try{
   q.Apply(HefuM3QualityController.Tier.Battery);
   if(a.enabled||b.enabled||QualitySettings.antiAliasing!=0||
      QualitySettings.pixelLightCount!=2||QualitySettings.shadows!=ShadowQuality.Disable||
      Application.targetFrameRate!=45||q.sceneCamera.allowHDR||QualitySettings.masterTextureLimit!=1)
     throw new Exception("HEFU_M3_QA_BATTERY_PROFILE_FAIL");
   q.Apply(HefuM3QualityController.Tier.Balanced);
   if(!a.enabled||!b.enabled||QualitySettings.antiAliasing!=2||QualitySettings.pixelLightCount!=3||
      QualitySettings.shadowDistance!=9f||Application.targetFrameRate!=60||q.sceneCamera.allowHDR)
     throw new Exception("HEFU_M3_QA_BALANCED_PROFILE_FAIL");
   q.Apply(HefuM3QualityController.Tier.Quality);
   if(!a.enabled||!b.enabled||QualitySettings.antiAliasing!=4||QualitySettings.pixelLightCount!=4||
      QualitySettings.shadowDistance!=13f||Application.targetFrameRate!=72||!q.sceneCamera.allowHDR)
     throw new Exception("HEFU_M3_QA_HIGH_PROFILE_FAIL");
   if(a.mode!=ReflectionProbeMode.Custom||b.mode!=ReflectionProbeMode.Custom||
       QualitySettings.realtimeReflectionProbes)
     throw new Exception("HEFU_M3_QA_RUNTIME_REFLECTION_CAPTURE_UNEXPECTED");
   q.Apply(HefuM3QualityController.Tier.Balanced);
   if(PlayerPrefs.GetInt("Hefu_M3_QualityTier_v1",-22)!=pref||
      PlayerPrefs.GetInt("Hefu_M3_PerformanceHUD_v1",-23)!=hudPref)
     throw new Exception("HEFU_M3_QA_PREFERENCE_MUTATED");
  }finally{
   QualitySettings.antiAliasing=savedAA;QualitySettings.pixelLightCount=savedLights;
   QualitySettings.shadowDistance=savedDistance;QualitySettings.shadows=savedShadows;
   QualitySettings.shadowResolution=savedRes;QualitySettings.anisotropicFiltering=savedAniso;
   QualitySettings.masterTextureLimit=savedMips;QualitySettings.realtimeReflectionProbes=savedRealtime;
   Application.targetFrameRate=savedFrameRate;q.sceneCamera.allowHDR=savedHdr;
   a.enabled=true;b.enabled=true;
  }
  Debug.Log("HEFU_M3_QA_PASS uniqueSceneCubemaps=2 cubeWidth=128 colorDifferentPixels="+different+
   " threeProfileTransitions=PASS batteryAA=0 balancedAA=2 qualityAA=4"+
   " lights=2/3/4 shadows=off/9m/13m reflection=off/on/on"+
   " runtimeCubeCaptures=0 fpsOnlyMeasuredOnDevice=true"+
   " oldFurniture17=true independentPrefsIntact=true");
 }
}
}
