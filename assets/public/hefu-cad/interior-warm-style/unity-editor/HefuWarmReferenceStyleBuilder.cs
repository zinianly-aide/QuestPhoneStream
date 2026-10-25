using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>Visual-only P4 warm minimal decor: two dining pendants and recessed
/// side curtain panels at CAD south glazing. No colliders, no added lights,
/// no movable furniture or CAD remodelling. Revert by deleting one child root.</summary>
public static class HefuWarmReferenceStyleBuilder {
 public const string GroupName="Hefu_WarmReferenceStyle_v1";
 const string Art="Assets/Hefu/Generated/WarmReferenceStyle/";
 const string LinenPath="Assets/Hefu/Textures/WarmInterior/Linen_IvoryWeave_512.png";
 static Material Mat(string id,Color color,float metallic,float smooth,Texture2D tex=null){
  Directory.CreateDirectory(Art);
  string path=Art+id+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Standard")){name=id};AssetDatabase.CreateAsset(m,path);}
  m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);
  m.mainTexture=tex;
  EditorUtility.SetDirty(m);
  return m;
 }
 static Transform Empty(Transform root,string name,Vector3 position){
  var t=new GameObject(name).transform;
  t.SetParent(root,false);t.position=position;return t;
 }
 static void Cylinder(Transform root,string id,Vector3 at,Vector3 dims,Material m){
  var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
  g.name=id;g.transform.SetParent(root,false);g.transform.position=at;g.transform.localScale=dims;
  g.GetComponent<Renderer>().sharedMaterial=m;
  UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
  g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
 }
 static void Globe(Transform root,string id,Vector3 at,Vector3 dims,Material m){
  var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);
  g.name=id;g.transform.SetParent(root,false);g.transform.position=at;g.transform.localScale=dims;
  g.GetComponent<Renderer>().sharedMaterial=m;
  UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
  g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
 }
 static Mesh Lathe(string name){
  const int sectors=24;
  float[] heights={1.994f,2.015f,2.040f,2.092f,2.165f,2.209f,2.235f};
  float[] radii={.218f,.249f,.251f,.228f,.174f,.102f,.029f};
  int rings=heights.Length;
  var vertices=new Vector3[(rings)*sectors];var uv=new Vector2[vertices.Length];
  for(int y=0;y<rings;y++)for(int i=0;i<sectors;i++){
   float a=2*Mathf.PI*i/sectors;int index=y*sectors+i;
   vertices[index]=new Vector3(radii[y]*Mathf.Cos(a),heights[y]-2f,radii[y]*Mathf.Sin(a));
   uv[index]=new Vector2(i/(float)sectors,y/(float)(rings-1));
  }
  var triangles=new int[(rings-1)*sectors*6];int j=0;
  for(int y=0;y<rings-1;y++)for(int i=0;i<sectors;i++){
   int a=y*sectors+i, b=y*sectors+(i+1)%sectors,c=(y+1)*sectors+i,d=(y+1)*sectors+(i+1)%sectors;
   triangles[j++]=a;triangles[j++]=c;triangles[j++]=b;
   triangles[j++]=b;triangles[j++]=c;triangles[j++]=d;
  }
  var mesh=new Mesh{name=name,vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
  return mesh;
 }
 static Mesh CurtainMesh(string name,float width,float height){
  const int columns=30,rows=9;
  var v=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[v.Length];
  for(int row=0;row<=rows;row++)for(int x=0;x<=columns;x++){
   float t=x/(float)columns, h=row/(float)rows;
   int n=row*(columns+1)+x;
   float ruffle=Mathf.Sin(t*Mathf.PI*10f)*.034f;
   // Small gathering, asymmetric hem and lightweight fabric wave.
   v[n]=new Vector3((t-.5f)*width+(1-h)*.021f*Mathf.Sin(t*10f),h*height,ruffle);
   uv[n]=new Vector2(t*2.0f,h*2.6f);
  }
  int[] tris=new int[columns*rows*6];int k=0;
  for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){
   int a=y*(columns+1)+x,b=a+1,c=a+columns+1,d=c+1;
   tris[k++]=a;tris[k++]=b;tris[k++]=c;
   tris[k++]=b;tris[k++]=d;tris[k++]=c;
  }
  var mesh=new Mesh{name=name,vertices=v,uv=uv,triangles=tris};mesh.RecalculateBounds();mesh.RecalculateNormals();return mesh;
 }
 static Mesh Persist(Mesh fresh,string id){
  Directory.CreateDirectory(Art);string path=Art+id+".asset";
  var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(existing){EditorUtility.CopySerialized(fresh,existing);UnityEngine.Object.DestroyImmediate(fresh);return existing;}
  AssetDatabase.CreateAsset(fresh,path);return fresh;
 }
 public static void Apply(Transform house){
  if(!house)throw new ArgumentNullException(nameof(house));
  var old=house.Find(GroupName);
  if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var root=Empty(house,GroupName,Vector3.zero);
  // Existing real CAD dining-table center; anchor to movable root at scene
  // generation, but do not parent this fixed ceiling lamp to moving furniture.
  var table=house.Find("Move_Dining_Table");
  if(!table)throw new Exception("HEFU_WARM_REFERENCE_DINING_ANCHOR_MISSING");
  var brass=Mat("Hefu_WarmBrushedBrass",new Color(.64f,.48f,.27f),.58f,.61f);
  var shade=Mat("Hefu_WarmPendantShade",new Color(.94f,.88f,.76f),.05f,.32f);
  var diffuser=Mat("Hefu_WarmDiffuser",new Color(1f,.95f,.84f),0f,.50f);
  // Mesh asset shared across both pendants (24 sections, 7 profile rings).
  var lamp=Persist(Lathe("HefuPendantLathe_24x7"),"HefuPendantLathe");
  for(int i=0;i<2;i++){
   var center=new Vector3(table.position.x,0,table.position.z+(i==0?-.38f:.38f));
   var pendant=Empty(root,"DiningPendant_"+i,center);
   Cylinder(pendant,"CeilingCanopy",center+new Vector3(0,2.665f,0),new Vector3(.070f,.019f,.070f),brass);
   Cylinder(pendant,"SuspensionCable",center+new Vector3(0,2.442f,0),new Vector3(.006f,.216f,.006f),brass);
   var shadeObj=new GameObject("IvoryShade_RealGeometry");shadeObj.transform.SetParent(pendant,false);
   shadeObj.transform.position=center+new Vector3(0,2f,0);
   shadeObj.AddComponent<MeshFilter>().sharedMesh=lamp;
   var renderer=shadeObj.AddComponent<MeshRenderer>();renderer.sharedMaterial=shade;
   renderer.shadowCastingMode=ShadowCastingMode.Off;
   Globe(pendant,"WarmDiffuserUnderShade",center+new Vector3(0,2.005f,0),new Vector3(.355f,.035f,.355f),diffuser);
  }
  var linen=AssetDatabase.LoadAssetAtPath<Texture2D>(LinenPath);
  if(!linen)throw new Exception("HEFU_WARM_REFERENCE_LINEN_MISSING");
  var fabric=Mat("Hefu_IvoryCurtainLinen",Color.white,0f,.11f,linen);
  var rod=Mat("Hefu_CurtainRailIvory",new Color(.84f,.82f,.77f),.18f,.22f);
  // Only 2 short SIDE drape stacks at CAD south window x=-10.33..-5.53.
  // Glass center remains clear. Curtains project toward the room (Z > -9.94).
  const float windowZ=-9.94f;
  var cloth=Persist(CurtainMesh("Hefu_LinenCurtainFold_30x9",.34f,1.74f),"HefuLinenCurtainFold");
  for(int i=0;i<2;i++){
   float x=i==0?-10.18f:-5.67f;
   var curtain=Empty(root,"SouthWindowLinenSidePanel_"+i,new Vector3(x,.74f,windowZ+.205f));
   curtain.gameObject.AddComponent<MeshFilter>().sharedMesh=cloth;
   var renderer=curtain.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=fabric;
   renderer.shadowCastingMode=ShadowCastingMode.Off;
   renderer.receiveShadows=true;
  }
  // Thin ceiling-mounted curtain rail, not a duplicate glass layer.
  var rail=Empty(root,"SouthWindowCurtainRail",new Vector3(-7.925f,2.503f,windowZ+.20f));
  Cylinder(rail,"CurtainRail",rail.position,new Vector3(.014f,2.283f,.014f),rod);
  rail.Find("CurtainRail").localRotation=Quaternion.Euler(0,0,90);
  if(root.GetComponentsInChildren<Collider>(true).Length!=0)
   throw new Exception("HEFU_WARM_REFERENCE_UNEXPECTED_COLLIDER");
  Debug.Log("HEFU_WARM_REFERENCE_STYLE_PASS pendants=2 drapes=2 rail=1"+
   " originalCAD=true movableFurnitureUnchanged=true noNewLights=true noColliders=true");
 }
}
}
