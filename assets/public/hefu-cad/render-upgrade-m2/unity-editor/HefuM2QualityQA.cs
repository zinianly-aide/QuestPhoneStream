using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor{
public static class HefuM2QualityQA{
 static Material Get(string n){var m=AssetDatabase.LoadAssetAtPath<Material>(n);if(!m)throw new Exception("HEFU_M2_QA_MATERIAL_MISSING_"+n);return m;}
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2").transform;
  var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
  if(!viewer||!viewer.dailyDecor||viewer.dailyDecor.items==null||viewer.dailyDecor.items.Length!=2)
   throw new Exception("HEFU_M2_QA_DAILY_CONTROLLER_MISSING");
  var d=viewer.dailyDecor;
  foreach(var g in d.items){
   var r=g.GetComponent<MeshRenderer>();var m=g.GetComponent<MeshFilter>();
   if(!r||!m||!m.sharedMesh||m.sharedMesh.vertexCount<50||g.GetComponent<Collider>()||
      g.GetComponentsInChildren<Light>(true).Length>0||r.shadowCastingMode!=ShadowCastingMode.Off)
    throw new Exception("HEFU_M2_QA_DECOR_BATCH_INVALID_"+g.name);
  }
  var sofa=house.Find("Move_Sofa");
  if(!d.items[1].transform.IsChildOf(sofa)||d.items[0].transform.IsChildOf(sofa))
   throw new Exception("HEFU_M2_QA_DECOR_PARENTING");
  var layout=sofa.position;var origPlayerPref=PlayerPrefs.GetInt("Hefu_M2_DailyDecorVisible_v1",-1);
  d.SetVisible(false);foreach(var g in d.items)if(g.activeSelf)throw new Exception("HEFU_M2_QA_DAILY_OFF");
  d.SetVisible(true);foreach(var g in d.items)if(!g.activeSelf)throw new Exception("HEFU_M2_QA_DAILY_ON");
  if(sofa.position!=layout||house.GetComponentsInChildren<HefuMovable>(true).Length!=17)
   throw new Exception("HEFU_M2_QA_FURNITURE_MOVED");
  var startBirthdayPref=PlayerPrefs.GetInt("HefuVR_Birthday_B23_Enabled_v1",-1);
  var oldBirthday=house.GetComponentsInChildren<HefuBirthdayScene>(true);
  if(oldBirthday.Length!=0)throw new Exception("HEFU_M2_QA_BIRTHDAY_SCENE_BAKED_IN");
  var birthday=HefuBirthdayScene.Build(house);
  if(birthday.AnchorCount!=5||birthday.DecorationCount<115)
   throw new Exception("HEFU_M2_QA_BIRTHDAY_BROKEN");
  d.SetVisible(false);
  if(birthday.DecorationCount<115||d.items[0].activeSelf||d.items[1].activeSelf)
   throw new Exception("HEFU_M2_QA_DAILY_BIRTHDAY_NOT_INDEPENDENT");
  birthday.ClearDecorations();UnityEngine.Object.DestroyImmediate(birthday.gameObject);
  d.SetVisible(true);
  if(PlayerPrefs.GetInt("HefuVR_Birthday_B23_Enabled_v1",-1)!=startBirthdayPref||
    PlayerPrefs.GetInt("Hefu_M2_DailyDecorVisible_v1",-1)!=origPlayerPref)
   throw new Exception("HEFU_M2_QA_PREFERENCES_MUTATED");
  var folder="Assets/Hefu/Generated/FurnitureDetailAssets/";
  var floor=Get(folder+"PhotoPorcelainFloor.mat");
  var walnut=Get(folder+"WalnutCoffeeTable_0.mat");
  var leather=Get(folder+"SofaUpholstery_0.mat");
  if(floor.GetFloat("_Glossiness")>.37f||floor.GetFloat("_Glossiness")<.28f||
     leather.GetFloat("_BumpScale")<.27f||!leather.IsKeywordEnabled("_NORMALMAP")||
     !walnut.GetTexture("_BumpMap")||Mathf.Abs(walnut.GetFloat("_Glossiness")-.29f)>.005f)
   throw new Exception("HEFU_M2_QA_PBR_FINISH");
  var cabinet=Get("Assets/Hefu/Generated/CabinetPBR/Hefu_DiningWalnut_48cm.mat");
  if(!cabinet.GetTexture("_BumpMap")||cabinet.GetFloat("_BumpScale")<.16f)
   throw new Exception("HEFU_M2_QA_CABINET_WORLD_SCALE");
  var glass=Get("Assets/Hefu/Generated/Atmosphere/HefuClearSouthWindow.mat");
  if(glass.renderQueue<3000||glass.color.a>.06f||glass.GetFloat("_ZWrite")>0.01f)
   throw new Exception("HEFU_M2_QA_GLASS_TRANSPARENCY_REGRESSION");
  var light=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!light||!light.HasBakedPair||LightmapSettings.lightmaps.Length!=1||
     LightmapSettings.lightmaps[0].lightmapColor.width<128||LightmapSettings.lightProbes.count!=46)
   throw new Exception("HEFU_M2_QA_DUAL_GI_REGRESSION");
  var hero=HefuPhotoViewpoints.All;
  if(hero.Length!=10||hero[9].id!="hero_living_dining")
   throw new Exception("HEFU_M2_QA_HERO_CAMERA_LOST");
  Debug.Log("HEFU_M2_QA_PASS floorPBR=true walnutPBR=true leatherBump=true cabinetNormal=true"+
   " glassTransparent=true dailyDecorRenderers=2 noExtraColliders=true sofaChildFollowsMovable=true"+
   " dailyOnOff=true birthdayIndependent=true birthdayAnchors=5 savedPrefsUntouched=true"+
   " moveIDs=17 views=10 atlas="+LightmapSettings.lightmaps[0].lightmapColor.width+" probes=46");
 }
}
}
