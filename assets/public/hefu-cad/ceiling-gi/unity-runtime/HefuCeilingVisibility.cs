using UnityEngine;
namespace HefuVR {
/// <summary>Roofed eye-level scenes vs roofless floor-plan overview.
/// Works from actual Unity camera world position, never persists as furniture.
/// No RenderPipeline-specific shader or expensive per-frame raycast.</summary>
[DefaultExecutionOrder(180)]
public sealed class HefuCeilingVisibility : MonoBehaviour {
 public Renderer ceiling;
 public Renderer cove;
 public Vector3 roomMin = new Vector3(-8.38f,0f,-8.02f);
 public Vector3 roomMax = new Vector3(-3.64f,2.7f,-.23f);
 public float cutawayHeight=2.47f;
 bool displayed;
 public bool IsRoofVisible=>displayed;
 void Awake(){SetVisible(false);}
 public void SetVisible(bool enabled){
  if(ceiling)ceiling.enabled=enabled;
  if(cove)cove.enabled=enabled;
  displayed=enabled;
 }
 public bool ShouldDisplay(Vector3 eye){
  // Slight horizontal tolerance for walking near but within the wall edge.
  return eye.y>.38f && eye.y<cutawayHeight &&
   eye.x>roomMin.x-.23f && eye.x<roomMax.x+.23f &&
   eye.z>roomMin.z-.28f && eye.z<roomMax.z+.28f;
 }
 public void UpdateVisibility(Vector3 eye){
  bool expected=ShouldDisplay(eye);
  if(expected!=displayed)SetVisible(expected);
 }
 void LateUpdate(){UpdateVisibility(transform.position);}
}
}
