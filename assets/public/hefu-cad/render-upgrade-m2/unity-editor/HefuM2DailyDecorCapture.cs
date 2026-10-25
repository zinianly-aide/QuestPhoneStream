using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor{
public static class HefuM2DailyDecorCapture{
 static void Shot(Camera cam,string path){
  var rt=new RenderTexture(960,640,24);var tex=new Texture2D(960,640,TextureFormat.RGB24,false);
  var prev=RenderTexture.active;
  try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
   tex.ReadPixels(new Rect(0,0,960,640),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
   Debug.Log("HEFU_M2_DECOR_CAPTURE_PASS "+path+" bytes="+new FileInfo(path).Length);
  }finally{cam.targetTexture=null;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);}
 }
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2").transform;
  var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
  var decor=viewer.dailyDecor;var light=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!decor||!light||!light.HasBakedPair)throw new Exception("HEFU_M2_DECOR_RENDER_MISSING");
  light.ApplyMode(false);
  var work=house.Find("DiningSideboard/Worktop");
  var side=house.Find("DiningSideboard");
  if(!work&&side)foreach(var t in side.GetComponentsInChildren<Transform>(true))
   if(t.name=="Worktop"&&t.GetComponent<Renderer>()){work=t;break;}
  if(!work)throw new Exception("HEFU_M2_DECOR_CAPTURE_NO_CABINET_WORKTOP");
  Vector3 c=work.GetComponent<Renderer>().bounds.center;
  var go=new GameObject("M2_TEMP_EDITOR_CAMERA");var cam=go.AddComponent<Camera>();
  cam.fieldOfView=57;cam.nearClipPlane=.04f;cam.farClipPlane=85;cam.allowHDR=false;
  cam.transform.position=new Vector3(c.x-1.40f,1.52f,c.z-1.05f);
  cam.transform.rotation=Quaternion.LookRotation(new Vector3(c.x,1.00f,c.z)-cam.transform.position);
  var roof=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
  if(roof)roof.UpdateVisibility(cam.transform.position);
  try{
   decor.SetVisible(false);Shot(cam,"Builds/StyleUpgradeM2/daily-OFF.png");
   decor.SetVisible(true);Shot(cam,"Builds/StyleUpgradeM2/daily-ON.png");
  }finally{decor.SetVisible(true);UnityEngine.Object.DestroyImmediate(go);}
 }
}
}
