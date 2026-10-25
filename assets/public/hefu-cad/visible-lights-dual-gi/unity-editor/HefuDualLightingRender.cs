using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor {
public static class HefuDualLightingRender {
 static void Render(Camera cam,string name,Vector3 eye,Vector3 look){
  const int W=960,H=640;
  var target=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32);
  var image=new Texture2D(W,H,TextureFormat.RGB24,false);
  var prev=RenderTexture.active;
  try{
   cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation((look-eye).normalized);
   var cutaway=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
   if(cutaway)cutaway.UpdateVisibility(eye);
   cam.targetTexture=target;cam.Render();RenderTexture.active=target;
   image.ReadPixels(new Rect(0,0,W,H),0,0);image.Apply();
   string file="Builds/PracticalLights_v336/"+name+".png";
   File.WriteAllBytes(file,image.EncodeToPNG());
   if(new FileInfo(file).Length<7000)throw new Exception("HEFU_DUAL_RENDER_TOO_SMALL "+name);
   Debug.Log("HEFU_DUAL_LIGHT_RENDER_PASS "+name+" bytes="+new FileInfo(file).Length);
  }finally{cam.targetTexture=null;RenderTexture.active=prev;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
 }
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var f=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  var style=UnityEngine.Object.FindObjectOfType<HefuDecorSwitcher>();
  if(!f||!f.HasBakedPair||!style)throw new Exception("HEFU_DUAL_RENDER_NO_PRESETS_OR_STYLE");
  style.preset=0;style.Apply();
  var obj=new GameObject("HEFU_DUAL_QA_TEMP_CAMERA");var cam=obj.AddComponent<Camera>();
  cam.fieldOfView=64f;cam.nearClipPlane=.04f;cam.farClipPlane=120f;
  HefuBirthdayScene birthday=null;
  try{
   f.ApplyMode(false);
   Render(cam,"Day_Dining",new Vector3(-7.28f,1.60f,-4.29f),new Vector3(-5.98f,1.38f,-2.45f));
   Render(cam,"Day_Living",new Vector3(-5.55f,1.72f,-3.85f),new Vector3(-5.30f,.65f,-7.80f));
   Render(cam,"Day_FloorLamp",new Vector3(-6.45f,1.57f,-6.30f),new Vector3(-5.74f,1.30f,-7.40f));
   birthday=HefuBirthdayScene.Build(GameObject.Find("Hefu_CAD_Architectural_Base_v0.2").transform);
   birthday.SetTheme(HefuBirthdayScene.BirthdayThemeMode.Night);
   f.ApplyMode(true);
   Render(cam,"Birthday_Dining",new Vector3(-7.28f,1.60f,-4.29f),new Vector3(-5.98f,1.38f,-2.45f));
   Render(cam,"Birthday_Living",new Vector3(-5.55f,1.72f,-3.85f),new Vector3(-5.30f,.65f,-7.80f));
   Render(cam,"Birthday_FloorLamp",new Vector3(-6.45f,1.57f,-6.30f),new Vector3(-5.74f,1.30f,-7.40f));
  }finally{
   if(birthday){birthday.ClearDecorations();UnityEngine.Object.DestroyImmediate(birthday.gameObject);}
   f.ApplyMode(false);UnityEngine.Object.DestroyImmediate(obj);
  }
 }
}
}
