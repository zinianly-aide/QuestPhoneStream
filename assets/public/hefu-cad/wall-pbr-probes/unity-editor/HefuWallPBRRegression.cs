using UnityEngine;
namespace HefuVR.Editor {
public static class HefuWallPBRRegression {
 public static void Validate(){
  HefuWallPBRQA.Validate();
  HefuWarmReferenceRegression.Validate();
  Debug.Log("HEFU_WALL_PBR_FULL_REGRESSION_PASS PBR=true CAD=true Glass=true Solar=true Sofa=true Movables17=true");
 }
}
}
