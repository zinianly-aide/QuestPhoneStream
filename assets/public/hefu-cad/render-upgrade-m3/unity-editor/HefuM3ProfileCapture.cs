using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor{
public static class HefuM3ProfileCapture{
 static void Frame(Camera cam,string name){
  const int w=960,h=640;
  var target=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);
  var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
  var prior=RenderTexture.active;
  try{cam.targetTexture=target;cam.Render();RenderTexture.active=target;
   tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
   var file="Builds/StyleUpgradeM3/captures/"+name+".png";
   File.WriteAllBytes(file,tex.EncodeToPNG());
   if(new FileInfo(file).Length<7000)throw new Exception("HEFU_M3_PROFILE_RENDER_EMPTY");
   Debug.Log("HEFU_M3_PROFILE_CAPTURE_PASS "+name+" bytes="+new FileInfo(file).Length);
  }finally{RenderTexture.active=prior;cam.targetTexture=null;target.Release();
   UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(tex);}
 }
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
  var controller=viewer?viewer.quality:null;
  if(!controller||!controller.roomProbes[0])throw new Exception("HEFU_M3_PROFILE_NO_QUALITY");
  var temp=new GameObject("M3_EDITOR_TEMP_PROFILE_CAMERA");var cam=temp.AddComponent<Camera>();
  cam.fieldOfView=60;cam.nearClipPlane=.04f;cam.farClipPlane=110;
  var first=HefuUpgradeM0Capture.Eyes[0];var aim=HefuUpgradeM0Capture.Targets[0];
  cam.transform.position=first;cam.transform.rotation=Quaternion.LookRotation((aim-first).normalized);
  try{
   foreach(var tier in new[]{HefuM3QualityController.Tier.Battery,HefuM3QualityController.Tier.Balanced,
                           HefuM3QualityController.Tier.Quality}){
    controller.Apply(tier);cam.allowHDR=tier==HefuM3QualityController.Tier.Quality;
    Frame(cam,"Profile_"+tier+"_Hero");
   }
  }finally{controller.Apply(HefuM3QualityController.Tier.Balanced);
   UnityEngine.Object.DestroyImmediate(temp);}
 }
}
}
