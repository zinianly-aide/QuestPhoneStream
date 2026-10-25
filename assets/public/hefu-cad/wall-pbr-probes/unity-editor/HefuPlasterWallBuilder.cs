using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>PBR light ivory wall finish with UVs derived from original CAD
/// surface normals. Zero CAD vertices/colliders modified. The original Style
/// switcher remains authoritative for each preset's wall colour.</summary>
public static class HefuPlasterWallBuilder {
 public const string MeshDir="Assets/Hefu/Generated/WallFinish/";
 const string TextureDir="Assets/Hefu/Textures/WallPBR/";
 static Texture2D Texture(string id,bool normal){
  var path=TextureDir+id+".png";var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  if(!imported)throw new Exception("HEFU_WALL_PBR_TEXTURE_MISSING "+path);
  var importer=AssetImporter.GetAtPath(path) as TextureImporter;
  if(importer!=null){
   bool change=false;
   if(normal&&importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;change=true;}
   if(importer.wrapMode!=TextureWrapMode.Repeat){importer.wrapMode=TextureWrapMode.Repeat;change=true;}
   if(!importer.mipmapEnabled){importer.mipmapEnabled=true;change=true;}
   if(change){importer.SaveAndReimport();imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
  }
  return imported;
 }
 static Mesh Project(Mesh source,Transform node,string id){
  string file=MeshDir+"Wall_UV_"+id+".asset";
  var mesh=UnityEngine.Object.Instantiate(source);mesh.name="HefuWallProjectedUV_"+id;
  var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
  if(normals.Length!=vertices.Length)throw new Exception("HEFU_WALL_PBR_NORMALS_INVALID "+id);
  for(int i=0;i<vertices.Length;i++){
   var p=node.TransformPoint(vertices[i]);
   var n=node.TransformDirection(normals[i]).normalized;
   float u=Mathf.Abs(n.y)>.72f?p.x:Mathf.Abs(n.x)>.55f?p.z:p.x;
   float v=Mathf.Abs(n.y)>.72f?p.z:p.y;
   uv[i]=new Vector2(u,v); // 1m per repeat; not stretched per CAD wall
  }
  mesh.uv=uv;
  var saved=AssetDatabase.LoadAssetAtPath<Mesh>(file);
  if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);return saved;}
  AssetDatabase.CreateAsset(mesh,file);return mesh;
 }
 public static void Apply(Transform house){
  if(!house)throw new ArgumentNullException(nameof(house));
  Directory.CreateDirectory(MeshDir);
  var color=Texture("IvoryPlaster_BaseColor",false);
  var normal=Texture("IvoryPlaster_Normal",true);
  string path=MeshDir+"IvoryPlasterWalls.mat";
  var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
  mat.mainTexture=color;
  mat.SetTexture("_BumpMap",normal);mat.SetFloat("_BumpScale",.20f);
  mat.EnableKeyword("_NORMALMAP");
  mat.SetFloat("_Metallic",0f);mat.SetFloat("_Glossiness",.18f);
  // Note: on load HefuDecorSwitcher.Start overwrites wall tint per saved Style.
  mat.color=new Color(.932f,.914f,.872f);
  EditorUtility.SetDirty(mat);
  int count=0,uvVertices=0;
  foreach(var r in house.GetComponentsInChildren<MeshRenderer>(true)){
   if(!r.name.Contains("CAD_wall"))continue;
   var filter=r.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
   var mesh=Project(filter.sharedMesh,filter.transform,r.name.Replace('/','_'));
   filter.sharedMesh=mesh;
   r.sharedMaterial=mat;r.receiveShadows=true;
   uvVertices+=mesh.vertexCount;count++;
  }
  if(count!=20)throw new Exception("HEFU_WALL_PBR_EXPECTED_20_FOUND_"+count);
  // Keep original mesh collider untouched, no new lights or draw layers.
  Debug.Log("HEFU_WALL_PBR_PASS walls="+count+" vertices="+uvVertices+
    " worldMetreUV=true plasterNormal=true metallic=0 smoothness=.18"+
    " CADCollidersUntouched=true styleSwitcherPreserved=true");
 }
}
}
