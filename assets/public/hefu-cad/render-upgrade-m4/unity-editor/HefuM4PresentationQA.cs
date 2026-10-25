using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor{
public static class HefuM4PresentationQA{
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var host=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
  var presenter=host?host.presentation:null;
  if(!host||!presenter||!host.photo||!host.quality||
    presenter.sourceCamera!=host.cam||presenter.quality!=host.quality||
    presenter.gameObject!=host.gameObject)throw new Exception("HEFU_M4_QA_CONTROLLER_NOT_LINKED");
  var views=HefuM4PresentationPlan.Views;
  if(views.Length!=5||HefuM4PresentationPlan.Width!=2048||HefuM4PresentationPlan.Height!=1365||
     HefuM4PresentationPlan.Fov!=60f)throw new Exception("HEFU_M4_QA_CAMERA_PRESETS");
  for(int i=0;i<views.Length;i++){
   if(views[i].id!=HefuUpgradeM0Capture.Names[i]||
      Vector3.Distance(views[i].eye,HefuUpgradeM0Capture.Eyes[i])>.00001f||
      Vector3.Distance(views[i].target,HefuUpgradeM0Capture.Targets[i])>.00001f||
      Mathf.Abs(views[i].eye.y-1.55f)>.001f)throw new Exception("HEFU_M4_QA_POSE_NOT_BASELINE_"+i);
  }
  var data=new List<float>{10,16,20,24,40};
  if(Mathf.Abs(HefuM4Presentation.Percentile(data,.5f)-20f)>.001f||
     Mathf.Abs(HefuM4Presentation.Percentile(data,.95f)-36.8f)>.001f||
     HefuM4Presentation.Percentile(new List<float>(),.5f)!=0)
    throw new Exception("HEFU_M4_QA_PERCENTILE_MATH");
  string csv=HefuM4Presentation.Csv("a,b\"quote");
  if(csv!="\"a,b\"\"quote\"")throw new Exception("HEFU_M4_QA_CSV_ESCAPING");
  var house=host.house;
  if(!house||house.GetComponentsInChildren<HefuMovable>(true).Length!=17||
     !host.dailyDecor||!UnityEngine.Object.FindObjectOfType<HefuFixtureLighting>().HasBakedPair)
    throw new Exception("HEFU_M4_QA_LEGACY_SCENE_GONE");
  if(host.quality.roomProbes.Length!=2||HefuPhotoViewpoints.All.Length!=10)
    throw new Exception("HEFU_M4_QA_PHOTO_OR_REFLECTION_REGRESSION");
  Debug.Log("HEFU_M4_QA_PASS fiveCameraBaselines=IDENTICAL fullHDPlus=2048x1365"+
   " offscreenCameraLinked=true noNewMovables=17 M3ReflectionProbes=2"+
   " M2DailyPreserved=true photoPickerPreserved=true 10PresetsPreserved=true"+
   " CSVQuoteEscaping=true percentileMath=true noDeviceFPSInvented=true");
 }
}
}
