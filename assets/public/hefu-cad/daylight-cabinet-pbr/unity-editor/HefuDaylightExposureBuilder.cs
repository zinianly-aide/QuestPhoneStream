using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>Bright but non-flat residential daylight preview for enclosed rooms.
/// Adjusts two ALREADY EXISTING non-shadow practical lights; no extra per-pixel
/// lights, no post gamma filter, no additional shadows; baked GI will sample it.</summary>
public static class HefuDaylightExposureBuilder {
 public static void Apply(Transform house){
  var root=house.Find(HefuP2InteriorLightBuilder.RootName);
  if(!root)throw new Exception("HEFU_EXPOSURE_PREREQUISITE_MISSING");
  var dining=root.Find("DiningWarmPendant_Unshadowed");
  var living=root.Find("LivingWarmBounce_Unshadowed");
  if(!dining||!living)throw new Exception("HEFU_EXPOSURE_LIGHT_ANCHOR_MISSING");
  var dl=dining.GetComponent<Light>();var ll=living.GetComponent<Light>();
  if(!dl||!ll||dl.type!=LightType.Point||ll.type!=LightType.Point)
   throw new Exception("HEFU_EXPOSURE_ORIGINAL_LIGHTS_CHANGED");
  // User-requested usable daytime brightness; does not require fixture UI.
  dl.intensity=1.15f;dl.range=5.2f;dl.shadows=LightShadows.None;
  dl.color=new Color(1f,.90f,.80f);
  ll.intensity=.92f;ll.range=5.4f;ll.shadows=LightShadows.None;
  ll.color=new Color(1f,.91f,.83f);
  // Uniform hemisphere sky contributes neutral ambient to dynamic assets;
  // real wall occlusion continues to come from static-baked lightmaps.
  RenderSettings.ambientMode=AmbientMode.Flat;
  RenderSettings.ambientLight=new Color(.84f,.814f,.770f);
  RenderSettings.reflectionIntensity=.68f;
  QualitySettings.pixelLightCount=3;
  var sun=GameObject.Find("BakedLike_Sun_VisualPreview");
  if(sun&&sun.GetComponent<Light>()){
   var key=sun.GetComponent<Light>();key.intensity=1.04f;
   key.shadows=LightShadows.Soft;
  }
  Debug.Log("HEFU_DAYLIGHT_EXPOSURE_PASS dining=1.15 living=.92"+
   " ambient=(.84,.814,.77) sun=1.04 extraLights=0"+
   " mobilePixelLights=3 realtimeExtraShadows=0");
 }
}
}
