using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
/// <summary>M0 audited before/after identical 5 physical camera positions. Only
/// a TEMP editor camera is created; doesn't mutate the saved phone scene.</summary>
public static class HefuUpgradeM0Capture {
 public const string Prefix="Builds/StyleUpgradeM0M1/";
 public static readonly string[] Names={"Hero_LivingDining","Entry_North","Dining_Cabinet","Living_BirthdayWall","Balcony_South"};
 public static readonly Vector3[] Eyes={
  new Vector3(-6.46f,1.55f,-3.96f),new Vector3(-7.31f,1.55f,-1.10f),
  new Vector3(-7.28f,1.55f,-4.29f),new Vector3(-5.55f,1.55f,-6.30f),
  new Vector3(-6.30f,1.55f,-7.05f)};
 public static readonly Vector3[] Targets={
  new Vector3(-5.44f,1.17f,-7.46f),new Vector3(-7.15f,1.20f,-4.42f),
  new Vector3(-5.98f,1.38f,-2.45f),new Vector3(-5.18f,1.30f,-7.75f),
  new Vector3(-7.90f,1.32f,-9.95f)};
 public static void Before(){Capture(false);}public static void After(){Capture(true);}
 static void Capture(bool after){
  var sc=EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  if(!sc.IsValid())throw new Exception("HEFU_M0_SCENE_MISSING");
  var f=UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>();
  if(!f||!f.HasBakedPair)throw new Exception("HEFU_M0_DUAL_GI_MISSING");
  f.ApplyMode(false);
  var go=new GameObject("M0_EDITOR_ONLY_5VIEW_CAMERA");var cam=go.AddComponent<Camera>();
  cam.fieldOfView=60;cam.nearClipPlane=.04f;cam.farClipPlane=110;cam.allowHDR=false;
  const int w=960,h=640;
  Directory.CreateDirectory(Prefix+(after?"after":"before"));
  for(int i=0;i<Names.Length;i++){
   var eye=Eyes[i];var look=Targets[i];cam.transform.position=eye;
   cam.transform.rotation=Quaternion.LookRotation((look-eye).normalized,Vector3.up);
   var roof=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();if(roof)roof.UpdateVisibility(eye);
   var rt=new RenderTexture(w,h,24);var tex=new Texture2D(w,h,TextureFormat.RGB24,false);var prev=RenderTexture.active;
   try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
    tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
    string path=Prefix+(after?"after":"before")+"/"+Names[i]+".png";
    File.WriteAllBytes(path,tex.EncodeToPNG());
    if(new FileInfo(path).Length<7000)throw new Exception("HEFU_M0_CAPTURE_TOO_SMALL_"+Names[i]);
    Debug.Log("HEFU_M0_CAPTURE_PASS "+(after?"AFTER":"BEFORE")+" view="+Names[i]+" eye="+eye+
      " target="+look+" FOV=60 resolution=960x640 bytes="+new FileInfo(path).Length);
   }finally{cam.targetTexture=null;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);}
  }
  UnityEngine.Object.DestroyImmediate(go);
  if(!after)Audit();
 }
 static void Audit(){
  var quality=QualitySettings.GetQualityLevel();var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");
  var lights=UnityEngine.Object.FindObjectsOfType<Light>();int dir=0,point=0,spot=0,shadow=0;
  foreach(var l in lights){if(l.type==LightType.Directional)dir++;if(l.type==LightType.Point)point++;if(l.type==LightType.Spot)spot++;if(l.shadows!=LightShadows.None)shadow++;}
  var light=LightmapSettings.lightmaps;var probes=LightmapSettings.lightProbes;
  Debug.Log("HEFU_M0_BASELINE_RENDERER pipeline="+(GraphicsSettings.renderPipelineAsset?GraphicsSettings.renderPipelineAsset.name:"BuiltIn")+
   " unity="+Application.unityVersion+" colorSpace="+PlayerSettings.colorSpace+
   " quality="+quality+" MSAA="+QualitySettings.antiAliasing+
   " shadowDistance="+QualitySettings.shadowDistance+
   " lightsDirectional="+dir+" point="+point+" spot="+spot+" shadowed="+shadow+
   " lightmaps="+light.Length+" lightmapWidth="+(light.Length>0&&light[0].lightmapColor?light[0].lightmapColor.width:0)+
   " probes="+(probes?probes.count:0)+" mobileFurniture="+house.GetComponentsInChildren<HefuMovable>(true).Length);
  foreach(var r in house.GetComponentsInChildren<Renderer>(true))
   if((r.name.Contains("CAD_WindowCell_")&&r.name.EndsWith("_glass"))||r.name=="DoorLeaf"&&r.transform.parent.parent.name=="Door_Kitchen"){
    var m=r.sharedMaterial;
    Debug.Log("HEFU_M0_GLASS "+r.name+" enabled="+r.enabled+" bounds="+r.bounds+
     " shader="+(m&&m.shader?m.shader.name:"NULL")+
     " queue="+(m?m.renderQueue:-1)+" alpha="+(m?m.color.a:-1)+
     " zwrite="+(m&&m.HasProperty("_ZWrite")?m.GetFloat("_ZWrite"):-1));
   }
  var sofa=house.transform.Find("Move_Sofa");
  foreach(var r in sofa.GetComponentsInChildren<MeshRenderer>(true)){
   if(r.name.Contains("HefuP1")||r.name.Contains("Sofa")||r.name.Contains("Cushion"))
    Debug.Log("HEFU_M0_SOFA_MESH "+r.name+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+
     " bounds="+r.bounds+" triangles="+(r.GetComponent<MeshFilter>()?.sharedMesh?.triangles.Length/3??0));
  }
 }
}
}
