using UnityEngine;
namespace HefuVR.Editor {
public static class HefuWarmReferenceRegression {
 public static void Validate(){
  HefuWarmReferenceStyleQA.Validate();
  HefuLuoyangAutumnRegression.Validate();
  Debug.Log("HEFU_WARM_REFERENCE_FULL_REGRESSION_PASS sceneStyle=true originalSixQAs=true"+
   " luoyangSolar=true 17Movables=true windowsAndDoors=true");
 }
}
}
