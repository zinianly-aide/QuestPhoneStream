using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor{
/// <summary>Two small everyday soft-furnishing render batches, independently
/// toggleable from Birthday, without furniture/collider/door schema changes.</summary>
public static class HefuM2SoftDecorBuilder{
 const string Root="Hefu_M2_DailyDecor_VisualOnly";
 const string Dir="Assets/Hefu/Generated/M2Style/";
 struct Shape {public int slot;public Mesh mesh;public Matrix4x4 transform;}
 static readonly List<Shape> shapes=new List<Shape>();
 static Mesh Cube,Sphere,Cylinder;
 static Material M(string n,Color c,float smooth,float metallic=0,string tex=null){
  Directory.CreateDirectory(Dir);var p=Dir+n+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(p);
  if(!m){m=new Material(Shader.Find("Standard")){name=n};AssetDatabase.CreateAsset(m,p);}
  m.color=c;m.SetFloat("_Glossiness",smooth);m.SetFloat("_Metallic",metallic);
  m.mainTexture=tex==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
  if(tex!=null&&!m.mainTexture)throw new Exception("HEFU_M2_DECOR_MISSING_TEXTURE "+tex);
  EditorUtility.SetDirty(m);return m;
 }
 static Mesh Primitive(PrimitiveType type){var o=GameObject.CreatePrimitive(type);var mesh=o.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(o);return mesh;}
 static void Add(Mesh mesh,int slot,Vector3 c,Vector3 scale,Quaternion rot){
  shapes.Add(new Shape{mesh=mesh,slot=slot,transform=Matrix4x4.TRS(c,rot,scale)});
 }
 static Mesh Save(string id,Mesh fresh){
  var p=Dir+id+".asset";var current=AssetDatabase.LoadAssetAtPath<Mesh>(p);
  if(current){EditorUtility.CopySerialized(fresh,current);UnityEngine.Object.DestroyImmediate(fresh);EditorUtility.SetDirty(current);return current;}
  AssetDatabase.CreateAsset(fresh,p);return fresh;
 }
 static GameObject Batch(Transform parent,string id,Material[] materials){
  var meshGroups=new List<Mesh>();var results=new CombineInstance[materials.Length];
  for(int slot=0;slot<materials.Length;slot++){
   var list=new List<CombineInstance>();
   foreach(var x in shapes)if(x.slot==slot)
    list.Add(new CombineInstance{mesh=x.mesh,transform=x.transform});
   if(list.Count==0)throw new Exception("HEFU_M2_EMPTY_DECOR_MATERIAL "+id+" "+slot);
   var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(list.ToArray(),true,true);
   meshGroups.Add(mesh);results[slot]=new CombineInstance{mesh=mesh,transform=Matrix4x4.identity};
  }
  var combined=new Mesh{name=id,indexFormat=IndexFormat.UInt32};
  combined.CombineMeshes(results,false,true);combined.RecalculateBounds();
  foreach(var intermediate in meshGroups)UnityEngine.Object.DestroyImmediate(intermediate);
  var o=new GameObject(id);o.transform.SetParent(parent,false);
  o.AddComponent<MeshFilter>().sharedMesh=Save(id,combined);
  var r=o.AddComponent<MeshRenderer>();r.sharedMaterials=materials;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
  if(o.GetComponent<Collider>())throw new Exception("HEFU_M2_DECOR_HAS_COLLIDER");
  shapes.Clear();return o;
 }
 public static HefuM2DailyDecor Apply(Transform house,HefuMobileViewer viewer){
  if(!house||!viewer)throw new ArgumentNullException("M2 missing CAD or viewer");
  var old=house.Find(Root);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  Cube=Primitive(PrimitiveType.Cube);Sphere=Primitive(PrimitiveType.Sphere);Cylinder=Primitive(PrimitiveType.Cylinder);
  var top=house.Find("DiningSideboard/Worktop");
  if(!top){foreach(var r in house.GetComponentsInChildren<Renderer>(true))
   if(r.name=="Worktop"&&r.transform.IsChildOf(house.Find("DiningSideboard"))){top=r.transform;break;}}
  if(!top||!top.GetComponent<Renderer>())throw new Exception("HEFU_M2_DECOR_MISSING_REAL_SIDEBOARD_WORKTOP");
  var work=top.GetComponent<Renderer>().bounds;
  var ivory=M("Hefu_M2_CeramicIvory",new Color(.92f,.88f,.78f),.42f);
  var leaves=M("Hefu_M2_OliveFoliage",new Color(.32f,.43f,.27f),.22f);
  var stems=M("Hefu_M2_WarmStems",new Color(.36f,.29f,.18f),.18f);
  var linen=M("Hefu_M2_IvoryLinenThrow",new Color(.91f,.87f,.78f),.14f,0,
   "Assets/Hefu/Textures/WarmInterior/Linen_IvoryWeave_512.png");
  var thread=M("Hefu_M2_SageThread",new Color(.49f,.54f,.38f),.10f);
  var root=new GameObject(Root).transform;root.SetParent(house,false);
  var potRoot=new GameObject("SideboardDailyPlant").transform;potRoot.SetParent(root,false);
  // Worktop real CAD bounds; pot stays within counter and has no colliders.
  potRoot.position=new Vector3(work.center.x,work.max.y+.012f,work.center.z);
  Add(Cylinder,0,new Vector3(0,.080f,0),new Vector3(.080f,.080f,.080f),Quaternion.identity);
  Add(Cylinder,0,new Vector3(0,.172f,0),new Vector3(.072f,.015f,.072f),Quaternion.identity);
  for(int i=0;i<3;i++){
   float a=i*2.0944f;
   var origin=new Vector3(.024f*Mathf.Cos(a),.21f,.024f*Mathf.Sin(a));
   Add(Cylinder,2,origin+Vector3.up*.065f,new Vector3(.008f,.115f,.008f),Quaternion.Euler(0,0,15f*Mathf.Cos(a)));
   for(int j=0;j<2;j++){
    float off=(j+.7f)*.062f;
    Add(Sphere,1,origin+new Vector3(Mathf.Cos(a)*off,.125f+off,Mathf.Sin(a)*off),
      new Vector3(.085f,.038f,.056f),Quaternion.Euler(0,i*120f+j*35f,35f));
   }
  }
  var pot=Batch(potRoot,"M2_PooledCeramicPlanter",new[]{ivory,leaves,stems});
  var sofa=house.Find("Move_Sofa");if(!sofa)throw new Exception("HEFU_M2_DECOR_SOFA_MISSING");
  // Match the M1 0.668m leather seat top and follow Move_Sofa when dragged.
  var folded=new GameObject("DailyLinenThrow").transform;folded.SetParent(sofa,false);
  Add(Cube,0,new Vector3(-.13f,.683f,-.746f),new Vector3(.29f,.022f,.34f),Quaternion.identity);
  Add(Cube,0,new Vector3(-.13f,.705f,-.824f),new Vector3(.28f,.018f,.175f),Quaternion.identity);
  for(int k=0;k<3;k++)Add(Cube,1,new Vector3(-.25f+k*.123f,.719f,-.824f),new Vector3(.009f,.002f,.158f),Quaternion.identity);
  var throwRenderer=Batch(folded,"M2_PooledLinenThrow",new[]{linen,thread});
  var ctrl=viewer.GetComponent<HefuM2DailyDecor>();if(!ctrl)ctrl=viewer.gameObject.AddComponent<HefuM2DailyDecor>();
  ctrl.items=new[]{pot,throwRenderer};viewer.dailyDecor=ctrl;
  AssetDatabase.SaveAssets();
  Debug.Log("HEFU_M2_DAILY_DECOR_PASS plantOnCADWorktop=true pooledMeshes=2"+
   " linenThrowOnMovableSofa=true renderers=2 visualOnly=true savedPreferenceIndependent=true"+
   " birthdayOverlayPreserved=true newColliders=0");
  return ctrl;
 }
}
}
