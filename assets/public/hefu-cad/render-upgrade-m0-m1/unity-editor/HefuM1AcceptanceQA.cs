using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor{
 public static class HefuM1AcceptanceQA{
  public static void Validate(){
   EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
   var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2").transform;
   var sofa=house.Find("Move_Sofa");var caps=sofa.Find(HefuM1StructureFinish.SeatName);
   if(!caps||caps.GetComponent<Collider>()||caps.parent!=sofa||
      caps.GetComponent<MeshFilter>().sharedMesh.vertexCount<800||
      !caps.GetComponent<MeshRenderer>().enabled)
    throw new Exception("HEFU_M1_QA_SOFA_VISIBLE_SEAT_FAIL");
   var capBounds=caps.GetComponent<Renderer>().bounds;
   if(capBounds.min.y<.50f||capBounds.max.y>.71f||capBounds.size.z<2.05f||capBounds.size.z>2.35f)
    throw new Exception("HEFU_M1_QA_SOFA_GEOMETRY_SCALE="+capBounds);
   if(house.GetComponentsInChildren<HefuMovable>(true).Length!=17)
    throw new Exception("HEFU_M1_QA_FURNITURE_CHANGED");
   var kitchen=house.Find(HefuInteriorDoorBuilder.GroupName+"/Door_Kitchen/DoorHinge/DoorLeaf");
   var mat=kitchen.GetComponent<Renderer>().sharedMaterial;
   if(!kitchen.GetComponent<Collider>()||mat.color.a>.15f||mat.renderQueue<3000||mat.GetFloat("_ZWrite")>.01f)
    throw new Exception("HEFU_M1_QA_KITCHEN_GLASS");
   int visible=0,hidden=0;
   foreach(var r in house.GetComponentsInChildren<Renderer>(true))if(r.name.EndsWith("_glass")&&r.name.Contains("CAD_WindowCell_")&&
    (r.name.StartsWith("21_CAD_")||r.name.StartsWith("22_CAD_")||r.name.StartsWith("23_CAD_")||
     r.name.StartsWith("27_CAD_")||r.name.StartsWith("28_CAD_")||r.name.StartsWith("29_CAD_"))){
    if(r.enabled){visible++;
     if(r.sharedMaterial.color.a>.06f||r.sharedMaterial.renderQueue<3000||r.sharedMaterial.GetFloat("_ZWrite")>0)
      throw new Exception("HEFU_M1_QA_SOUTH_NOT_TRANSPARENT");
    }else hidden++;
   }
   if(visible!=2||hidden!=4)throw new Exception("HEFU_M1_QA_SOUTH_GLASS_LAYERS");
   var sun=GameObject.Find("BakedLike_Sun_VisualPreview").GetComponent<Light>();
   if(sun.transform.forward.z<.3f||sun.transform.forward.y>-.3f||sun.shadows!=LightShadows.Soft)
    throw new Exception("HEFU_M1_QA_SUN_NOT_FROM_SOUTH");
   var light=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
   if(!light||!light.HasBakedPair||!light.pendant0||!light.pendant1||
      light.pendant0.type!=LightType.Spot||light.pendant1.type!=LightType.Spot)
    throw new Exception("HEFU_M1_QA_LAMP_PHYSICAL_SOURCE");
   foreach(var lamp in new[]{light.pendant0,light.pendant1}){
    if(lamp.transform.parent.name.IndexOf("DiningPendant_",StringComparison.Ordinal)!=0||
       Vector3.Dot(lamp.transform.forward,Vector3.down)<.95f||lamp.range<3f)
     throw new Exception("HEFU_M1_QA_SPOT_NOT_ILLUMINATING_TABLE");
   }
   if(light.glowingDiffusers.Length!=3||
    light.glowingDiffusers[0].sharedMaterial.GetColor("_EmissionColor").maxColorComponent<.20f)
    throw new Exception("HEFU_M1_QA_SHADE_NOT_EMISSIVE");
   var v=HefuPhotoViewpoints.All;
   if(v.Length!=10||v[9].id!="hero_living_dining"||v[9].eye.y!=1.55f)
    throw new Exception("HEFU_M1_QA_HERO_PRESET_MISSING");
   if(LightmapSettings.lightmaps.Length!=1||LightmapSettings.lightmaps[0].lightmapColor.width<128||
      !LightmapSettings.lightProbes||LightmapSettings.lightProbes.count!=46)
    throw new Exception("HEFU_M1_QA_BAKED_GI_MISSING");
   Debug.Log("HEFU_M1_ACCEPTANCE_PASS windowsSouthVisible=2 kitchenAlpha="+mat.color.a+
    " 3SeatCushions=true seatBound="+capBounds+" sofaMoveIDKept=true"+
    " warmAutumnSunFromSouth=true twoPhysicalPendantSpots=true emissiveShades=true"+
    " lightmap="+LightmapSettings.lightmaps[0].lightmapColor.width+
    " lightProbes=46 heroViewId="+v[9].id+" furniture=17");
  }
 }
}
