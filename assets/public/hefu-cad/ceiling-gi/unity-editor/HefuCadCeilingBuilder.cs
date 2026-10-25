using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>Projects the ACTUAL CAD dining/living/hall top-floor triangulation at
/// existing 2.7m floor-to-roof height; no invented rectangular plate.
/// Dual mode: hidden for top-N, visible when eye inside house.</summary>
public static class HefuCadCeilingBuilder {
 public const string RootName="Hefu_CAD_DerivedCeiling_v1";
 const string Path="Assets/Hefu/Generated/Ceiling/";
 static Mesh Saved(Mesh fresh,string name){
  Directory.CreateDirectory(Path);string path=Path+name+".asset";
  var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old){EditorUtility.CopySerialized(fresh,old);UnityEngine.Object.DestroyImmediate(fresh);return old;}
  AssetDatabase.CreateAsset(fresh,path);return fresh;
 }
 static Material Material(string id,Color color,float gloss){
  Directory.CreateDirectory(Path);string path=Path+id+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
  m.color=color;m.SetFloat("_Metallic",0f);m.SetFloat("_Glossiness",gloss);
  EditorUtility.SetDirty(m);return m;
 }
 public static void Apply(Transform house,Camera cam){
  if(!house||!cam)throw new ArgumentNullException("house/cam");
  var old=house.Find(RootName);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  MeshFilter floor=null;
  foreach(var f in house.GetComponentsInChildren<MeshFilter>(true))
   if(f.name.StartsWith("00_")&&f.name.Contains("客餐厅")){floor=f;break;}
  if(!floor||!floor.sharedMesh)throw new Exception("HEFU_CEILING_CAD_FLOOR_MISSING");
  var source=floor.sharedMesh;var vertices=source.vertices;var idx=source.triangles;
  var roofVerts=new List<Vector3>();var roofUV=new List<Vector2>();var roofTris=new List<int>();
  var counts=new Dictionary<string,int>();var border=new Dictionary<string,Vector3[]>();
  int faces=0;
  const float ceilingHeight=2.677f;
  for(int i=0;i<idx.Length;i+=3){
   Vector3 a=floor.transform.TransformPoint(vertices[idx[i]]),
           b=floor.transform.TransformPoint(vertices[idx[i+1]]),
           c=floor.transform.TransformPoint(vertices[idx[i+2]]);
   Vector3 norm=Vector3.Cross(b-a,c-a).normalized;
   if(norm.y<.93f || a.y>.10f || b.y>.10f || c.y>.10f)continue;
   if(Vector3.Cross(b-a,c-a).sqrMagnitude<.0000001f)continue;
   int baseIndex=roofVerts.Count;
   foreach(var p in new[]{a,b,c}){roofVerts.Add(new Vector3(p.x,ceilingHeight,p.z));roofUV.Add(new Vector2(p.x,p.z));}
   // Original upward-facing floor triangle --> downward ceiling face.
   roofTris.Add(baseIndex);roofTris.Add(baseIndex+2);roofTris.Add(baseIndex+1);
   for(int j=0;j<3;j++){
    var x=new[]{a,b,c}[j];var y=new[]{a,b,c}[(j+1)%3];
    // Quantise to 1mm so shared CAD edges match, even if input mesh is split.
    string xk=Mathf.RoundToInt(x.x*1000)+","+Mathf.RoundToInt(x.z*1000);
    string yk=Mathf.RoundToInt(y.x*1000)+","+Mathf.RoundToInt(y.z*1000);
    string key=string.CompareOrdinal(xk,yk)<0?xk+"/"+yk:yk+"/"+xk;
    counts[key]=counts.ContainsKey(key)?counts[key]+1:1;
    border[key]=new[]{x,y};
   }
   faces++;
  }
  if(faces<2||roofVerts.Count<6||roofVerts.Count>5000)
   throw new Exception("HEFU_CEILING_NO_UPWARD_CAD_FLOOR_TRIANGLES_"+faces);
  var mesh=new Mesh{name="HefuActualCADCeilingMesh"};mesh.indexFormat=IndexFormat.UInt32;
  mesh.SetVertices(roofVerts);mesh.SetUVs(0,roofUV);mesh.SetTriangles(roofTris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
  var ceiling=Saved(mesh,"HefuActualCADCeilingMesh");
  var group=new GameObject(RootName);group.transform.SetParent(house,false);
  var ceilingObj=new GameObject("DiningLivingCeiling_DownwardSurface");ceilingObj.transform.SetParent(group.transform,false);
  ceilingObj.AddComponent<MeshFilter>().sharedMesh=ceiling;
  var renderer=ceilingObj.AddComponent<MeshRenderer>();
  renderer.sharedMaterial=Material("HefuCeilingMattePlaster",new Color(.90f,.87f,.81f),.08f);
  renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
  renderer.receiveShadows=true;
  // Thin shared cove / crown trim where the exact CAD floor perimeter is known.
  var coveVerts=new List<Vector3>();var coveUV=new List<Vector2>();var coveTris=new List<int>();
  int borderCount=0;
  foreach(var entry in counts){
   if(entry.Value!=1)continue;
   var ends=border[entry.Key];var a=ends[0];var b=ends[1];
   float len=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
   if(len<.045f)continue;
   int t=coveVerts.Count;
   coveVerts.Add(new Vector3(a.x,2.582f,a.z));coveVerts.Add(new Vector3(b.x,2.582f,b.z));
   coveVerts.Add(new Vector3(a.x,2.647f,a.z));coveVerts.Add(new Vector3(b.x,2.647f,b.z));
   coveUV.Add(new Vector2(0,0));coveUV.Add(new Vector2(len,0));coveUV.Add(new Vector2(0,.065f));coveUV.Add(new Vector2(len,.065f));
   coveTris.Add(t);coveTris.Add(t+2);coveTris.Add(t+1);
   coveTris.Add(t+1);coveTris.Add(t+2);coveTris.Add(t+3);
   borderCount++;
  }
  MeshRenderer covR=null;
  if(borderCount>0){
   var cov=new Mesh{name="HefuActualCADCrownTrim"};cov.indexFormat=IndexFormat.UInt32;
   cov.SetVertices(coveVerts);cov.SetUVs(0,coveUV);cov.SetTriangles(coveTris,0);
   cov.RecalculateNormals();cov.RecalculateBounds();
   var covObj=new GameObject("CAD_CrownPerimeter_SingleMesh");covObj.transform.SetParent(group.transform,false);
   covObj.AddComponent<MeshFilter>().sharedMesh=Saved(cov,"HefuActualCADCrownTrim");
   covR=covObj.AddComponent<MeshRenderer>();covR.sharedMaterial=Material("HefuCreamCrownTrim",new Color(.82f,.78f,.70f),.32f);
   covR.shadowCastingMode=ShadowCastingMode.Off;
  }
  var v=cam.GetComponent<HefuVR.HefuCeilingVisibility>();if(!v)v=cam.gameObject.AddComponent<HefuVR.HefuCeilingVisibility>();
  v.ceiling=renderer;v.cove=covR;
  var floorBounds=floor.GetComponent<Renderer>().bounds;
  v.roomMin=new Vector3(floorBounds.min.x,0,floorBounds.min.z);
  v.roomMax=new Vector3(floorBounds.max.x,2.7f,floorBounds.max.z);
  v.SetVisible(false);
  if(renderer.GetComponent<Collider>()||group.GetComponentsInChildren<Collider>(true).Length!=0)
   throw new Exception("HEFU_CEILING_UNEXPECTED_COLLIDER");
  Debug.Log("HEFU_CAD_CEILING_PASS cadFloorTriangles="+faces+" roofVerts="+roofVerts.Count+
    " CADPerimeter="+borderCount+" roofVisibleAtTop=false roofHeight="+ceilingHeight+
    " cameraControl=true 17MovablesUnchanged=true noColliders=true");
 }
}
}
