using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor{
/// <summary>Real Unity 2K gallery proof for M4; uses same runtime camera IDs
/// and output resolution; avoids scripts/AI mocks/any scene save.</summary>
public static class HefuM4ExhibitEditor{
 const string Dir="Builds/StyleUpgradeM4/qa/highres/";
 public static void Export(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var light=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!light||!light.HasBakedPair)throw new Exception("HEFU_M4_EXPORT_MISSING_DUAL_GI");
  light.ApplyMode(false);
  var quality=UnityEngine.Object.FindObjectOfType<HefuM3QualityController>();
  quality.Apply(HefuM3QualityController.Tier.Quality);
  var roof=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
  var live=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>().cam;
  var go=new GameObject("HEFU_M4_EDITOR_TEMP_2K_CAMERA");var cam=go.AddComponent<Camera>();
  cam.CopyFrom(live);cam.enabled=false;
  cam.fieldOfView=HefuM4PresentationPlan.Fov;cam.nearClipPlane=.04f;cam.farClipPlane=110f;cam.allowHDR=false;
  Directory.CreateDirectory(Dir);
  var header="id,eyeX,eyeY,eyeZ,targetX,targetY,targetZ,width,height,fov,bytes\n";
  var sb=new System.Text.StringBuilder(header);
  try{
   foreach(var pose in HefuM4PresentationPlan.Views){
    cam.transform.position=pose.eye;
    cam.transform.rotation=Quaternion.LookRotation((pose.target-pose.eye).normalized);
    if(roof)roof.UpdateVisibility(pose.eye);
    var rt=new RenderTexture(HefuM4PresentationPlan.Width,HefuM4PresentationPlan.Height,24);
    var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
    var restore=RenderTexture.active;
    try{
     rt.Create();cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
     image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
     var png=image.EncodeToPNG();if(png.Length<50000)throw new Exception("HEFU_M4_BLANK_"+pose.id);
     File.WriteAllBytes(Dir+pose.id+".png",png);
     sb.Append(pose.id).Append(',').Append(pose.eye.x).Append(',').Append(pose.eye.y).Append(',')
       .Append(pose.eye.z).Append(',').Append(pose.target.x).Append(',')
       .Append(pose.target.y).Append(',').Append(pose.target.z).Append(',')
       .Append(rt.width).Append(',').Append(rt.height).Append(',').Append(cam.fieldOfView)
       .Append(',').Append(png.Length).Append('\n');
     Debug.Log("HEFU_M4_2K_EXPORT_PASS "+pose.id+" "+rt.width+"x"+rt.height+" bytes="+png.Length);
    }finally{
     cam.targetTexture=null;RenderTexture.active=restore;
     rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
    }
   }
   File.WriteAllText(Dir+"manifest.csv",sb.ToString());
   Debug.Log("HEFU_M4_FIVE_VIEWS_PASS resolution=2048x1365 manifest="+Dir+"manifest.csv");
  }finally{if(roof&&live)roof.UpdateVisibility(live.transform.position);
   quality.Apply(HefuM3QualityController.Tier.Balanced);UnityEngine.Object.DestroyImmediate(go);}
 }
}
}
