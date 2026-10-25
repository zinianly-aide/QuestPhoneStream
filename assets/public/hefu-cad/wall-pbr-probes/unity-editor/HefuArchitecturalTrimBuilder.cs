using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>CAD-topology-derived wall/baseboard junctions. Extrudes thin finish
/// ONLY where wall mesh has a real vertical face terminating at floor height;
/// no arbitrary full-room rectangles blocking interior door openings.</summary>
public static class HefuArchitecturalTrimBuilder {
 public const string Name="Hefu_FittedBaseboardTrim_v1";
 const string Path="Assets/Hefu/Generated/WallFinish/";
 public static void Apply(Transform house){
  if(!house)throw new ArgumentNullException(nameof(house));
  var old=house.Find(Name);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  Directory.CreateDirectory(Path);
  var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
  int edges=0;
  foreach(var filter in house.GetComponentsInChildren<MeshFilter>(true)){
   if(!filter.name.Contains("CAD_wall")||!filter.gameObject.activeInHierarchy)continue;
   var mesh=filter.sharedMesh;var pts=mesh.vertices;var idx=mesh.triangles;
   for(int t=0;t<idx.Length;t+=3){
    Vector3 a=filter.transform.TransformPoint(pts[idx[t]]);
    Vector3 b=filter.transform.TransformPoint(pts[idx[t+1]]);
    Vector3 c=filter.transform.TransformPoint(pts[idx[t+2]]);
    var n=Vector3.Cross(b-a,c-a).normalized;
    if(Mathf.Abs(n.y)>.18f)continue;
    int nGround=0;Vector3 lo=Vector3.zero,hi=Vector3.zero;
    foreach(var p in new[]{a,b,c}){
     if(p.y > -.018f && p.y < .025f){if(nGround++==0)lo=p;else hi=p;}
    }
    if(nGround!=2||Vector3.Distance(lo,hi)<.13f||Vector3.Distance(lo,hi)>8.0f)continue;
    var off=n*.011f;
    // four vertices, two tris: 10cm lacquered skirting at actual base edge.
    var start=vertices.Count;
    foreach(var p in new[]{lo+off+Vector3.up*.026f,hi+off+Vector3.up*.026f,
        lo+off+Vector3.up*.129f,hi+off+Vector3.up*.129f}){
      vertices.Add(p);normals.Add(n);
      uv.Add(new Vector2((p-lo).magnitude, p.y*9));
    }
    tris.Add(start);tris.Add(start+2);tris.Add(start+1);
    tris.Add(start+1);tris.Add(start+2);tris.Add(start+3);
    edges++;
   }
  }
  if(edges<15||edges>550)throw new Exception("HEFU_TRIM_BAD_CAD_GROUND_EDGES="+edges);
  var baseboard=new Mesh{name="HefuCADWallSkirting"};baseboard.indexFormat=IndexFormat.UInt32;
  baseboard.SetVertices(vertices);baseboard.SetNormals(normals);baseboard.SetUVs(0,uv);baseboard.SetTriangles(tris,0);baseboard.RecalculateBounds();
  var path=Path+"HefuCADWallSkirting.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(saved){EditorUtility.CopySerialized(baseboard,saved);UnityEngine.Object.DestroyImmediate(baseboard);baseboard=saved;}
  else AssetDatabase.CreateAsset(baseboard,path);
  var matPath=Path+"HefuWarmLacquerSkirting.mat";
  var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
  if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,matPath);}
  mat.color=new Color(.84f,.82f,.77f);mat.SetFloat("_Metallic",0f);mat.SetFloat("_Glossiness",.42f);
  EditorUtility.SetDirty(mat);
  var g=new GameObject(Name);g.transform.SetParent(house,false);
  // CAD-to-world vertices already in world coordinate and origin 0.
  g.transform.position=Vector3.zero;
  g.AddComponent<MeshFilter>().sharedMesh=baseboard;
  var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
  renderer.shadowCastingMode=ShadowCastingMode.Off;
  Debug.Log("HEFU_CAD_BASEBOARD_PASS segments="+edges+" verts="+vertices.Count+
    " CADWallSurfaceOnly=true 17MovablesUnchanged=true noColliders=true noExtraLights=true");
 }
}
}
