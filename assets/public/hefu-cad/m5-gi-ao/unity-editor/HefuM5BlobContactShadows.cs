using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;

namespace HefuVR.Editor {
/// Procedural, transparent, visual-only furniture contact shadows.
public static class HefuM5BlobContactShadows {
 const string Dir="Assets/Hefu/Generated/M5Contact/";
 const string Image=Dir+"M5_SoftBlob128.png";
 const string Mat=Dir+"M5_SoftBlob.mat";
 const string MeshPath=Dir+"M5_SoftBlobQuad.asset";
 const string Child="M5_SoftContact_VisualOnly";
 static Material MakeMaterial(){
  var old=AssetDatabase.LoadAssetAtPath<Material>(Mat);
  if(old)return old;
  var tex=new Texture2D(128,128,TextureFormat.RGBA32,true,false);
  var colors=new Color[128*128];
  for(int y=0;y<128;y++)for(int x=0;x<128;x++){
   float a=(x+.5f)/64f-1f,b=(y+.5f)/64f-1f;
   float radius=Mathf.Sqrt(a*a+b*b);
   float alpha=.26f*Mathf.Pow(Mathf.Clamp01(1f-radius),1.45f);
   colors[y*128+x]=new Color(.12f,.095f,.08f,alpha);
  }
  tex.SetPixels(colors);tex.Apply(true,false);
  File.WriteAllBytes(Image,tex.EncodeToPNG());
  UnityEngine.Object.DestroyImmediate(tex);
  AssetDatabase.ImportAsset(Image);
  var importer=AssetImporter.GetAtPath(Image) as TextureImporter;
  if(!importer)throw new Exception("M5_SHADOW_TEXTURE_IMPORT_ERROR");
  importer.alphaIsTransparency=true;
  importer.wrapMode=TextureWrapMode.Clamp;
  importer.mipmapEnabled=true;
  importer.SaveAndReimport();
  var shader=Shader.Find("Unlit/Transparent");
  if(!shader)throw new Exception("M5_SHADOW_SHADER_UNAVAILABLE");
  var mat=new Material(shader){name="M5_SoftGroundContact"};
  mat.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Image));
  mat.SetColor("_Color",Color.white);
  mat.renderQueue=(int)RenderQueue.Transparent+2;
  AssetDatabase.CreateAsset(mat,Mat);
  return mat;
 }
 static Mesh MakeMesh(){
  var old=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
  if(old)return old;
  var m=new Mesh{name="M5_GroundContactUnitQuad"};
  m.vertices=new[]{
   new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),
   new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
  m.uv=new[]{new Vector2(0,0),new Vector2(0,1),
   new Vector2(1,1),new Vector2(1,0)};
  m.triangles=new[]{0,1,2,0,2,3};
  m.RecalculateNormals();m.RecalculateBounds();
  AssetDatabase.CreateAsset(m,MeshPath);
  return m;
 }
 public static void Stage(){
  string source=HefuM5DenseLightProbeLayout.Dense;
  string dest=HefuM5DenseLightProbeLayout.Blob;
  if(AssetDatabase.LoadAssetAtPath<SceneAsset>(dest))
   throw new Exception("M5_SHADOW_DEST_ALREADY_EXISTS");
  EditorSceneManager.OpenScene(source,OpenSceneMode.Single);
  var baked=Lightmapping.lightingDataAsset;
  int probeCount=LightmapSettings.lightProbes?LightmapSettings.lightProbes.count:0;
  if(!baked||probeCount<150||!AssetDatabase.GetAssetPath(baked).Contains("M5_GI20_DenseProbes/"))
   throw new Exception("M5_SHADOW_REQUIRES_DENSE_BAKE");
  if(!AssetDatabase.CopyAsset(source,dest))throw new Exception("M5_SHADOW_SCENE_CLONE_FAILED");
  var scene=EditorSceneManager.OpenScene(dest,OpenSceneMode.Single);
  var h=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2")?.transform;
  if(!h)throw new Exception("M5_SHADOW_CAD_MISSING");
  var furniture=h.GetComponentsInChildren<HefuMovable>(true);
  if(furniture.Length!=17)throw new Exception("M5_SHADOW_FURNITURE_COUNT");
  int collidersBefore=h.GetComponentsInChildren<Collider>(true).Length;
  Directory.CreateDirectory(Dir);
  var material=MakeMaterial();var mesh=MakeMesh();
  var root=h.gameObject.AddComponent<HefuM5BlobShadowToggle>();
  var shadows=new System.Collections.Generic.List<MeshRenderer>();
  foreach(var moving in furniture.OrderBy(x=>x.name,StringComparer.Ordinal)){
   var rs=moving.GetComponentsInChildren<MeshRenderer>(true);
   if(rs.Length==0)continue; // 3 empty placeholders; never fake a shadow
   if(moving.transform.Find(Child))throw new Exception("M5_SHADOW_ALREADY_EXISTS");
   var bounds=rs[0].bounds;
   foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
   var go=new GameObject(Child);
   go.transform.SetParent(moving.transform,false);
   go.transform.localPosition=moving.transform.InverseTransformPoint(
      new Vector3(bounds.center.x,.016f,bounds.center.z));
   go.transform.localRotation=Quaternion.identity;
   // A quad follows furniture translations, yaw rotation and scaling for free.
   // Feathered margin extends slightly beyond footprint; checked in a separate wide candidate.
   Vector3 sc=moving.transform.lossyScale;
   go.transform.localScale=new Vector3(
     Mathf.Max(.22f,bounds.size.x*1.15f)/Mathf.Max(.01f,Mathf.Abs(sc.x)),
     1f,
     Mathf.Max(.22f,bounds.size.z*1.15f)/Mathf.Max(.01f,Mathf.Abs(sc.z)));
   go.AddComponent<MeshFilter>().sharedMesh=mesh;
   var rOut=go.AddComponent<MeshRenderer>();
   rOut.sharedMaterial=material;
   rOut.shadowCastingMode=ShadowCastingMode.Off;
   rOut.receiveShadows=false;
   rOut.lightProbeUsage=LightProbeUsage.Off;
   rOut.reflectionProbeUsage=ReflectionProbeUsage.Off;
   GameObjectUtility.SetStaticEditorFlags(go,0);
   shadows.Add(rOut);
  }
  root.shadows=shadows.ToArray();root.visible=true;
  if(shadows.Count!=14||
      h.GetComponentsInChildren<Collider>(true).Length!=collidersBefore||
      LightmapSettings.lightProbes.count!=probeCount||
      Lightmapping.lightingDataAsset!=baked)
   throw new Exception("M5_SHADOW_STAGE_STRUCTURE_FAIL");
  EditorUtility.SetDirty(root);
  EditorSceneManager.MarkSceneDirty(scene);
  AssetDatabase.SaveAssets();
  if(!EditorSceneManager.SaveScene(scene))throw new Exception("M5_SHADOW_SAVE_FAIL");
  Debug.Log("M5_BLOB_STAGE_PASS renderedFurniture14=true emptyRoots3=true"+
   " probes="+probeCount+" sameGI=true noNewColliders=true visualOnly=true"+
   " followsFurniture=true toggle=true");
 }
 public static void QA(){
  EditorSceneManager.OpenScene(HefuM5DenseLightProbeLayout.Blob,OpenSceneMode.Single);
  var h=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2").transform;
  var root=h.GetComponent<HefuM5BlobShadowToggle>();
  if(!root||root.shadows==null||root.shadows.Length!=14)
   throw new Exception("M5_BLOB_QA_COUNT_FAIL");
  foreach(var r in root.shadows){
   if(!r||r.GetComponent<Collider>()||
      r.shadowCastingMode!=ShadowCastingMode.Off||
      r.lightProbeUsage!=LightProbeUsage.Off||
      GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.ContributeGI)||
      !r.transform.parent.GetComponent<HefuMovable>())
     throw new Exception("M5_BLOB_QA_SHADOW_INVALID");
  }
  root.Apply(false);
  if(root.shadows.Any(x=>x.enabled))throw new Exception("M5_BLOB_QA_TOGGLE_OFF");
  root.Apply(true);
  if(root.shadows.Any(x=>!x.enabled))throw new Exception("M5_BLOB_QA_TOGGLE_ON");
  Debug.Log("M5_BLOB_QA_PASS shadows14=true toggle=true collidersZero=true"+
   " staticZero=true attachedToMovables=true");
 }
}
}
