using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>Offline captured 128px localized cubemaps (NO realtime scene capture).
/// Captured by HefuM3LocalCubemapBake on Mac GPU; mobile uses Custom mode.
/// Captures include current roofless design-preview environment, not GI.</summary>
public static class HefuBakedRoomReflections {
 public const string Root="Hefu_StaticRoomReflections_v1";
 static void Install(Transform parent,string name,string cubemap,Vector3 center,Vector3 volume){
  var texture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Hefu/Generated/Reflections/"+cubemap);
  if(!texture)throw new Exception("HEFU_REFLECTION_CUBE_MISSING_"+cubemap);
  var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=center;
  var p=g.AddComponent<ReflectionProbe>();p.mode=ReflectionProbeMode.Custom;
  p.customBakedTexture=texture;p.resolution=128;p.size=volume;p.boxProjection=true;
  p.intensity=.52f;p.importance=1;p.hdr=true;p.nearClipPlane=.05f;p.farClipPlane=20;
  if(p.customBakedTexture!=texture)throw new Exception("HEFU_REFLECTION_CUBE_NOT_LINKED");
 }
 public static void Apply(Transform house){
  var old=house.Find(Root);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var root=new GameObject(Root).transform;root.SetParent(house,false);
  Install(root,"Living_StaticReflection128","Hefu_M3_Living_Local128.asset",
    new Vector3(-5.9f,1.33f,-6.23f),new Vector3(4.15f,2.7f,5.7f));
  Install(root,"Dining_StaticReflection128","Hefu_M3_Dining_Local128.asset",
    new Vector3(-5.97f,1.33f,-2.77f),new Vector3(4.7f,2.7f,4.1f));
  Debug.Log("HEFU_BAKED_REFLECTION_PASS customCubemaps=2 resolution=128 sceneLocal=true"+
    " realtimeCaptures=0 GI_NOT_BAKED=true");
 }
}
}
