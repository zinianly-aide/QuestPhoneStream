using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HefuVR.Editor {
/// <summary>
/// Opt-in, non-authoritative visual finish for HefuVR Mobile. North is +Unity Z,
/// south balcony/windows face -Unity Z. Does not relocate CAD walls, original
/// doors or furniture, does not add colliders nor change movable identities.
/// Trees/horizon are illustrative placeholders, NOT real user location.
/// </summary>
public static class HefuSceneAtmosphereBuilder {
 public const string RootName="Hefu_OutdoorPlaceholder_v1";
 const string MatDir="Assets/Hefu/Generated/Atmosphere/";
 const string SkyTexture="Assets/Hefu/Textures/OutdoorPreview/Illustrative_LuoyangAutumnSky.png";
 const string ModelDir="Assets/Hefu/Models/OutdoorPreview/";

 static Material Mat(string name,Color color,float smooth=0.45f){
  Directory.CreateDirectory(MatDir);
  string path=MatDir+name+".mat";
  var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!mat){mat=new Material(Shader.Find("Standard")){name=name};AssetDatabase.CreateAsset(mat,path);}
  mat.color=color;
  if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0f);
  if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",smooth);
  EditorUtility.SetDirty(mat);return mat;
 }
 static Material ClearGlass(){
  var mat=Mat("HefuClearArchitecturalGlass",new Color(.78f,.91f,.95f,.22f),.88f);
  // Built-in Standard Fade uses SrcAlpha + OneMinusSrcAlpha;
  // Transparent/Premultiply uses Src=One, so mixing the two caused
  // inconsistent opaque-tinted panes on some Android drivers.
  mat.SetFloat("_Mode",2);
  mat.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
  mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
  mat.SetInt("_ZWrite",0);
  mat.DisableKeyword("_ALPHATEST_ON");mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
  mat.EnableKeyword("_ALPHABLEND_ON");
  mat.SetOverrideTag("RenderType","Transparent");
  mat.renderQueue=(int)RenderQueue.Transparent;
  EditorUtility.SetDirty(mat);return mat;
 }
 static void Box(Transform root,string name,Vector3 p,Vector3 size,Material mat){
  var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;
  obj.transform.SetParent(root,false);obj.transform.localPosition=p;obj.transform.localScale=size;
  obj.GetComponent<Renderer>().sharedMaterial=mat;
  UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
  obj.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
 }
 static void Skybox(){
  var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexture);
  if(!texture)throw new Exception("HEFU_ATMOSPHERE_SKY_MISSING "+SkyTexture);
  var path=MatDir+"HefuIllustrativeSky.mat";
  var sky=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!sky){var shader=Shader.Find("Skybox/Panoramic");
   if(!shader)throw new Exception("HEFU_ATMOSPHERE_PANORAMIC_SHADER_UNAVAILABLE");
   sky=new Material(shader);AssetDatabase.CreateAsset(sky,path);
  }
  sky.SetTexture("_MainTex",texture);
  sky.SetFloat("_Exposure",1.02f);
  sky.SetFloat("_Rotation",0f);
  RenderSettings.skybox=sky;
  RenderSettings.ambientMode=AmbientMode.Flat;
  RenderSettings.ambientLight=new Color(.70f,.677f,.622f);
  RenderSettings.fog=true;
  RenderSettings.fogColor=new Color(.76f,.745f,.69f);
  RenderSettings.fogMode=FogMode.Linear;
  RenderSettings.fogStartDistance=18f;
  RenderSettings.fogEndDistance=62f;
  EditorUtility.SetDirty(sky);
 }
 static void Glass(Transform house){
  var mat=ClearGlass();
  // CAD south wall contains TWO triple-glazed units (#21–23 and #27–29)
  // separated by ~8cm. Rendering three blue-tinted panes together made the
  // window look opaque and hid trees/sky beyond the glass. Preserve all CAD
  // meshes and colliders, but render ONLY the center sheet of each triple.
  var south=Mat("HefuClearSouthWindow",new Color(.97f,.99f,1f,.075f),.30f);
  south.SetFloat("_Mode",2);
  south.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
  south.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
  south.SetInt("_ZWrite",0);
  south.DisableKeyword("_ALPHATEST_ON");south.DisableKeyword("_ALPHAPREMULTIPLY_ON");
  south.EnableKeyword("_ALPHABLEND_ON");
  south.SetOverrideTag("RenderType","Transparent");south.renderQueue=(int)RenderQueue.Transparent;
  EditorUtility.SetDirty(south);
  int windows=0, southVisible=0, southHidden=0;
  foreach(var r in house.GetComponentsInChildren<Renderer>(true)){
   if(!r.name.Contains("CAD_WindowCell_")||!r.name.EndsWith("_glass"))continue;
   bool southWindow=r.name.StartsWith("21_CAD_")||r.name.StartsWith("22_CAD_")||r.name.StartsWith("23_CAD_")||
    r.name.StartsWith("27_CAD_")||r.name.StartsWith("28_CAD_")||r.name.StartsWith("29_CAD_");
   bool keepSouth=r.name.StartsWith("22_CAD_")||r.name.StartsWith("28_CAD_");
   r.sharedMaterial=southWindow?south:mat;
   r.enabled=!southWindow||keepSouth;
   r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
   if(southWindow){if(keepSouth)southVisible++;else southHidden++;}
   windows++;
  }
  if(windows!=18||southVisible!=2||southHidden!=4)
   throw new Exception("HEFU_SOUTH_GLASS_COUNT_INVALID "+windows+"/"+southVisible+"/"+southHidden);
  Debug.Log("HEFU_SOUTH_GLASS_CLEAR_PASS southUnits=2 visibleLayers=2 hiddenDuplicateLayers=4"+
   " singlePaneAlpha=.075 originalCADMeshesPreserved=true");
  // Kitchen door is an openable original doorway on CAD wall 26, NOT the north
  // entry door. Replace its leaf paint with transparent glass, retain hinge,
  // collider and HefuEntryDoor component (selection/opening still works).
  var kitchen=house.Find(HefuInteriorDoorBuilder.GroupName+"/Door_Kitchen");
  var leaf=kitchen?kitchen.Find("DoorHinge/DoorLeaf"):null;
  if(!kitchen||!leaf||!kitchen.GetComponent<HefuVR.HefuEntryDoor>())
   throw new Exception("HEFU_ATMOSPHERE_KITCHEN_DOOR_NOT_READY");
  var body=leaf.GetComponent<Renderer>();body.sharedMaterial=mat;
  body.shadowCastingMode=ShadowCastingMode.Off;body.receiveShadows=false;
  var hinge=leaf.parent;
  var black=Mat("KitchenGlazingGraphite",new Color(.12f,.13f,.14f),.38f);
  // Hinge leaf is .771 m across, spans along -Z in this CAD orientation.
  float w=leaf.localScale.z;
  for(int i=0;i<2;i++){
   float z=i==0?-.017f:-w+.017f;
   Box(hinge,"KitchenGlassVerticalRail_"+i,new Vector3(0,1.015f,z),
    new Vector3(.048f,2.025f,.032f),black);
  }
  foreach(float y in new[]{.024f,2.006f})
   Box(hinge,"KitchenGlassHorizontalRail_"+y,new Vector3(0,y,-w*.5f),
    new Vector3(.048f,.035f,w),black);
  // Study/balcony sliding-glass leaves, without touching their leaf transform.
  int sliding=0;
  foreach(var r in house.GetComponentsInChildren<Renderer>(true)){
   if(!r.name.Equals("Glass")||!r.transform.IsChildOf(house.Find(HefuInteriorDoorBuilder.GroupName)))continue;
   if(r.transform.parent.name!="WestFixedPanel"&&r.transform.parent.name!="EastSlidingPanel")continue;
   r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;sliding++;
  }
  if(sliding!=2)throw new Exception("HEFU_ATMOSPHERE_SLIDER_GLASS_COUNT="+sliding);
  Debug.Log("HEFU_ATMOSPHERE_GLASS_PASS windows="+windows+" kitchenLeaf=true balconySlider="+sliding);
 }
 static void Outdoor(Transform house){
  var old=house.Find(RootName);
  if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var root=new GameObject(RootName).transform;
  root.SetParent(house,false);
  root.position=Vector3.zero;
  var distant=Mat("HefuExteriorHazeBuildings",new Color(.59f,.59f,.55f),.11f);
  var far=Mat("HefuExteriorFarBuildings",new Color(.73f,.70f,.62f),.14f);
  // Buildings at least ~25m SOUTH of south windows. Horizon is illustrative.
  float[] xx={-20.8f,-17.5f,-14.4f,-11.9f,-8.0f,-4.0f,-.5f,3.0f,6.5f};
  float[] hh={7f,9.3f,6.2f,11f,8.4f,10f,5.4f,8f,7.2f};
  for(int i=0;i<xx.Length;i++){
   float h=hh[i];
   Box(root,"Illustrative_DistantBuilding_"+i,new Vector3(xx[i],h*.5f,-40f-i%3*3),
      new Vector3(2.1f+(i%3)*.32f,h,1.2f),i%2==0?far:distant);
  }
  // Shallow, synthetic late-autumn ground beyond the south-facing windows.
  // The inside CAD flooring and balcony tiles remain untouched. One renderer,
  // no collider, no realtime shadow map pass: mobile-safe horizon depth cue.
  var earth=Mat("HefuAutumnEarthGround",Color.white,.04f);
  const string earthPath="Assets/Hefu/Textures/OutdoorPreview/Illustrative_AutumnGround.png";
  earth.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(earthPath);
  if(!earth.mainTexture)throw new Exception("HEFU_AUTUMN_GROUND_TEXTURE_MISSING");
  earth.mainTextureScale=new Vector2(16f,12f);
  EditorUtility.SetDirty(earth);
  Box(root,"Illustrative_AutumnGround",new Vector3(-7.0f,-.16f,-38f),
      new Vector3(86f,.05f,53f),earth);
  // Blender 4.5 low-poly tree assets. Limited to 5 without realtime shadows,
  // external to south glazing Z≈-10.0; no colliders/navigation obstacles.
  Vector3[] locations={
   new Vector3(-14.5f,0,-15.9f),new Vector3(-10.9f,0,-18.4f),
   new Vector3(-6.8f,0,-16.9f),new Vector3(-3.3f,0,-19.5f),
   new Vector3(.7f,0,-17.2f)};
  for(int i=0;i<locations.Length;i++){
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir+"OutdoorOliveTree_"+"ABC"[i%3]+".fbx");
   if(!source)throw new Exception("HEFU_ATMOSPHERE_TREE_FBX_MISSING index="+i);
   var instance=PrefabUtility.InstantiatePrefab(source) as GameObject;
   if(!instance)throw new Exception("HEFU_ATMOSPHERE_TREE_INSTANTIATION_FAILED");
   instance.name="Illustrative_Tree_"+i;
   instance.transform.SetParent(root,false);
   instance.transform.position=locations[i];
   // Blender FBX prefab uses import scale 100. Previous scene accidentally
   // overwrote it with ~1, shrinking trees by ~100x and leaving blue windows.
   // Always multiply, NEVER replace, the imported prefab scale.
   instance.transform.localScale=instance.transform.localScale*(.86f+(i%3)*.16f);
   // Preserve Blender FBX Z-up conversion! Forcing a world yaw alone made
   // these trees lie on their sides, with their canopy at floor level.
   instance.transform.rotation=Quaternion.Euler(0,i*67f,0)*instance.transform.rotation;
   // Stable autumn foliage variation: material-slot recoloring retains the
   // original Blender geometry and trunk material, without photos/downloads.
   // Some trees remain olive-green, others show yellow/ochre/bronze leaves.
   Color[] autumn={
    new Color(.37f,.43f,.25f),new Color(.58f,.48f,.23f),
    new Color(.73f,.55f,.26f),new Color(.48f,.30f,.18f),
    new Color(.29f,.38f,.24f),new Color(.69f,.41f,.22f)};
   int foliageSlots=0;
   foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)){
    var original=renderer.sharedMaterials;
    var replacements=new Material[original.Length];
    for(int slot=0;slot<original.Length;slot++){
     var current=original[slot];
     if(current && current.name.Contains("Foliage_")){
      int variant=(slot+i*2)%autumn.Length;
      replacements[slot]=Mat("LuoyangAutumnFoliage_"+variant,autumn[variant],.12f);
      foliageSlots++;
     }else replacements[slot]=current;
    }
    renderer.sharedMaterials=replacements;
    renderer.shadowCastingMode=ShadowCastingMode.Off;
    renderer.receiveShadows=false;
   }
   if(foliageSlots<4)throw new Exception("HEFU_LUOYANG_AUTUMN_FOLIAGE_MISSING_TREE_"+i+"="+foliageSlots);
   foreach(var c in instance.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
  }
  Debug.Log("HEFU_ATMOSPHERE_OUTDOOR_PASS illustrativeTrees=5 distantBuildings=9 southZ<-13 noColliders=true");
 }
 static void Light(){
  var sun=GameObject.Find("BakedLike_Sun_VisualPreview");
  if(!sun)throw new Exception("HEFU_ATMOSPHERE_SUN_NOT_FOUND");
  var s=sun.GetComponent<Light>();
  if(!s||s.type!=LightType.Directional)throw new Exception("HEFU_ATMOSPHERE_SUN_INVALID");
  // The south-facing windows see a late-October LUOYANG afternoon sun,
  // not an artist-guessed 3D vector. Azimuth measured clockwise from north;
  // Unity +X is east, +Z is north; afternoon azimuth >180 gives negative
  // world X (sun to the west) and negative Z (sun to the south).
  var astronomy=HefuLuoyangSolar.Default();
  if(astronomy.elevation<=0)throw new Exception("HEFU_LUOYANG_AUTUMN_SUN_BELOW_HORIZON");
  sun.transform.rotation=Quaternion.LookRotation(-astronomy.toSun);
  s.color=new Color(1f,.906f,.764f);
  s.intensity=.91f;
  s.shadows=LightShadows.Soft;
  s.shadowStrength=.58f;
  QualitySettings.shadowDistance=11f;
  QualitySettings.shadowResolution=ShadowResolution.Medium;
  QualitySettings.pixelLightCount=2;
  Debug.Log("HEFU_LUOYANG_AUTUMN_SUN_PASS year=2026 month=10 day=25 hour=14.5 CST elevation="+
   astronomy.elevation.ToString("F2")+" azimuth="+astronomy.azimuth.ToString("F2")+
   " coordinates=34.6836N,112.4536E physicalSolarDirection=true weatherSimulated=true");
  Debug.Log("HEFU_ATMOSPHERE_LIGHT_PASS southDaylight=true oneShadowedSun=true ambient=warm skybox=illustrative");
 }
 public static void Apply(Transform house){
  if(!house)throw new ArgumentNullException(nameof(house));
  AssetDatabase.Refresh();Directory.CreateDirectory(MatDir);
  Skybox();Glass(house);Outdoor(house);Light();
  Debug.Log("HEFU_ATMOSPHERE_PASS noCADSourceChanges=true noFurnitureProtocolChanges=true");
 }
}
}
