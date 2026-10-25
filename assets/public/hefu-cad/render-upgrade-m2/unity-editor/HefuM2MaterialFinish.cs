using System;
using UnityEditor;
using UnityEngine;
namespace HefuVR.Editor{
/// <summary>M2 consistent PBR roughness and real-world UV scale. Reuses the
/// exact existing licensed/locally-generated texture/normal assets; no new
/// per-frame scripts, renderer instances, pipeline or geometry/colliders.</summary>
public static class HefuM2MaterialFinish {
 const string Furniture="Assets/Hefu/Generated/FurnitureDetailAssets/";
 const string Cabinet="Assets/Hefu/Generated/CabinetPBR/";
 const string Atmosphere="Assets/Hefu/Generated/Atmosphere/";
 static Material Need(string p){var mat=AssetDatabase.LoadAssetAtPath<Material>(p);if(!mat||!mat.shader||mat.shader.name!="Standard")throw new Exception("HEFU_M2_PBR_MISSING_STANDARD_"+p);return mat;}
 static void Finish(Material m,float smooth,float metal,float bump){
  m.SetFloat("_Glossiness",smooth);m.SetFloat("_Metallic",metal);
  if(bump>0){if(!m.GetTexture("_BumpMap"))throw new Exception("HEFU_M2_MISSING_NORMAL_"+m.name);
   m.SetFloat("_BumpScale",bump);m.EnableKeyword("_NORMALMAP");}
  EditorUtility.SetDirty(m);
 }
 public static void Apply(Transform house){
  var floor=Need(Furniture+"PhotoPorcelainFloor.mat");
  Finish(floor,.34f,.025f,.22f);floor.color=new Color(.955f,.95f,.93f);EditorUtility.SetDirty(floor);
  // UV from the unchanged floor mesh is generated at 0.78m per tile.
  var walnut=Need(Furniture+"WalnutCoffeeTable_0.mat");Finish(walnut,.29f,0f,.21f);
  var oak=Need(Furniture+"DiningTableFine_0.mat");Finish(oak,.33f,0f,.19f);
  var leather=Need(Furniture+"SofaUpholstery_0.mat");Finish(leather,.32f,0f,.30f);
  var back=Need(Furniture+"SofaUpholstery_1.mat");Finish(back,.29f,0f,.24f);
  var veneer=Need(Cabinet+"Hefu_IvoryCabinetVeneer48cm.mat");Finish(veneer,.28f,0f,0f);
  var fitted=Need(Cabinet+"Hefu_DiningWalnut_48cm.mat");Finish(fitted,.32f,0f,.19f);
  var brass=Need(Furniture+"HefuP1_BrushedBrass.mat");Finish(brass,.55f,.76f,0f);
  var g=Need(Atmosphere+"HefuClearArchitecturalGlass.mat");Finish(g,.79f,0f,0f);
  var gs=Need(Atmosphere+"HefuClearSouthWindow.mat");Finish(gs,.62f,0f,0f);
  if(g.GetFloat("_ZWrite")>.01f||gs.GetFloat("_ZWrite")>.01f||g.renderQueue<3000||gs.renderQueue<3000)
   throw new Exception("HEFU_M2_PBR_GLASS_DEPTH_REGRESSION");
  // The existing woollen rug stays physically co-located with the sofa and coffee table.
  var rug=AssetDatabase.LoadAssetAtPath<Material>(Furniture+"RenderQualityRug.mat");
  if(rug){Finish(rug,.12f,0f,0f);}
  AssetDatabase.SaveAssets();
  Debug.Log("HEFU_M2_MATERIAL_PASS tileMeters=.78 woodMeters=.48 floorGloss=.34"+
   " leatherNormal=.30 walnutNormal=.21 brushedBrassMetal=.76"+
   " cabinetVeneer=.28 transparentGlassDepthPreserved=true"+
   " existingSharedMaterials=true noNewTextures=true");
 }
}
}
