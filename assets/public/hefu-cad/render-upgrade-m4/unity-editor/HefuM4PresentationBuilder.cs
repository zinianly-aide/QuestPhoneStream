using System;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor {
 public static class HefuM4PresentationBuilder {
  public static void Apply(Transform house,HefuMobileViewer viewer,Camera cam){
   if(!viewer||!viewer.photo||!viewer.quality||!cam)throw new Exception("HEFU_M4_MISSING_COMPONENT");
   var component=viewer.GetComponent<HefuM4Presentation>();
   if(!component)component=viewer.gameObject.AddComponent<HefuM4Presentation>();
   component.sourceCamera=cam;component.quality=viewer.quality;
   component.ceiling=UnityEngine.Object.FindObjectOfType<HefuCeilingVisibility>();
   viewer.presentation=component;
   if(HefuM4PresentationPlan.Views.Length!=5)throw new Exception("HEFU_M4_EXPECT_FIVE_VIEWS");
   Debug.Log("HEFU_M4_PRESENTATION_READY views=5 png=2048x1365 screenshotOffscreen=true"+
     " bench=Battery/Balanced/Quality timePerTier=6s noAutoBenchmark=true");
  }
 }
}
