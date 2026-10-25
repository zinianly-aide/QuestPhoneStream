using UnityEngine;
namespace HefuVR {
/// Five fixed M0 metric positions, runtime-safe and independent of editor.
public static class HefuM4PresentationPlan {
 public const int Width=2048,Height=1365;
 public const float Fov=60f;
 public struct Pose {
  public readonly string id;public readonly Vector3 eye,target;
  public Pose(string n,Vector3 e,Vector3 t){id=n;eye=e;target=t;}
 }
 public static readonly Pose[] Views={
  new Pose("Hero_LivingDining",new Vector3(-6.46f,1.55f,-3.96f),new Vector3(-5.44f,1.17f,-7.46f)),
  new Pose("Entry_North",new Vector3(-7.31f,1.55f,-1.10f),new Vector3(-7.15f,1.20f,-4.42f)),
  new Pose("Dining_Cabinet",new Vector3(-7.28f,1.55f,-4.29f),new Vector3(-5.98f,1.38f,-2.45f)),
  new Pose("Living_BirthdayWall",new Vector3(-5.55f,1.55f,-6.30f),new Vector3(-5.18f,1.30f,-7.75f)),
  new Pose("Balcony_South",new Vector3(-6.30f,1.55f,-7.05f),new Vector3(-7.90f,1.32f,-9.95f))
 };
}
}
