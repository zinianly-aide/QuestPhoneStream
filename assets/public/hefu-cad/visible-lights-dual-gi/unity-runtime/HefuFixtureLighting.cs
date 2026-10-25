using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR {
/// <summary>Lights physically inside three visible fixtures. Two complete baked
/// variants are imported by the Editor and swapped atomically at runtime.
/// No extra realtime shadow casters or unsupported URP post effects.</summary>
public sealed class HefuFixtureLighting:MonoBehaviour {
 public HefuLightingPresets baked;
 public Light pendant0,pendant1,standingLamp,sun;
 public Renderer[] glowingDiffusers;
 public bool birthdayMode;
 public bool HasBakedPair=>baked&&baked.Ready;
 // HefuMobileViewer.Start is the authoritative state initializer; a second
 // Start() here would race with a restored Birthday preference and reset it
 // back to Day depending on Unity component Start execution order.
 public void ApplyMode(bool birthday){
  birthdayMode=birthday;
  SetFixtureVisuals(birthday);
  if(HasBakedPair){
   var lp=LightmapSettings.lightProbes;
   if(lp!=null && lp.count==baked.probesPerPreset){
    lp.bakedProbes=baked.Unflatten(birthday);
    LightProbes.Tetrahedralize();
   }
   LightmapSettings.lightmaps=baked.Lightmaps(birthday);
  }
 }
 public void SetFixtureVisuals(bool birthday){
  // Draw light from actual luminaires: two spots immediately under pendants,
  // and one shadow-free point inside the living floor lamp shade.
  if(pendant0){pendant0.intensity=birthday?1.80f:.85f;pendant0.range=3.6f;}
  if(pendant1){pendant1.intensity=birthday?1.80f:.85f;pendant1.range=3.6f;}
  if(standingLamp){standingLamp.intensity=birthday?1.30f:.72f;standingLamp.range=4.0f;}
  if(sun)sun.intensity=birthday?.26f:1.04f;
  RenderSettings.ambientMode=AmbientMode.Flat;
  RenderSettings.ambientLight=birthday?new Color(.47f,.43f,.39f):new Color(.80f,.78f,.73f);
  Color emission=birthday?new Color(2.15f,1.36f,.66f):new Color(1.12f,.72f,.36f);
  if(glowingDiffusers!=null)foreach(var r in glowingDiffusers)if(r){
   var mat=Application.isPlaying?r.material:r.sharedMaterial;
   if(mat.HasProperty("_EmissionColor")){
    mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",emission);
   }
  }
 }
}
}
