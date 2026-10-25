using System;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor{
 public static class HefuM3QualityBuilder{
  public static void Apply(Transform house,HefuMobileViewer viewer,Camera cam){
   var root=house.Find(HefuBakedRoomReflections.Root);
   if(!root)throw new Exception("HEFU_M3_REFLECTION_ROOT_ABSENT");
   var volumes=root.GetComponentsInChildren<ReflectionProbe>(true);
   if(volumes.Length!=2)throw new Exception("HEFU_M3_EXPECTS_TWO_LOCAL_REFLECTIONS");
   foreach(var p in volumes){
    if(p.mode!=UnityEngine.Rendering.ReflectionProbeMode.Custom||
       p.resolution!=128||p.customBakedTexture==null)
     throw new Exception("HEFU_M3_INVALID_LOCAL_REFLECTION "+p.name);
   }
   var quality=viewer.GetComponent<HefuM3QualityController>();
   if(!quality)quality=viewer.gameObject.AddComponent<HefuM3QualityController>();
   quality.roomProbes=volumes;quality.sceneCamera=cam;quality.tier=HefuM3QualityController.Tier.Balanced;
   viewer.quality=quality;
   Debug.Log("HEFU_M3_QUALITY_READY customReflection=2 cubemap128=true"+
    " realtimeReflection=off battery=45fps balanced=60fps quality=72fps"+
    " perPixelLights=2/3/4 AA=off/2x/4x shadow=off/9m/13m"+
    " independentUserPrefs=true noMovableOrXrChanges=true");
  }
 }
}
