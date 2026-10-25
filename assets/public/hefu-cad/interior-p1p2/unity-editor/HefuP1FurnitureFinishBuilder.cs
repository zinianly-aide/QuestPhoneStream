using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HefuVR.Editor {
/// <summary>Replaces ONLY the visible sofa / coffee table shells with Blender FBX.
/// Original synthetic B mesh, CAD collider and Move_* identity remain intact.
/// Safe to rerun on a fresh generated scene; no behavioral script changes.</summary>
public static class HefuP1FurnitureFinishBuilder {
 const string ModelDir="Assets/Hefu/Models/InteriorRefinement/";
 const string MaterialDir="Assets/Hefu/Generated/FurnitureDetailAssets/";
 const string Name="Hefu_P1_BlenderVisual_";
 static Transform Find(Transform root,string n){
  foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==n)return t;
  throw new Exception("HEFU_P1_FURNITURE_ANCHOR_MISSING_"+n);
 }
 static Material FinishGold(){
  Directory.CreateDirectory(MaterialDir);
  const string path=MaterialDir+"HefuP1_BrushedBrass.mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Standard")){name="HefuP1_BrushedBrass"};AssetDatabase.CreateAsset(m,path);}
  m.color=new Color(.67f,.50f,.24f);
  m.SetFloat("_Metallic",.72f);m.SetFloat("_Glossiness",.61f);
  EditorUtility.SetDirty(m);return m;
 }
 static Material SourceMaterial(string prefix,int slot){
  var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialDir+prefix+"_"+slot+".mat");
  if(!mat)throw new Exception("HEFU_P1_MATERIAL_NOT_READY_"+prefix+"_"+slot);
  return mat;
 }
 static void Inject(Transform root,string holderId,string detailId,string model,
   string[] materialNames,Material[] materials){
  var holder=Find(root,holderId);
  var fallback=Find(holder,"Hefu_FurnitureDetail_"+detailId);
  var oldRenderer=fallback.GetComponent<MeshRenderer>();
  if(!oldRenderer||!oldRenderer.enabled)throw new Exception("HEFU_P1_FALLBACK_RENDERER_NOT_READY_"+detailId);
  var anchorBounds=oldRenderer.bounds;
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir+model+".fbx");
  if(!source)throw new Exception("HEFU_P1_FBX_MISSING_"+model);
  var instance=PrefabUtility.InstantiatePrefab(source) as GameObject;
  if(!instance)throw new Exception("HEFU_P1_FBX_INSTANCE_ERROR_"+model);
  instance.name=Name+model;
  instance.transform.SetParent(fallback,false);
  instance.transform.localPosition=Vector3.zero;
  // Blender's original mesh faces +X, but the TV is WEST (-X): in the
  // previous phone screenshot we were seeing the sofa BACK from the TV.
  // Rotate only the sofa model 180 degrees around WORLD UP, not its Move_* root.
  if(model=="HefuP1_SoftSofa")
    instance.transform.rotation=Quaternion.AngleAxis(180f,Vector3.up)*instance.transform.rotation;
  // Preserve FBX importer root rotation as well: it converts Blender Z-up
  // to Unity Y-up. Forcing identity transposes sofa length into vertical.
  // FBX import importer intentionally reports root scale=100; preserve imported
  // root scale rather than forcing it to 1, which would shrink every model.
  var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
  if(renderers.Length!=1)throw new Exception("HEFU_P1_EXPECTED_ONE_POOLED_RENDERER_"+model);
  var renderer=renderers[0];
  renderer.transform.GetComponent<MeshFilter>().sharedMesh.RecalculateBounds();
  var currentMats=renderer.sharedMaterials;
  if(currentMats.Length!=materialNames.Length || materials.Length!=materialNames.Length)
   throw new Exception("HEFU_P1_MATERIAL_COUNT_"+model+"="+currentMats.Length);
  var newMats=new Material[currentMats.Length];
  for(int i=0;i<currentMats.Length;i++){
   int found=Array.IndexOf(materialNames,currentMats[i].name);
   if(found<0)throw new Exception("HEFU_P1_UNKNOWN_MATERIAL_"+currentMats[i].name);
   newMats[i]=materials[found];
  }
  renderer.sharedMaterials=newMats;
  // Imported Blender authoring mesh center is allowed a slight offset.
  instance.transform.position -= renderer.bounds.center-anchorBounds.center;
  var b=renderer.bounds;
  if(Vector3.Distance(b.center,anchorBounds.center)>.035f ||
    Mathf.Abs(b.size.x-anchorBounds.size.x)>.10f ||
    Mathf.Abs(b.size.y-anchorBounds.size.y)>.10f ||
    Mathf.Abs(b.size.z-anchorBounds.size.z)>.11f)
   throw new Exception("HEFU_P1_MESH_BOUNDS_NOT_ALIGNED_"+model+" original="+anchorBounds+" new="+b);
  if(instance.GetComponentsInChildren<Collider>(true).Length!=0)
   throw new Exception("HEFU_P1_IMPORTED_COLLIDERS_UNEXPECTED");
  renderer.shadowCastingMode=ShadowCastingMode.On;
  renderer.receiveShadows=true;
  // Hide but retain source geometry, source UV and materials for instant rollback.
  oldRenderer.enabled=false;
  Debug.Log("HEFU_P1_BLENDER_VISUAL_PASS "+model+" submeshes="+newMats.Length+
   " verts="+renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount+
   " dimensions="+b.size+" fallbackKept=true colliderUnchanged=true");
 }
 public static void Apply(Transform root){
  var sofa=new[]{SourceMaterial("SofaUpholstery",1),SourceMaterial("SofaUpholstery",0),
    SourceMaterial("SofaUpholstery",3),SourceMaterial("SofaUpholstery",2)};
  Inject(root,"Move_Sofa","SofaUpholstery","HefuP1_SoftSofa",
   new[]{"Hefu_LeatherDark","Hefu_LeatherOlive","Hefu_FabricGraphite","Hefu_FabricCream"},sofa);
  Inject(root,"Move_Coffee_Table","WalnutCoffeeTable","HefuP1_WalnutCoffee",
   new[]{"Hefu_Walnut","Hefu_BrushedBrass"},
   new[]{SourceMaterial("WalnutCoffeeTable",0),FinishGold()});
  Debug.Log("HEFU_P1_FURNITURE_PASS imported=2 originalMoveIDs=true originalColliders=true");
 }
}
}
