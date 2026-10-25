using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Linq;
namespace HefuVR.Editor {
public static class HefuSouthWindowDiagnostic {
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  var outgroup=house.transform.Find("Hefu_OutdoorPlaceholder_v1");
  foreach(Transform child in outgroup)if(child.name.StartsWith("Illustrative_Tree_")){
   var rr=child.GetComponentsInChildren<Renderer>(true);
   var bb=rr[0].bounds;foreach(var rrr in rr)bb.Encapsulate(rrr.bounds);
   Debug.Log("HEFU_TREE_BOUNDS "+child.name+" pos="+child.position+" rot="+child.rotation+
     " scale="+child.localScale+" size="+bb.size+" center="+bb.center);
  }
  var windows=house.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("WindowCell")&&r.name.EndsWith("_glass")).OrderBy(r=>r.bounds.center.z).ToArray();
  foreach(var r in windows){
   var m=r.sharedMaterial;
   Debug.Log("HEFU_SOUTH_WINDOW glass="+r.name+" bounds="+r.bounds+" mat="+m.name+" color="+m.color+" shader="+m.shader.name+" queue="+m.renderQueue+" mode="+m.GetFloat("_Mode")+" zwrite="+m.GetFloat("_ZWrite")+" src="+m.GetInt("_SrcBlend")+" dst="+m.GetInt("_DstBlend"));
  }
  foreach(var r in house.GetComponentsInChildren<Renderer>(true)){
   var b=r.bounds;
   if(b.center.z < -8.8f && b.center.z > -11.6f && b.size.x>.32f && b.size.y>.35f && b.size.z<1.75f && !r.name.Contains("WindowCell")){
    Debug.Log("HEFU_SOUTH_OCCLUDER name="+r.name+" bounds="+b+" mat="+(r.sharedMaterial?r.sharedMaterial.name:"none")+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled);
   }
  }
 }
}
}
