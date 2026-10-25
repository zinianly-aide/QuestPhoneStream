using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuWarmReferenceStyleRender {
 static void Take(Camera cam,string name,Vector3 eye,Vector3 look){
  const int w=960,h=640;
  var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);var texture=new Texture2D(w,h,TextureFormat.RGB24,false);
  var prev=RenderTexture.active;
  try{cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation((look-eye).normalized);
   // In the editor the runtime orbit-camera LateUpdate is not running.
   // Explicitly activate the roof cutaway based on this QA camera's position.
   var cutaway=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
   if(cutaway)cutaway.UpdateVisibility(eye);
   cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
   texture.ReadPixels(new Rect(0,0,w,h),0,0);texture.Apply();
   string filename="Builds/WarmReferenceStyle/Unity_"+name+".png";
   File.WriteAllBytes(filename,texture.EncodeToPNG());
   if(new FileInfo(filename).Length<5000)throw new Exception("HEFU_WARM_RENDER_SUSPICIOUSLY_SMALL_"+name);
   Debug.Log("HEFU_WARM_REFERENCE_RENDER_PASS "+name+" bytes="+new FileInfo(filename).Length);
  }finally{cam.targetTexture=null;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
 }
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var style=UnityEngine.Object.FindObjectOfType<HefuDecorSwitcher>();
  if(!style)throw new Exception("HEFU_WARM_REFERENCE_RENDER_MISSING_DECOR");
  style.preset=0;style.Apply(); // mimic runtime Start() wall colours
  var g=new GameObject("Hefu_QA_StudioCamera");var cam=g.AddComponent<Camera>();
  cam.nearClipPlane=.04f;cam.farClipPlane=120f;cam.fieldOfView=64;
  try{
   Take(cam,"Dining",new Vector3(-7.28f,1.60f,-4.29f),new Vector3(-5.98f,1.38f,-2.45f));
   Take(cam,"Living",new Vector3(-5.55f,1.72f,-3.85f),new Vector3(-5.30f,.65f,-7.80f));
   Take(cam,"SouthWindow",new Vector3(-6.30f,1.68f,-7.05f),new Vector3(-7.90f,1.32f,-9.95f));
   Take(cam,"TopNorth",new Vector3(-6.30f,15.8f,-7.20f),new Vector3(-6.30f,0,-4.70f));
  }finally{UnityEngine.Object.DestroyImmediate(g);}
 }
}
}
