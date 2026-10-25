using UnityEngine;
namespace HefuVR.Editor {
public static class HefuLuoyangAutumnRegression {
 public static void Validate(){
  HefuLuoyangAutumnQA.Validate();
  HefuP1P2RegressionSuite.Validate();
  Debug.Log("HEFU_LUOYANG_AUTUMN_FULL_REGRESSION_PASS solar=true foliage=true outdoorGround=true sixLegacyQAs=true");
 }
}
}
