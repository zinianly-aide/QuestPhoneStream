using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>M1: honor original exact CAD while improving readability of south
/// single-pane / kitchen glazing and 3 visible sofa cushions. Render only:
/// no collider changes, no movable IDs added, no door hinges repositioned.</summary>
public static class HefuM1StructureFinish {
 public const string SeatName="Hefu_M1_VisibleSeatCushions";
 const string AssetDir="Assets/Hefu/Generated/M1Style/";
 static Mesh Persist(Mesh value,string name){
  Directory.CreateDirectory(AssetDir);string path=AssetDir+name+".asset";
  var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
  AssetDatabase.CreateAsset(value,path);return value;
 }
 sealed class Sculpt{
  public List<Vector3> positions=new List<Vector3>();
  public List<Vector2> uv=new List<Vector2>();
  public List<int>[] tris={new List<int>(),new List<int>()};
  void Face(int slot,Vector3 center,Vector3 half,float bevel,Vector3 n,Vector3 u,Vector3 v){
   float hd=Mathf.Abs(Vector3.Dot(half,n));
   float hu=Mathf.Abs(Vector3.Dot(half,u)),hv=Mathf.Abs(Vector3.Dot(half,v));
   Vector3 core=new Vector3(Mathf.Max(0,half.x-bevel),Mathf.Max(0,half.y-bevel),Mathf.Max(0,half.z-bevel));
   float[] t={-1f,-.87f,0,.87f,1};int start=positions.Count;
   for(int j=0;j<5;j++)for(int i=0;i<5;i++){
    var p=n*hd+u*(hu*t[i])+v*(hv*t[j]);
    var nearest=new Vector3(Mathf.Clamp(p.x,-core.x,core.x),Mathf.Clamp(p.y,-core.y,core.y),Mathf.Clamp(p.z,-core.z,core.z));
    var outwards=p-nearest;
    positions.Add(center+nearest+outwards.normalized*bevel);
    uv.Add(new Vector2((hu*t[i]+hu)/.48f,(hv*t[j]+hv)/.48f));
   }
   bool flip=Vector3.Dot(Vector3.Cross(u,v),n)<0;
   for(int j=0;j<4;j++)for(int i=0;i<4;i++){
    int a=start+j*5+i,b=a+1,c=a+5,d=c+1;
    if(flip)tris[slot].AddRange(new[]{a,c,b,b,c,d});
    else tris[slot].AddRange(new[]{a,b,c,b,d,c});
   }
  }
  public void Rounded(int material,Vector3 c,Vector3 size,float r){
   var h=size*.5f;
   Face(material,c,h,r,Vector3.up,Vector3.right,Vector3.forward);
   Face(material,c,h,r,Vector3.down,Vector3.right,Vector3.forward);
   Face(material,c,h,r,Vector3.left,Vector3.forward,Vector3.up);
   Face(material,c,h,r,Vector3.right,Vector3.forward,Vector3.up);
   Face(material,c,h,r,Vector3.forward,Vector3.right,Vector3.up);
   Face(material,c,h,r,Vector3.back,Vector3.right,Vector3.up);
  }
  public Mesh Build(){
   var m=new Mesh{name="Hefu_M1_Sofa_3SeparateSeats"};m.indexFormat=IndexFormat.UInt32;
   m.SetVertices(positions);m.SetUVs(0,uv);m.subMeshCount=2;
   for(int i=0;i<2;i++)m.SetTriangles(tris[i],i);
   m.RecalculateNormals();m.RecalculateBounds();return m;
  }
 }
 static void Sofa(Transform house){
  var sofa=house.Find("Move_Sofa");if(!sofa)throw new Exception("HEFU_M1_SOFA_ROOT_MISSING");
  var src=sofa.Find("Hefu_FurnitureDetail_SofaUpholstery");
  if(!src||!sofa.GetComponentInChildren<HefuVR.HefuMovable>()){
   // HefuMovable lives at the sofa root itself in existing shipping scenes.
   if(!src||!sofa.GetComponent<HefuVR.HefuMovable>())throw new Exception("HEFU_M1_NO_SOFA_VISUAL_OR_MOBILITY");
  }
  if(sofa.Find(SeatName))UnityEngine.Object.DestroyImmediate(sofa.Find(SeatName).gameObject);
  // Original Blender seat is y≈.59-.65; pads rest on it, not floating above.
  // 3 x 700mm pads across the 2.27m living sofa, 10mm gaps, soft edge roll.
  var sculpt=new Sculpt();
  for(int i=0;i<3;i++){
   float z=(i-1)*.738f;
   sculpt.Rounded(0,new Vector3(-.120f,.606f,z),new Vector3(.670f,.125f,.695f),.052f);
   // Restraint: 2.5mm front-edge piping reads as stitched cushioning in close views.
   sculpt.Rounded(1,new Vector3(-.465f,.584f,z),new Vector3(.006f,.013f,.622f),.0025f);
  }
  var node=new GameObject(SeatName);node.transform.SetParent(sofa,false);
  node.AddComponent<MeshFilter>().sharedMesh=Persist(sculpt.Build(),"Hefu_M1_Sofa_3SeparateSeats");
  var renderer=node.AddComponent<MeshRenderer>();
  var leather=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hefu/Generated/FurnitureDetailAssets/SofaUpholstery_0.mat");
  var piping=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hefu/Generated/FurnitureDetailAssets/SofaUpholstery_1.mat");
  if(!leather||!piping)throw new Exception("HEFU_M1_SOFA_MATERIALS_MISSING");
  renderer.sharedMaterials=new[]{leather,piping};renderer.shadowCastingMode=ShadowCastingMode.On;
  renderer.receiveShadows=true;
  if(node.GetComponent<Collider>())throw new Exception("HEFU_M1_SEAT_HAS_COLLIDER");
  Debug.Log("HEFU_M1_SEAT_PASS separateSeats=3 sofaSeatTop=0.669m"+
    " seatWidth=0.670m segmentLength=0.695m seatGap=0.043m"+
    " originalBlenderModelKept=true noNewColliders=true pooled=1");
 }
 static void Glass(Transform house){
  var south=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hefu/Generated/Atmosphere/HefuClearSouthWindow.mat");
  var general=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hefu/Generated/Atmosphere/HefuClearArchitecturalGlass.mat");
  if(!south||!general)throw new Exception("HEFU_M1_GLAZING_REQUIRED");
  // Existing glass was already Transparent, but three overlapping CAD sheets
  // and strong blue tint made portions of the house appear frosted/opaque.
  // Keep only 22/28 drawing per group (already established in AtmosphereBuilder),
  // neutralize the tint. Preserve the entire original CAD geometry/door swing.
  south.color=new Color(.985f,.992f,1f,.048f);
  south.SetFloat("_Glossiness",.51f);EditorUtility.SetDirty(south);
  general.color=new Color(.93f,.967f,.99f,.125f);
  general.SetFloat("_Glossiness",.71f);EditorUtility.SetDirty(general);
  int southCount=0,hidden=0;
  foreach(var r in house.GetComponentsInChildren<Renderer>(true))if(r.name.Contains("CAD_WindowCell_")&&r.name.EndsWith("_glass")){
   bool isSouth=r.name.StartsWith("21_CAD_")||r.name.StartsWith("22_CAD_")||r.name.StartsWith("23_CAD_")||r.name.StartsWith("27_CAD_")||r.name.StartsWith("28_CAD_")||r.name.StartsWith("29_CAD_");
   if(isSouth){if(r.enabled)southCount++;else hidden++;}
  }
  var kitchen=house.Find(HefuInteriorDoorBuilder.GroupName+"/Door_Kitchen/DoorHinge/DoorLeaf");
  if(!kitchen||!kitchen.GetComponent<Collider>()||kitchen.GetComponent<Renderer>().sharedMaterial!=general)
   throw new Exception("HEFU_M1_KITCHEN_GLASS_HINGE_OR_MATERIAL_MISSING");
  foreach(var m in new[]{south,general})if(m.renderQueue<(int)RenderQueue.Transparent||m.GetFloat("_ZWrite")>.01f||m.GetFloat("_Mode")!=2)
   throw new Exception("HEFU_M1_GLASS_TRANSPARENCY_MODE_FAIL");
  if(southCount!=2||hidden!=4)throw new Exception("HEFU_M1_SOUTH_SHEET_DUPLICATION="+southCount+"/"+hidden);
  Debug.Log("HEFU_M1_GLASS_PASS visibleSouthSheets=2 hiddenDuplicates=4 alphaSouth=.048"+
   " kitchenAlpha=.125 premultiplyOff=true zwriteOff=true doorCollidersPreserved=true");
 }
 public static void Apply(Transform house){Glass(house);Sofa(house);AssetDatabase.SaveAssets();}
}
}
