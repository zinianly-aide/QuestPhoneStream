using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuWarmReferenceStyleQA {
 static Transform Required(Transform p,string name){var t=p.Find(name);if(!t)throw new Exception("HEFU_WARM_STYLE_MISSING_"+name);return t;}
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  if(!house)throw new Exception("HEFU_WARM_STYLE_NO_CAD");
  var warm=Required(house.transform,HefuWarmReferenceStyleBuilder.GroupName);
  if(warm.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("HEFU_WARM_STYLE_COLLIDERS");
  if(warm.GetComponentsInChildren<Light>(true).Length!=2)throw new Exception("HEFU_WARM_STYLE_ATTACHED_PENDANT_LIGHTS_MISSING");
  var table=Required(house.transform,"Move_Dining_Table");
  int lampCount=0;
  foreach(Transform obj in warm){
   if(obj.name.StartsWith("DiningPendant_")){
    lampCount++;
    var shade=Required(obj,"IvoryShade_RealGeometry");
    var mf=shade.GetComponent<MeshFilter>();
    if(!mf||mf.sharedMesh.vertexCount<100||!shade.GetComponent<Renderer>())
     throw new Exception("HEFU_WARM_STYLE_BAD_SHADE_MESH");
    if(Mathf.Abs(obj.position.x-table.position.x)>.03f||
       Mathf.Abs(obj.position.z-table.position.z)>.50f ||
       shade.position.y<1.94f||shade.position.y>2.10f)
     throw new Exception("HEFU_WARM_STYLE_PENDANT_WRONG_POSITION "+shade.position);
   }
  }
  if(lampCount!=2)throw new Exception("HEFU_WARM_STYLE_PENDANT_COUNT="+lampCount);
  int curtainCount=0;
  foreach(Transform o in warm){
   if(!o.name.StartsWith("SouthWindowLinenSidePanel_"))continue;
   curtainCount++;
   var renderer=o.GetComponent<MeshRenderer>();
   var filter=o.GetComponent<MeshFilter>();
   if(!renderer||!filter||!renderer.sharedMaterial.mainTexture || filter.sharedMesh.vertexCount<250)
    throw new Exception("HEFU_WARM_STYLE_LINEN_MESH_OR_TEXTURE");
   if(o.position.z<-9.82f||o.position.z>-9.55f || o.position.y<.7f || o.position.y>.8f)
    throw new Exception("HEFU_WARM_STYLE_CURTAIN_WRONG_DEPTH");
   if(Mathf.Abs(o.position.x+7.925f)<1.85f)
    throw new Exception("HEFU_WARM_STYLE_CURTAIN_BLOCKS_WINDOW_CENTER");
  }
  if(curtainCount!=2||!warm.Find("SouthWindowCurtainRail"))throw new Exception("HEFU_WARM_STYLE_CURTAIN_COUNT");
  var style=UnityEngine.Object.FindObjectOfType<HefuDecorSwitcher>();
  if(!style||style.wallRenderers==null||style.wallRenderers.Length<1)
   throw new Exception("HEFU_WARM_STYLE_SWITCHER_LOST");
  style.preset=0;style.Apply();var wall=style.wallRenderers[0].material.color;
  if(wall.r<.9f||wall.b<.83f||wall.r<wall.b)
   throw new Exception("HEFU_WARM_STYLE_NEUTRAL_WALL_NOT_IVORY "+wall);
  style.preset=2;style.Apply();var cool=style.wallRenderers[0].material.color;
  if(cool.b<=cool.r)throw new Exception("HEFU_WARM_STYLE_SWITCHER_COOL_REGRESSION "+cool);
  style.preset=0;style.Apply();
  int movable=house.GetComponentsInChildren<HefuMovable>(true).Length;
  if(movable!=17)throw new Exception("HEFU_WARM_STYLE_MOVABLE_COUNT="+movable);
  Debug.Log("HEFU_WARM_STYLE_QA_PASS lamps=2 ivoryShades=true linenPanels=2 transparentSouthWindowUnblocked=true"+
    " furniture=17 noColliders=true noNewLights=true wallStyleSwitchPreserved=true");
 }
}
}
