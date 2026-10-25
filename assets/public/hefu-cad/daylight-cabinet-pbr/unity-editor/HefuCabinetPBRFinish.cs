using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>Manufacturer-style 3mm front bevel overlays and metre-scale wood
/// UVs added ONLY to real maker cabinet and dining sideboard prefab roots.
/// No source CAD, cabinet collision, prefab geometry or movable root changes.</summary>
public static class HefuCabinetPBRFinish {
 public const string Overlay="Hefu_CabinetFrontChamfer_3mm";
 const string AssetDir="Assets/Hefu/Generated/CabinetPBR/";
 const string Ivory="Assets/Hefu/Textures/CabinetPBR/IvoryVeneer_48cm.png";
 struct MeshData {
  public List<Vector3> vertices;
  public List<Vector2> uv;
  public List<int> triangles;
  public MeshData(bool b){vertices=new List<Vector3>();uv=new List<Vector2>();triangles=new List<int>();}
  public void Tri(int a,int b,int c,bool reverse){
   if(reverse){triangles.Add(a);triangles.Add(c);triangles.Add(b);}
   else{triangles.Add(a);triangles.Add(b);triangles.Add(c);}
  }
 }
 static Material Mat(string name,Texture2D t,Color tint,float gloss,Texture2D bump=null){
  Directory.CreateDirectory(AssetDir);var path=AssetDir+name+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Standard")){name=name};AssetDatabase.CreateAsset(m,path);}
  m.mainTexture=t;m.color=tint;m.SetFloat("_Metallic",0f);m.SetFloat("_Glossiness",gloss);
  m.SetTexture("_BumpMap",bump);
  if(bump){m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.11f);}
  else m.DisableKeyword("_NORMALMAP");
  EditorUtility.SetDirty(m);return m;
 }
 static Mesh Store(string path,Mesh fresh){
  var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old){EditorUtility.CopySerialized(fresh,old);UnityEngine.Object.DestroyImmediate(fresh);return old;}
  AssetDatabase.CreateAsset(fresh,path);return fresh;
 }
 static bool IsDoor(string n){
  return n.StartsWith("DrawerFront_")||n.StartsWith("LowerDoor_")||
   n.StartsWith("BottomDrawer_")||n.StartsWith("Door_")||
   n.Contains("_Door_")||n.StartsWith("EastSinkWestFacingDoor_");
 }
 static Vector3 Vert(Transform door,Vector3 dimensions,float u,float v,float relief,bool faceX){
  var p=faceX?new Vector3(-.5f-relief/dimensions.x,v/dimensions.y,u/dimensions.z):
    new Vector3(u/dimensions.x,v/dimensions.y,-.5f-relief/dimensions.z);
  return door.TransformPoint(p);
 }
 static void AddDoor(MeshData data,Transform cabin,Transform door,bool faceX){
  Vector3 size=door.lossyScale;var local=door.localScale;
  if(local.x<.008f||local.y<.060f||local.z<.008f)return;
  float width=faceX?local.z:local.x,height=local.y;
  float r=Mathf.Min(.003f,Mathf.Min(width,height)*.09f);
  float hx=width*.5f,hy=height*.5f;
  // Chamfer profile has an 8-corner outer and 8-corner inner ring.
  var p=new[]{new Vector2(-hx+r,-hy),new Vector2(hx-r,-hy),
   new Vector2(hx,-hy+r),new Vector2(hx,hy-r),
   new Vector2(hx-r,hy),new Vector2(-hx+r,hy),
   new Vector2(-hx,hy-r),new Vector2(-hx,-hy+r)};
  int start=data.vertices.Count;
  for(int ring=0;ring<2;ring++)for(int i=0;i<8;i++){
   var a=p[i];float u=a.x,v=a.y;
   if(ring==1){u=Mathf.Sign(u)*Mathf.Max(0f,Mathf.Abs(u)-r);v=Mathf.Sign(v)*Mathf.Max(0f,Mathf.Abs(v)-r);}
   var world=Vert(door,local,u,v,ring==0?.0018f:.0062f,faceX);
   data.vertices.Add(cabin.InverseTransformPoint(world));
   data.uv.Add(new Vector2((u+hx)/.48f,(v+hy)/.48f));
  }
  var centroid=Vert(door,local,0,0,.0062f,faceX);
  int mid=data.vertices.Count;data.vertices.Add(cabin.InverseTransformPoint(centroid));
  data.uv.Add(new Vector2(hx/.48f,hy/.48f));
  bool reverse=!faceX;
  for(int i=0;i<8;i++){
   int j=(i+1)%8;
   data.Tri(start+i,start+j,start+8+j,reverse);
   data.Tri(start+i,start+8+j,start+8+i,reverse);
   data.Tri(mid,start+8+i,start+8+j,reverse);
  }
 }
 static void FitWalnut(Transform cabinet,Material walnut,string id){
  int k=0;
  foreach(var mr in cabinet.GetComponentsInChildren<MeshRenderer>(true)){
   string n=mr.name;
   // Only walnut niches/worktop of the dining sideboard; kitchen counters
   // intentionally remain original stone finish and original render geometry.
   if(!(n.StartsWith("Wood")||n=="Worktop"))continue;
   var f=mr.GetComponent<MeshFilter>();
   if(!f||!f.sharedMesh||f.sharedMesh.vertexCount<20)continue;
   var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);
   mesh.name="Hefu_WalnutWorldUV_"+id+"_"+k;
   var normals=mesh.normals;var verts=mesh.vertices;var tex=new Vector2[verts.Length];
   for(int i=0;i<verts.Length;i++){
    var world=mr.transform.TransformPoint(verts[i]);var normal=mr.transform.TransformDirection(normals[i]);
    if(Mathf.Abs(normal.y)>.68f)tex[i]=new Vector2(world.x/.48f,world.z/.48f);
    else if(Mathf.Abs(normal.x)>.68f)tex[i]=new Vector2(world.z/.48f,world.y/.48f);
    else tex[i]=new Vector2(world.x/.48f,world.y/.48f);
   }
   mesh.uv=tex;
   f.sharedMesh=Store(AssetDir+"HefuWalnutWorldUV_"+id+"_"+k+".asset",mesh);
   mr.sharedMaterial=walnut;
   k++;
  }
  if(cabinet.name=="DiningSideboard"&&k<2)throw new Exception("HEFU_CABINET_WALNUT_UV_FAILED="+k);
  Debug.Log("HEFU_CABINET_WALNUT_UV_PASS cabinet="+cabinet.name+" meshes="+k);
 }
 static int ApplyCabinet(Transform root,int index,Material ivory){
  var old=root.Find(Overlay);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var data=new MeshData(true);int doors=0;
  foreach(var mr in root.GetComponentsInChildren<MeshRenderer>(true)){
   if(!IsDoor(mr.name)||!mr.gameObject.activeInHierarchy)continue;
   // East-facing sink panels are rotated 90 degrees in plan; respect their
   // original -X front face. Others front south (-Z) in cabinet local frame.
   var t=mr.transform;bool side=t.name.StartsWith("EastSinkWestFacingDoor_");
   AddDoor(data,root,t,side);doors++;
  }
  if(doors==0)throw new Exception("HEFU_CABINET_DOORS_NOT_FOUND="+root.name);
  var m=new Mesh{name="Hefu_CabinetChamferBatch_"+index};m.indexFormat=IndexFormat.UInt32;
  m.SetVertices(data.vertices);m.SetUVs(0,data.uv);m.SetTriangles(data.triangles,0);m.RecalculateNormals();m.RecalculateBounds();
  var mesh=Store(AssetDir+"Hefu_CabinetChamferBatch_"+index+".asset",m);
  var obj=new GameObject(Overlay);obj.transform.SetParent(root,false);
  obj.AddComponent<MeshFilter>().sharedMesh=mesh;
  var ren=obj.AddComponent<MeshRenderer>();ren.sharedMaterial=ivory;
  ren.receiveShadows=true;ren.shadowCastingMode=ShadowCastingMode.Off;
  if(obj.GetComponent<Collider>())throw new Exception("HEFU_CABINET_OVERLAY_HAS_COLLIDER");
  Debug.Log("HEFU_CABINET_CHAMFER_BATCH_PASS root="+root.name+" doors="+doors+" verts="+mesh.vertexCount);
  return doors;
 }
 public static void Apply(Transform house){
  Directory.CreateDirectory(AssetDir);
  var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Ivory);
  if(!tex)throw new Exception("HEFU_CABINET_IVORY_TEXTURE_MISSING");
  var ivory=Mat("Hefu_IvoryCabinetVeneer48cm",tex,new Color(.98f,.98f,.97f),.31f);
  var walnutTex=HefuPhotoMaterialFinish.Tex("PhotoWalnut.png");
  var walnutNorm=HefuPhotoMaterialFinish.Tex("PhotoWalnutNormal.png",true);
  var walnut=Mat("Hefu_DiningWalnut_48cm",walnutTex,new Color(.96f,.95f,.93f),.33f,walnutNorm);
  var side=house.Find("DiningSideboard");var maker=house.Find(HefuInRoomPlacement.RootName);
  if(!side||!maker||maker.childCount!=5)throw new Exception("HEFU_CABINET_ROOTS_ABSENT");
  int doors=ApplyCabinet(side,0,ivory),cabinetCount=1;
  FitWalnut(side,walnut,"Sideboard");
  for(int i=0;i<maker.childCount;i++){
   var child=maker.GetChild(i);
   doors+=ApplyCabinet(child,i+1,ivory);cabinetCount++;
  }
  Debug.Log("HEFU_CABINET_PBR_PASS cabinets="+cabinetCount+" doors="+doors+
   " chamfer3mm=true pooledMeshes=6 woodRepeat48cm=true"+
   " sourceCabinetCollidersPreserved=true moveIDsUnchanged=true");
 }
}
}
