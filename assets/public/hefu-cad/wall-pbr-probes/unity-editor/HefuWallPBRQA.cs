using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuWallPBRQA {
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  if(!house)throw new Exception("HEFU_PBR_QA_NO_HOUSE");
  int walls=0,uvVertices=0;
  foreach(var r in house.GetComponentsInChildren<MeshRenderer>(true)){
   if(!r.name.Contains("CAD_wall"))continue;
   var filter=r.GetComponent<MeshFilter>();
   if(!filter||filter.sharedMesh.uv.Length!=filter.sharedMesh.vertexCount||
     filter.sharedMesh.vertexCount==0||!r.sharedMaterial||
     !r.sharedMaterial.mainTexture||!r.sharedMaterial.GetTexture("_BumpMap")||
     !r.sharedMaterial.IsKeywordEnabled("_NORMALMAP"))
     throw new Exception("HEFU_PBR_QA_UNMAPPED_WALL_"+r.name);
   if(!r.GetComponent<Collider>())throw new Exception("HEFU_PBR_QA_COLLIDER_LOST_"+r.name);
   walls++;uvVertices+=filter.sharedMesh.vertexCount;
  }
  if(walls!=20)throw new Exception("HEFU_PBR_QA_WALL_COUNT="+walls);
  var baseboard=house.transform.Find(HefuArchitecturalTrimBuilder.Name);
  if(!baseboard||baseboard.GetComponent<MeshFilter>().sharedMesh.vertexCount<300||
    baseboard.GetComponentsInChildren<Collider>(true).Length>0)
    throw new Exception("HEFU_PBR_QA_BASEBOARD_MISSING_OR_COLLIDING");
  var mesh=baseboard.GetComponent<MeshFilter>().sharedMesh;
  if(mesh.bounds.min.y<.025f||mesh.bounds.max.y>.131f)
   throw new Exception("HEFU_PBR_QA_TRIM_BOUNDS "+mesh.bounds);
  var reflections=house.transform.Find(HefuBakedRoomReflections.Root);
  if(!reflections||reflections.GetComponentsInChildren<ReflectionProbe>(true).Length!=2)
   throw new Exception("HEFU_PBR_QA_BAKED_PROBES_MISSING");
  foreach(var rp in reflections.GetComponentsInChildren<ReflectionProbe>(true)){
   if(rp.mode!=ReflectionProbeMode.Custom||rp.resolution!=64||
       !rp.customBakedTexture||!rp.boxProjection||rp.intensity>.55f)
    throw new Exception("HEFU_PBR_QA_REALTIME_OR_UNBAKED_PROBE_"+rp.name);
  }
  var style=UnityEngine.Object.FindObjectOfType<HefuDecorSwitcher>();
  if(!style||style.wallRenderers.Length!=20)
   throw new Exception("HEFU_PBR_QA_STYLE_WALL_LINKS_LOST");
  style.preset=0;style.Apply();
  if(!style.wallRenderers[0].material.mainTexture||
     style.wallRenderers[0].material.color.r<.90f)
    throw new Exception("HEFU_PBR_QA_STYLE_REMOVES_PLASTER");
  style.preset=2;style.Apply();
  if(!style.wallRenderers[0].material.mainTexture||
     style.wallRenderers[0].material.color.b<=style.wallRenderers[0].material.color.r)
    throw new Exception("HEFU_PBR_QA_COOL_STYLE_REGRESSION");
  int furniture=house.GetComponentsInChildren<HefuMovable>(true).Length;
  if(furniture!=17)throw new Exception("HEFU_PBR_QA_FURNITURE_COUNT_"+furniture);
  Debug.Log("HEFU_WALL_PBR_QA_PASS walls=20 verts="+uvVertices+" wallNormal=true"+
   " baseboardOneMesh=true baked64x2=true styleSwitchPreserved=true CADCollisionPreserved=true furniture=17");
 }
}
}
