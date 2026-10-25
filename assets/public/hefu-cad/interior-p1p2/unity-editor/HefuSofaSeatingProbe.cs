using UnityEngine;
using UnityEditor.SceneManagement;
namespace HefuVR.Editor{
public static class HefuSofaSeatingProbe{
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var sofa=GameObject.Find("Hefu_P1_BlenderVisual_HefuP1_SoftSofa");var group=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  var tv=GameObject.Find("Hefu_FurnitureDetail_TelevisionAndConsole");
  var r=sofa.GetComponentInChildren<MeshRenderer>();var f=r.GetComponent<MeshFilter>();
  var b=r.bounds;int low=0,front=0,back=0;var sample=new System.Text.StringBuilder();
  foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);
   if(p.y>=b.min.y+.28f&&p.y<=b.min.y+.57f){low++;if(p.x<b.center.x-.16f)front++;if(p.x>b.center.x+.16f)back++;}
  }
  Debug.Log("HEFU_SOFA_PROBE sofaBounds="+b+" sofaRoot="+sofa.transform.position+" subRenderer="+r.transform.position+" localRot="+sofa.transform.localRotation+" parentRot="+sofa.transform.parent.rotation+" TVpos="+tv.transform.position+" meshRotation="+r.transform.rotation+" seatBandV="+low+" frontV="+front+" backV="+back);
 }
}
}
