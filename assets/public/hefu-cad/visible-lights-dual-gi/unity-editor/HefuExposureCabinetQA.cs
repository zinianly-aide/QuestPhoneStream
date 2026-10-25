using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuExposureCabinetQA {
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  if(!house)throw new Exception("HEFU_335_CAD_MISSING");
  var root=house.transform.Find(HefuP2InteriorLightBuilder.RootName);
  if(!root)throw new Exception("HEFU_335_LIGHTS_MISSING");
  var dining=root.Find("DiningWarmPendant_Unshadowed");
  var living=root.Find("LivingWarmBounce_Unshadowed");
  var fixture=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(root.GetComponentsInChildren<Light>(true).Length!=0||!fixture||
     fixture.pendant0==null||fixture.pendant1==null||fixture.standingLamp==null)
   throw new Exception("HEFU_335_LEGACY_DETACHED_LIGHTS_REMAIN");
  if(fixture.pendant0.transform.parent.name!="DiningPendant_0"||
     fixture.pendant1.transform.parent.name!="DiningPendant_1"||
     fixture.standingLamp.transform.parent.parent.name!="LivingFloorLamp_PhysicalFixture")
   throw new Exception("HEFU_335_LIGHTS_NOT_INSIDE_FIXTURES");
  if(RenderSettings.ambientLight.r<.70f||RenderSettings.ambientLight.b<.65f)
   throw new Exception("HEFU_335_DAY_AMBIENT_TOO_LOW");
  var side=house.transform.Find("DiningSideboard");
  var maker=house.transform.Find(HefuInRoomPlacement.RootName);
  if(!side||!maker||maker.childCount!=5)throw new Exception("HEFU_335_CABINET_ROOTS");
  int pooled=0,uvs=0,walnut=0;
  foreach(var t in new[]{side,maker.GetChild(0),maker.GetChild(1),maker.GetChild(2),maker.GetChild(3),maker.GetChild(4)}){
   var overlay=t.Find(HefuCabinetPBRFinish.Overlay);
   if(!overlay||overlay.GetComponent<Collider>()||
     overlay.GetComponent<MeshFilter>()==null||
     overlay.GetComponent<MeshRenderer>()==null)
    throw new Exception("HEFU_335_CABINET_OVERLAY_MISSING="+t.name);
   var m=overlay.GetComponent<MeshFilter>().sharedMesh;
   if(m.vertexCount<50||m.uv.Length!=m.vertexCount)throw new Exception("HEFU_335_CABINET_UV_BAD="+t.name);
   var material=overlay.GetComponent<MeshRenderer>().sharedMaterial;
   if(!material.mainTexture||material.mainTexture.width!=512)
    throw new Exception("HEFU_335_IVORY_TEXTURE_BAD");
   pooled++;uvs+=m.vertexCount;
  }
  foreach(var mr in side.GetComponentsInChildren<MeshRenderer>(true))
   if(mr.sharedMaterial&&mr.sharedMaterial.name=="Hefu_DiningWalnut_48cm"){
    var f=mr.GetComponent<MeshFilter>();
    if(!f||f.sharedMesh.uv.Length!=f.sharedMesh.vertexCount)
     throw new Exception("HEFU_335_WALNUT_UV_BAD");
    walnut++;
   }
  if(pooled!=6||uvs<800||walnut<2)throw new Exception("HEFU_335_CAB_COUNT_BAD pooled="+pooled+" vertices="+uvs+" walnut="+walnut);
  int furniture=house.GetComponentsInChildren<HefuMovable>(true).Length;
  if(furniture!=17)throw new Exception("HEFU_335_FURNITURE_ROOTS_CHANGED="+furniture);
  var ceiling=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
  if(!ceiling||!ceiling.ceiling)throw new Exception("HEFU_335_CEILING_LOST");
  Debug.Log("HEFU_335_EXPOSURE_CABINET_QA_PASS lights=3 actualFixtures=true invisibleFillLights=0"+
   " cabinetBatches="+pooled+" doorVertices="+uvs+" walnutMesh="+walnut+
   " movableFurniture=17 ceilingCutawayRetained=true");
 }
}
}
