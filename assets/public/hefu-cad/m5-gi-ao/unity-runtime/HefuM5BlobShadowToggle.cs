using UnityEngine;
namespace HefuVR {
[DisallowMultipleComponent]
public sealed class HefuM5BlobShadowToggle:MonoBehaviour {
 public MeshRenderer[] shadows;
 public bool visible=true;
 public void Apply(bool on) {
  visible=on;
  if(shadows==null)return;
  foreach(var r in shadows)if(r)r.enabled=on;
 }
 void OnEnable(){Apply(visible);}
}
}
