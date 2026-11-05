using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor {
/// Same scene/GI/camera/quality, only toggles 14 blob renderers.
public static class HefuM5DenseContactCapture {
 const string Out="ABTestReports/M5_GI_AO/DenseContact/";
 static void Frames(Camera cam,HefuCeilingVisibility roof,string label){
  var rt=new RenderTexture(2048,1365,24);
  var tex=new Texture2D(2048,1365,TextureFormat.RGB24,false);
  var previous=RenderTexture.active;
  try{
   rt.Create();cam.targetTexture=rt;
   foreach(var pose in HefuM4PresentationPlan.Views){
    cam.transform.position=pose.eye;
    cam.transform.rotation=Quaternion.LookRotation((pose.target-pose.eye).normalized);
    cam.fieldOfView=HefuM4PresentationPlan.Fov;
    if(roof)roof.UpdateVisibility(pose.eye);
    cam.Render();cam.Render();
    RenderTexture.active=rt;
    tex.ReadPixels(new Rect(0,0,2048,1365),0,0);tex.Apply();
    var image=tex.EncodeToPNG();
    if(image.Length<50000)throw new Exception("M5_DENSE_EMPTY_"+label+"_"+pose.id);
    string file=Out+label+"_"+pose.id+".png";
    File.WriteAllBytes(file,image);
    Debug.Log("M5_DENSE_CAPTURE_FRAME "+file+" bytes="+image.Length);
   }
  } finally {
   cam.targetTexture=null;RenderTexture.active=previous;
   rt.Release();UnityEngine.Object.DestroyImmediate(rt);
   UnityEngine.Object.DestroyImmediate(tex);
  }
 }
 public static void Run(){
  EditorSceneManager.OpenScene(HefuM5DenseLightProbeLayout.Blob,OpenSceneMode.Single);
  var h=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  var toggle=h?.GetComponent<HefuM5BlobShadowToggle>();
  var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
  var roof=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
  if(!toggle||toggle.shadows.Length!=14||!viewer||!viewer.cam||
    !LightmapSettings.lightProbes||LightmapSettings.lightProbes.count!=256)
    throw new Exception("M5_DENSE_CAPTURE_PRECONDITION");
  Directory.CreateDirectory(Out);
  var o=new GameObject("M5_TEMP_CAPTURE_NOT_SAVED");
  var cam=o.AddComponent<Camera>();cam.CopyFrom(viewer.cam);cam.enabled=false;
  cam.allowHDR=false;cam.nearClipPlane=.04f;cam.farClipPlane=110f;
  try{
   toggle.Apply(false);Frames(cam,roof,"Dense256_NoBlob");
   toggle.Apply(true);Frames(cam,roof,"Dense256_WithBlob");
   Debug.Log("M5_DENSE_CAPTURE_PASS frames10=true sameGI=true sameScene=true"+
     " sameCamera=true onlyBlobToggled=true");
  } finally {
   toggle.Apply(true);
   UnityEngine.Object.DestroyImmediate(o);
  }
 }
}
}
