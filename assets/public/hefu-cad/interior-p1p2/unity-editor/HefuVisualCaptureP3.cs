using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HefuVR.Editor {
public static class HefuVisualCaptureP3 {
 static void Shot(Camera cam,string id,Vector3 from,Vector3 look){
  cam.transform.position=from;cam.transform.rotation=Quaternion.LookRotation((look-from).normalized,Vector3.up);
  var rt=new RenderTexture(800,560,24,RenderTextureFormat.ARGB32);var prev=RenderTexture.active;
  var tex=new Texture2D(800,560,TextureFormat.RGB24,false);
  try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
      tex.ReadPixels(new Rect(0,0,800,560),0,0);tex.Apply();
      string dest="Builds/InteriorRefinement/QA_Render_"+id+".png";
      File.WriteAllBytes(dest,tex.EncodeToPNG());
      Debug.Log("HEFU_P3_RENDER_PASS "+id+" bytes="+new FileInfo(dest).Length+" path="+dest);
  }finally{cam.targetTexture=null;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
 }
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var go=new GameObject("HEFU_VISUAL_QA_TEMP_CAMERA");var cam=go.AddComponent<Camera>();
  cam.clearFlags=CameraClearFlags.Skybox;cam.fieldOfView=64;cam.nearClipPlane=.04f;cam.farClipPlane=120f;
  try{
   Shot(cam,"sofa_front",new Vector3(-6.73f,1.43f,-5.10f),new Vector3(-5.10f,.72f,-5.99f));
   Shot(cam,"south_window",new Vector3(-7.88f,1.56f,-9.12f),new Vector3(-7.58f,1.45f,-17.9f));
   Shot(cam,"living",new Vector3(-5.55f,1.72f,-3.85f),new Vector3(-5.30f,.65f,-7.80f));
  }finally{UnityEngine.Object.DestroyImmediate(go);}
 }
}
}
