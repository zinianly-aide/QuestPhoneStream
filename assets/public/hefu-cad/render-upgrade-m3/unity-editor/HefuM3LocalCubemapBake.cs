using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor{
/// <summary>Editor-only true LOCAL 6-face scene capture. A directional
/// Lightmapping.BakeReflectionProbe on this setup output identical sky-only
/// EXRs; direct camera RenderToCubemap captures actual walls and furniture.</summary>
public static class HefuM3LocalCubemapBake{
 const string Dir="Assets/Hefu/Generated/Reflections/";
 static void Run(string room,Vector3 location){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var house=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");if(!house)throw new Exception("HEFU_M3_NO_HOUSE");
  var existing=house.transform.Find(HefuBakedRoomReflections.Root);
  bool before=existing&&existing.gameObject.activeSelf;
  if(existing)existing.gameObject.SetActive(false);
  var go=new GameObject("HEFU_EDITOR_ONLY_M3_CUBE_CAMERA");go.transform.position=location;
  var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.Skybox;
  cam.nearClipPlane=.04f;cam.farClipPlane=45f;cam.allowHDR=false;
  var cube=new Cubemap(128,TextureFormat.RGBA32,true){name="Hefu_M3_"+room+"_Local128"};
  try{
   var ok=cam.RenderToCubemap(cube);
   if(!ok)throw new Exception("HEFU_M3_RENDER_TO_CUBEMAP_FALSE");
   cube.Apply(true,false);
   double luminance=0,energy=0;int count=0;
   foreach(CubemapFace f in new[]{CubemapFace.PositiveX,CubemapFace.NegativeX,CubemapFace.PositiveY,
    CubemapFace.NegativeY,CubemapFace.PositiveZ,CubemapFace.NegativeZ}){
    foreach(var c in cube.GetPixels(f)){
     luminance+=c.r*.2126+c.g*.7152+c.b*.0722;
     energy+=(c.r-c.g)*(c.r-c.g)+(c.g-c.b)*(c.g-c.b);count++;
    }
   }
   double avg=luminance/count;
   if(avg<.01||energy/count<.00001)throw new Exception("HEFU_M3_EMPTY_CUBE avg="+avg);
   var path=Dir+cube.name+".asset";
   var old=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
   if(old){EditorUtility.CopySerialized(cube,old);UnityEngine.Object.DestroyImmediate(cube);cube=old;EditorUtility.SetDirty(cube);}
   else AssetDatabase.CreateAsset(cube,path);
   AssetDatabase.SaveAssets();
   Debug.Log("HEFU_M3_LOCAL_CAPTURE_PASS room="+room+" cube=128 format=RGBA32"+
    " pixels="+count+" average="+avg.ToString("F5")+" chromaVariance="+(energy/count).ToString("F5")+
    " materialAsset="+path+" EditorOnly=true realtimeOnPhone=false");
  }finally{if(existing)existing.gameObject.SetActive(before);UnityEngine.Object.DestroyImmediate(go);}
 }
 public static void Living(){Run("Living",new Vector3(-5.9f,1.33f,-6.23f));}
 public static void Dining(){Run("Dining",new Vector3(-5.97f,1.33f,-2.77f));}
}
}
