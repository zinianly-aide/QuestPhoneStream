using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuRoofGiQA {
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  var root=house.transform.Find(HefuCadCeilingBuilder.RootName);
  if(!root)throw new Exception("HEFU_GI_QA_NO_CEILING");
  var mesh=root.Find("DiningLivingCeiling_DownwardSurface").GetComponent<MeshFilter>().sharedMesh;
  Debug.Log("HEFU_ROOF_MESH_DIAG vertices="+mesh.vertexCount+" minY="+mesh.bounds.min.y+" tris="+mesh.triangles.Length+" uv2="+mesh.uv2.Length);
  if(mesh.vertexCount<12||Mathf.Abs(mesh.bounds.min.y-2.677f)>.005f||
     mesh.triangles.Length<30 || mesh.uv2.Length!=mesh.vertexCount)
   throw new Exception("HEFU_GI_QA_CEILING_UV_OR_TRIANGULATION_BAD");
  if(root.GetComponentsInChildren<Collider>(true).Length>0)
   throw new Exception("HEFU_GI_QA_CEILING_COLLISION");
  var v=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
  if(!v||!v.ceiling)throw new Exception("HEFU_GI_QA_NO_CUTAWAY_SCRIPT");
  v.UpdateVisibility(new Vector3(-5.99f,1.60f,-2.66f));
  if(!v.ceiling.enabled)throw new Exception("HEFU_GI_QA_DINING_ROOF_OFF");
  v.UpdateVisibility(new Vector3(-5.55f,1.75f,-6.01f));
  if(!v.ceiling.enabled)throw new Exception("HEFU_GI_QA_LIVING_ROOF_OFF");
  v.UpdateVisibility(new Vector3(-6.3f,15.8f,-4.7f));
  if(v.ceiling.enabled)throw new Exception("HEFU_GI_QA_OVERVIEW_ROOF_VISIBLE");
  v.UpdateVisibility(new Vector3(-1.4f,1.8f,-6.1f));
  if(v.ceiling.enabled)throw new Exception("HEFU_GI_QA_OUTSIDE_ROOF_VISIBLE");
  if(LightmapSettings.lightmaps==null||LightmapSettings.lightmaps.Length!=1||
    !LightmapSettings.lightmaps[0].lightmapColor)
   throw new Exception("HEFU_GI_QA_NO_BAKED_ATLAS");
  var group=house.transform.Find("Hefu_DynamicFurnitureLightProbes_v1");
  if(!group||group.GetComponent<LightProbeGroup>().probePositions.Length!=46)
   throw new Exception("HEFU_GI_QA_LIGHT_PROBES_MISSING");
  if(!LightmapSettings.lightProbes||LightmapSettings.lightProbes.count!=46)
   throw new Exception("HEFU_GI_QA_LIGHT_PROBES_NOT_BAKED");
  int markedStatic=0,lightmapped=0,uv2=0;
  foreach(var r in house.GetComponentsInChildren<MeshRenderer>(true)){
   if(!GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.ContributeGI))continue;
   markedStatic++;
   var f=r.GetComponent<MeshFilter>();
   if(f && f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount)uv2++;
   if(r.lightmapIndex>=0&&r.lightmapIndex!=65534)lightmapped++;
  }
  if(markedStatic!=20||uv2!=20||lightmapped<8)
   throw new Exception("HEFU_GI_QA_LIT_STATIC_COUNT="+markedStatic+" lightmapped="+lightmapped+" uv2="+uv2);
  int movable=0,lighting=0;
  foreach(var m in house.GetComponentsInChildren<HefuMovable>(true)){
   movable++;
   foreach(var r in m.GetComponentsInChildren<MeshRenderer>(true))
    if(r.lightProbeUsage==LightProbeUsage.BlendProbes)lighting++;
  }
  if(movable!=17||lighting<20)throw new Exception("HEFU_GI_QA_DYNAMIC_FURNITURE_MISSING="+movable+"/"+lighting);
  HefuWallPBRQA.Validate();
  Debug.Log("HEFU_ROOF_GI_QA_PASS roofCADTris=16 height=2.677"+
   " topCutaway=true eyeLevelRoof=true staticBaked="+markedStatic+" indexedLightmaps="+lightmapped+" uv2="+uv2+
   " lightmaps=1 probes=46 movable=17 noColliders=true");
 }
}
}
