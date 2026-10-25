using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
/// <summary>Controlled low-resolution GI experiment on GENERATED Mobile scene.
/// Only CAD shell is static. Furniture, doors, curtain, glass, controls remain
/// dynamic/unmodified. One 2.7m roof and 24+ probes, low-resolution CPU bake.</summary>
public static class HefuStaticGiBake {
 const string Path="Assets/Hefu/Generated/GI/";
 static bool OnFloor(Vector2 pos,MeshFilter floor){
  var v=floor.sharedMesh.vertices;var tris=floor.sharedMesh.triangles;
  for(int j=0;j<tris.Length;j+=3){
   var a=floor.transform.TransformPoint(v[tris[j]]);
   var b=floor.transform.TransformPoint(v[tris[j+1]]);
   var c=floor.transform.TransformPoint(v[tris[j+2]]);
   if(Vector3.Cross(b-a,c-a).y<.90f*(b-a).magnitude*(c-a).magnitude)continue;
   Vector2 p=new Vector2(pos.x,pos.y),aa=new Vector2(a.x,a.z),bb=new Vector2(b.x,b.z),cc=new Vector2(c.x,c.z);
   float s=(bb.x-aa.x)*(p.y-aa.y)-(bb.y-aa.y)*(p.x-aa.x);
   float t=(cc.x-bb.x)*(p.y-bb.y)-(cc.y-bb.y)*(p.x-bb.x);
   float u=(aa.x-cc.x)*(p.y-cc.y)-(aa.y-cc.y)*(p.x-cc.x);
   if((s>=-.00001f&&t>=-.00001f&&u>=-.00001f)||(s<=.00001f&&t<=.00001f&&u<=.00001f))return true;
  }
  return false;
 }
 public static void Prepare(Transform house){
  Directory.CreateDirectory(Path);
  var roof=house.Find(HefuCadCeilingBuilder.RootName+"/DiningLivingCeiling_DownwardSurface");
  if(!roof)throw new Exception("HEFU_GI_ROOF_REQUIRED");
  MeshFilter floor=null;
  int statics=0,uv2=0;
  foreach(var f in house.GetComponentsInChildren<MeshFilter>(true)){
   if(f.name.StartsWith("00_")&&f.name.Contains("客餐厅"))floor=f;
   bool fixedCAD=f.name.Contains("CAD_wall") ||
     (f.name.StartsWith("00_")&&f.name.Contains("客餐厅")) ||
     f.transform==roof;
   if(!fixedCAD)continue;
   // 35 and 42 are duplicate tiny CAD wall shells occupying exactly the same
   // bounds. Baking both causes physical UV texel overlap despite unwrap.
   // Keep the geometry and collisions, exclude ONLY these two from lightmaps.
   if(f.name=="35_CAD_wall_35"||f.name=="42_CAD_wall_42"){
    GameObjectUtility.SetStaticEditorFlags(f.gameObject,(StaticEditorFlags)0);
    continue;
   }
   var renderer=f.GetComponent<MeshRenderer>();
   if(!renderer||!f.sharedMesh)throw new Exception("HEFU_GI_STATIC_CAD_RENDERER_MISSING_"+f.name);
   Mesh mesh=f.sharedMesh;
   // Regenerate explicit high-padding UV2 every bake to eliminate overlap
   // from old auto-unwraps. Do not overwrite PBR UV0 or the source CAD mesh.
   UnwrapParam unwrap;
   UnwrapParam.SetDefaults(out unwrap);
   unwrap.packMargin=.09f;unwrap.angleError=.08f;unwrap.areaError=.05f;
   if(!Unwrapping.GenerateSecondaryUVSet(mesh,unwrap))
     throw new Exception("HEFU_GI_SECONDARY_UV_FAILED_"+mesh.name);
   EditorUtility.SetDirty(mesh);uv2++;
   GameObjectUtility.SetStaticEditorFlags(f.gameObject,StaticEditorFlags.ContributeGI);
   renderer.receiveShadows=true;renderer.scaleInLightmap=.7f;
   statics++;
  }
  if(floor==null||statics!=20)throw new Exception("HEFU_GI_STATIC_COUNT_"+statics);
  var existing=house.Find("Hefu_DynamicFurnitureLightProbes_v1");
  if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);
  var probeGo=new GameObject("Hefu_DynamicFurnitureLightProbes_v1");
  probeGo.transform.SetParent(house,false);
  var probe=probeGo.AddComponent<LightProbeGroup>();
  var positions=new List<Vector3>();
  for(float x=-8.00f;x<=-4.04f;x+=.49f)
   for(float z=-7.74f;z<=-.50f;z+=.49f)
    if(OnFloor(new Vector2(x,z),floor)){
     positions.Add(new Vector3(x,.65f,z));
     positions.Add(new Vector3(x,1.75f,z));
    }
  if(positions.Count<30||positions.Count>140)throw new Exception("HEFU_GI_PROBE_COUNT_"+positions.Count);
  probe.probePositions=positions.ToArray();
  int dynamicRenderers=0;
  foreach(var movable in house.GetComponentsInChildren<HefuMovable>(true))
   foreach(var renderer in movable.GetComponentsInChildren<MeshRenderer>(true)){
    renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
    dynamicRenderers++;
   }
  if(dynamicRenderers<20)throw new Exception("HEFU_GI_FURNITURE_RENDERER_COUNT="+dynamicRenderers);
  // Unity bake geometry uses a visible roof (but this is a persistent renderer
  // toggle, so temporarily show roof, then disable for overview at runtime).
  roof.GetComponent<Renderer>().enabled=true;
  var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(Path+"HefuMobileGI.lighting");
  if(!settings){settings=new LightingSettings();AssetDatabase.CreateAsset(settings,Path+"HefuMobileGI.lighting");}
  settings.bakedGI=true;settings.realtimeEnvironmentLighting=false;
  settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
  settings.lightmapResolution=7f;settings.lightmapMaxSize=512;settings.lightmapPadding=4;
  settings.directSampleCount=32;settings.indirectSampleCount=96;
  settings.environmentSampleCount=32;
  settings.indirectScale=1.4f;
  Lightmapping.lightingSettings=settings;
  EditorUtility.SetDirty(settings);
  var sun=GameObject.Find("BakedLike_Sun_VisualPreview");
  if(sun&&sun.GetComponent<Light>())sun.GetComponent<Light>().lightmapBakeType=LightmapBakeType.Mixed;
  foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())
   if(light.name.StartsWith("PendantSpot_")||light.name=="FloorLampPoint_InsideShade")
    light.lightmapBakeType=LightmapBakeType.Mixed;
  AssetDatabase.SaveAssets();
  EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
  EditorSceneManager.SaveOpenScenes();
  Debug.Log("HEFU_GI_SETUP_PASS staticRenderers="+statics+" generatedSecondaryUV="+uv2+
    " probes="+positions.Count+" samples=32/96 atlasMax=512 lightmapDensity=7"+
    " duplicateWallsExcluded=2 dynamicFurnitureLightProbeBlend=true roofVisibleDuringBake=true");
 }
 public static void Bake(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  BakeCurrent(house);
 }
 public static void BakeCurrent(GameObject house){
  if(!house)throw new Exception("HEFU_GI_SCENE_NOT_OPEN");
  Prepare(house.transform);
  var started=DateTime.UtcNow;
  bool ok=Lightmapping.Bake();
  Debug.Log("HEFU_GI_BAKE_RESULT "+ok+" seconds="+(DateTime.UtcNow-started).TotalSeconds.ToString("F1")+
    " atlasCount="+LightmapSettings.lightmaps.Length+
    " probeCount="+(LightmapSettings.lightProbes!=null?LightmapSettings.lightProbes.count:0));
  if(!ok||LightmapSettings.lightmaps.Length==0)throw new Exception("HEFU_GI_BAKE_ZERO_LIGHTMAPS");
  EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
  Debug.Log("HEFU_GI_BAKE_PASS realtimeGI=false mobileStaticOnly=true dynamicFurnitureProbes=true");
 }
}
}
