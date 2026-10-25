using UnityEngine;
namespace HefuVR.Editor {
/// <summary>Run the existing immutable geometry/orientation checks with P1/P2.</summary>
public static class HefuP1P2RegressionSuite {
 public static void Validate(){
  HefuInteriorRefinementQA.Validate();
  HefuRealismQA.Validate();
  HefuSceneAtmosphereQA.Validate();
  HefuKitchenOrientationQA.Validate();
  HefuFurnitureDetailQA.Validate();
  HefuPhotoViewsQA.Validate();
  Debug.Log("HEFU_P1P2_REGRESSION_SUITE_PASS suite=6 visualDeviceRequired=true");
 }
}
}
