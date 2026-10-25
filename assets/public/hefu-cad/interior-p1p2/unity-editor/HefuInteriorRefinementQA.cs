using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuInteriorRefinementQA {
 static Transform Find(Transform root,string id){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==id)return t;throw new Exception("HEFU_INTERIOR_QA_MISSING_"+id);}
 static void CheckVisual(Transform house,string holder,string original,string imported,int minVertices){
  var movable=Find(house,holder);
  var source=Find(movable,"Hefu_FurnitureDetail_"+original);
  if(source.GetComponent<MeshRenderer>().enabled)throw new Exception("HEFU_INTERIOR_QA_DOUBLE_DRAW_"+original);
  var art=Find(source,"Hefu_P1_BlenderVisual_"+imported);
  var renderers=art.GetComponentsInChildren<MeshRenderer>(true);
  if(renderers.Length!=1||!renderers[0].enabled ||!renderers[0].receiveShadows)
   throw new Exception("HEFU_INTERIOR_QA_NO_P1_VISIBLE_RENDERER_"+imported);
  var filter=renderers[0].GetComponent<MeshFilter>();
  if(!filter||filter.sharedMesh.vertexCount<minVertices || filter.sharedMesh.uv.Length==0)
   throw new Exception("HEFU_INTERIOR_QA_P1_MESH_"+imported);
  if(art.GetComponentsInChildren<Collider>(true).Length!=0)
   throw new Exception("HEFU_INTERIOR_QA_DUPLICATE_COLLIDER_"+imported);
  if(renderers[0].bounds.max.y > 1.16f || renderers[0].bounds.min.y < -.02f)
   throw new Exception("HEFU_INTERIOR_QA_VERTICAL_FOOTPRINT_"+imported+" "+renderers[0].bounds);
  Debug.Log("HEFU_P1_ART_QA_PASS "+imported+" verts="+filter.sharedMesh.vertexCount+
   " materials="+renderers[0].sharedMaterials.Length+" fallbackHidden=true collisionOwnedByCAD=true");
 }
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  if(!house)throw new Exception("HEFU_INTERIOR_QA_NO_CAD");
  CheckVisual(house.transform,"Move_Sofa","SofaUpholstery","HefuP1_SoftSofa",2400);
  CheckVisual(house.transform,"Move_Coffee_Table","WalnutCoffeeTable","HefuP1_WalnutCoffee",3800);
  int count=house.GetComponentsInChildren<HefuMovable>(true).Length;
  if(count!=17)throw new Exception("HEFU_INTERIOR_QA_MOVABLE_COUNT="+count);
  var grp=house.transform.Find(HefuP2InteriorLightBuilder.RootName);
  if(!grp)throw new Exception("HEFU_INTERIOR_QA_MISSING_LIGHT_RIG");
  int lights=0;
  foreach(var l in grp.GetComponentsInChildren<Light>(true)){
   if(l.type!=LightType.Point||l.shadows!=LightShadows.None||l.range>4.6f)
    throw new Exception("HEFU_INTERIOR_QA_LIGHT_PERFORMANCE");
   lights++;
  }
  if(lights!=2||RenderSettings.defaultReflectionMode!=DefaultReflectionMode.Skybox ||
    RenderSettings.reflectionIntensity>.7f)
   throw new Exception("HEFU_INTERIOR_QA_SKY_REFLECTION_OR_POINT_COUNT");
  if(house.transform.Find(HefuSceneAtmosphereBuilder.RootName)==null)
   throw new Exception("HEFU_INTERIOR_QA_OUTDOOR_REGRESSION");
  // Perspective test metadata; top_n is a CAD inspection view, not a reliable
  // measure of architectural lighting/glass or exterior quality.
  var ids=new HashSet<string>();
  foreach(var camera in HefuPhotoViewpoints.All)ids.Add(camera.id);
  foreach(var id in new[]{"living","kitchen","balcony","entry"})
   if(!ids.Contains(id))throw new Exception("HEFU_INTERIOR_QA_REQUIRED_PERSPECTIVE_VIEW_"+id);
  Debug.Log("HEFU_P1_P2_INTERIOR_QA_PASS newFurniture=2 17movables=true"+
   " nonshadowWarmLights=2 skyboxReflection=true livingKitchenBalconyEntryViews=true"+
   " oldFurnitureRenderable=false noNewColliders=true");
 }
}
}
