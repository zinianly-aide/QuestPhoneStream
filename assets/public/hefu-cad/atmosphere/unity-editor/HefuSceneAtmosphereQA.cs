using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuSceneAtmosphereQA {
 public static void Validate(){
  var sc=EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  if(!sc.IsValid())throw new Exception("HEFU_ATMO_QA_MOBILE_SCENE_INVALID");
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  if(!house)throw new Exception("HEFU_ATMO_QA_NO_CAD_HOUSE");
  var outdoors=house.transform.Find(HefuSceneAtmosphereBuilder.RootName);
  if(!outdoors)throw new Exception("HEFU_ATMO_QA_OUTDOOR_MISSING");
  int trees=0,buildings=0;
  foreach(Transform obj in outdoors){
   if(obj.name.StartsWith("Illustrative_Tree_")){
    trees++;
    if(obj.position.z>=-11f)throw new Exception("HEFU_ATMO_QA_TREE_WRONG_NORTH_SOUTH");
    if(obj.GetComponentsInChildren<Renderer>(true).Length==0)throw new Exception("HEFU_ATMO_QA_TREE_MESH_MISSING");
   }
   if(obj.name.StartsWith("Illustrative_DistantBuilding_")){
    buildings++;
    if(obj.position.z>=-25f)throw new Exception("HEFU_ATMO_QA_BUILDING_WRONG_SIDE");
   }
  }
  if(trees!=5||buildings!=9)throw new Exception("HEFU_ATMO_QA_OUTDOOR_COUNT="+trees+"/"+buildings);
  if(outdoors.GetComponentsInChildren<Collider>(true).Length!=0)
   throw new Exception("HEFU_ATMO_QA_OUTDOOR_COLLIDER_UNEXPECTED");
  var group=house.transform.Find(HefuInteriorDoorBuilder.GroupName);
  if(!group)throw new Exception("HEFU_ATMO_QA_DOOR_GROUP_MISSING");
  var kitchen=group.Find("Door_Kitchen");
  var leaf=kitchen?kitchen.Find("DoorHinge/DoorLeaf"):null;
  if(!kitchen||!leaf||!kitchen.GetComponent<HefuEntryDoor>()||!leaf.GetComponent<Collider>())
   throw new Exception("HEFU_ATMO_QA_KITCHEN_DOOR_LOST_INTERACTION");
  var glass=leaf.GetComponent<Renderer>().sharedMaterial;
  if(!glass||glass.renderQueue<(int)RenderQueue.Transparent||
     glass.color.a>=.45f||glass.GetFloat("_ZWrite")>0.01f)
   throw new Exception("HEFU_ATMO_QA_KITCHEN_NOT_TRANSPARENT");
  int windows=0;
  foreach(var r in house.GetComponentsInChildren<Renderer>(true))
   if(r.name.Contains("CAD_WindowCell_")&&r.name.EndsWith("_glass")){
    if(r.sharedMaterial!=glass)throw new Exception("HEFU_ATMO_QA_WINDOW_NOT_GLASS_"+r.name);
    windows++;
   }
  if(windows!=18)throw new Exception("HEFU_ATMO_QA_WINDOWS="+windows);
  var slider=group.Find("StudySouth_BalconySlidingDoor");
  if(!slider||!slider.GetComponent<HefuSlidingDoor>()||!slider.GetComponent<HefuSlidingDoor>().leaf)
   throw new Exception("HEFU_ATMO_QA_BALCONY_SLIDER_LOST");
  if(!RenderSettings.skybox||!RenderSettings.skybox.mainTexture)
   throw new Exception("HEFU_ATMO_QA_SKYBOX_MISSING");
  var sun=GameObject.Find("BakedLike_Sun_VisualPreview");
  if(!sun||!sun.GetComponent<Light>()||sun.GetComponent<Light>().shadows!=LightShadows.Soft)
   throw new Exception("HEFU_ATMO_QA_SUN_UNSHADOWED");
  var vector=sun.transform.forward;
  if(vector.y>=-.3f||vector.z<=.25f)throw new Exception("HEFU_ATMO_QA_SUN_NOT_FROM_SOUTH");
  var movables=house.GetComponentsInChildren<HefuMovable>(true);
  if(movables.Length!=17)throw new Exception("HEFU_ATMO_QA_MOVABLE_COUNT="+movables.Length);
  if(GameObject.Find("DECOR_Accent_Color_Swatch"))throw new Exception("HEFU_ATMO_QA_OLD_BOX_REAPPEARED");
  Debug.Log("HEFU_ATMOSPHERE_QA_PASS windows="+windows+" transparentKitchen=true"+
    " kitchenDoorInteractive=true balconySliderInteractive=true southTrees="+trees+
    " farBuildings="+buildings+" skybox=true southSun=true noOutdoorColliders=true movables=17");
 }
}
}
