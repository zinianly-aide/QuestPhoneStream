using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuDualLightingQA {
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var fixture=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!fixture||!fixture.HasBakedPair)throw new Exception("HEFU_DUAL_QA_INCOMPLETE_LIGHT_PRESETS fixture="+(fixture!=null)+" asset="+(fixture&&fixture.baked!=null)+" day="+(fixture&&fixture.baked&&fixture.baked.dayColor!=null)+" birthday="+(fixture&&fixture.baked&&fixture.baked.birthdayColor!=null)+" probes="+(fixture&&fixture.baked?fixture.baked.probesPerPreset:-1)+" lengthDay="+(fixture&&fixture.baked&&fixture.baked.dayProbeSH!=null?fixture.baked.dayProbeSH.Length:-1)+" lengthBirthday="+(fixture&&fixture.baked&&fixture.baked.birthdayProbeSH!=null?fixture.baked.birthdayProbeSH.Length:-1));
  var data=fixture.baked;
  if(data.dayColor==data.birthdayColor||data.dayColor.width<64||data.birthdayColor.width<64 ||
     data.probesPerPreset!=46)throw new Exception("HEFU_DUAL_QA_ATLAS_UNVERIFIED");
  var a=System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(data.dayColor));
  var b=System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(data.birthdayColor));
  using(var sha=System.Security.Cryptography.SHA256.Create()){
   if(BitConverter.ToString(sha.ComputeHash(a))==BitConverter.ToString(sha.ComputeHash(b)))
    throw new Exception("HEFU_DUAL_QA_BIRTHDAY_LIGHTMAP_IDENTICAL_TO_DAY");
  }
  int probeDifferences=0;
  for(int i=0;i<data.dayProbeSH.Length;i++)
   if(Mathf.Abs(data.dayProbeSH[i]-data.birthdayProbeSH[i])>.00015f)probeDifferences++;
  if(probeDifferences<12)throw new Exception("HEFU_DUAL_QA_PROBES_UNCHANGED="+probeDifferences);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  if(house.GetComponentsInChildren<HefuMovable>(true).Length!=17)
   throw new Exception("HEFU_DUAL_QA_FURNITURE_LOST");
  if(house.transform.Find(HefuP2InteriorLightBuilder.RootName).GetComponentsInChildren<Light>(true).Length!=0)
   throw new Exception("HEFU_DUAL_QA_INVISIBLE_FLOATING_LIGHTS");
  var lamp=house.transform.Find(HefuPracticalLampBuilder.RootName+"/LivingFloorLamp_PhysicalFixture");
  var sofa=house.transform.Find("Move_Sofa");
  var sofaBounds=sofa.GetComponentInChildren<Renderer>().bounds;
  foreach(var r in sofa.GetComponentsInChildren<Renderer>(true))sofaBounds.Encapsulate(r.bounds);
  if(lamp && (lamp.position.z>=sofaBounds.min.z-.12f ||
     lamp.position.x>=sofaBounds.min.x-.10f))
   throw new Exception("HEFU_DUAL_QA_FLOOR_LAMP_BEHIND_PARTITION");
  if(lamp){
   var eye=new Vector3(-6.45f,1.57f,-6.30f);
   var target=lamp.Find("LinenShade").position;
   RaycastHit hit;
   if(Physics.Raycast(eye,(target-eye).normalized,out hit,(target-eye).magnitude-.01f))
    throw new Exception("HEFU_DUAL_QA_FLOOR_LAMP_OCCLUDED_BY="+hit.collider.name);
  }
  if(!lamp||lamp.GetComponentsInChildren<Collider>(true).Length>0)
   throw new Exception("HEFU_DUAL_QA_LIVING_LAMP_COLLISION");
  if(fixture.pendant0.transform.parent.name!="DiningPendant_0"||
     fixture.pendant1.transform.parent.name!="DiningPendant_1"||
     fixture.standingLamp.transform.IsChildOf(lamp)==false)
   throw new Exception("HEFU_DUAL_QA_LIGHTS_NOT_INSIDE_VISIBLE_FIXTURES");
  if(fixture.glowingDiffusers.Length!=3||
    fixture.glowingDiffusers[0].sharedMaterial.GetColor("_EmissionColor").maxColorComponent<.1f)
   throw new Exception("HEFU_DUAL_QA_VISIBLE_EMISSION_MISSING");
  foreach(var l in new[]{fixture.pendant0,fixture.pendant1,fixture.standingLamp})
   if(l.shadows!=LightShadows.None)throw new Exception("HEFU_DUAL_QA_EXTRA_SHADOWED_LIGHT");
  fixture.ApplyMode(true);
  if(LightmapSettings.lightmaps[0].lightmapColor!=data.birthdayColor ||
     fixture.pendant0.intensity<=1f ||fixture.sun.intensity>=.4f)
   throw new Exception("HEFU_DUAL_QA_BIRTHDAY_SWITCH_FAIL");
  fixture.ApplyMode(false);
  if(LightmapSettings.lightmaps[0].lightmapColor!=data.dayColor ||
     fixture.pendant0.intensity>=1.30f ||fixture.sun.intensity<.9f)
   throw new Exception("HEFU_DUAL_QA_DAY_SWITCH_FAIL");
  if(LightmapSettings.lightProbes==null||LightmapSettings.lightProbes.count!=46)
   throw new Exception("HEFU_DUAL_QA_PROBE_COUNT_FAIL");
  // No allocations/reprocessing of SH or LightmapData on repeated button taps.
  int cacheBuilds=fixture.CacheBuildCount,applies=fixture.ModeApplyCount;
  for(int i=0;i<16;i++)fixture.ApplyMode(i%2==0);
  fixture.ApplyMode(false);
  if(fixture.CacheBuildCount!=cacheBuilds||fixture.ModeApplyCount<applies+15)
   throw new Exception("HEFU_DUAL_QA_RUNTIME_CACHE_REGRESSION");
  applies=fixture.ModeApplyCount;
  fixture.ApplyMode(false);
  if(fixture.ModeApplyCount!=applies)
   throw new Exception("HEFU_DUAL_QA_IDENTICAL_MODE_NOT_SKIPPED");
  Debug.Log("HEFU_DUAL_LIGHT_CACHE_QA_PASS probeCacheBuilds="+cacheBuilds+
   " toggles=16 noIdenticalModeWork=true noTetrahedralizeOnToggle=true");
  Debug.Log("HEFU_DUAL_LIGHTING_QA_PASS physicalPendants=2 physicalFloorLamp=1"+
   " emissiveBulbs=3 noHiddenFills=true noExtraShadows=true"+
   " atlasDay="+data.dayColor.width+" atlasBirthday="+data.birthdayColor.width+" bakedProbesEach=46"+
   " differences="+probeDifferences+" runtimeLightmapSwap=true SHSwap=true"+
   " birthdaySunReduced=true furniture17=true");
 }
}
}
