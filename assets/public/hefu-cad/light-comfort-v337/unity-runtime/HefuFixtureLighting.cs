using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR {
/// <summary>Visible luminaire lighting with two mobile-friendly prebaked states.
/// Cache the 2 arrays/46 SH sets on first use: no repeated SH decompression,
/// allocations or probe tetrahedralization on every theme button press.</summary>
public sealed class HefuFixtureLighting:MonoBehaviour {
 public HefuLightingPresets baked;
 public Light pendant0,pendant1,standingLamp,sun;
 public Renderer[] glowingDiffusers;
 public bool birthdayMode;
 public bool HasBakedPair=>baked&&baked.Ready;
 LightmapData[] dayMaps,birthdayMaps;
 SphericalHarmonicsL2[] dayProbes,birthdayProbes;
 Material[] emissiveInstanceMaterials;
 bool cacheReady,hasApplied;
 public int CacheBuildCount {get;private set;}
 public int ModeApplyCount {get;private set;}
 public bool CacheReady=>cacheReady;

 void EnsureCache(){
  if(cacheReady||!HasBakedPair)return;
  dayMaps=baked.Lightmaps(false);birthdayMaps=baked.Lightmaps(true);
  dayProbes=baked.Unflatten(false);birthdayProbes=baked.Unflatten(true);
  if(dayProbes.Length!=baked.probesPerPreset||birthdayProbes.Length!=baked.probesPerPreset)
   throw new System.InvalidOperationException("Hefu light probe mismatch");
  if(glowingDiffusers!=null){
   emissiveInstanceMaterials=new Material[glowingDiffusers.Length];
   for(int i=0;i<glowingDiffusers.Length;i++)if(glowingDiffusers[i]){
    // Unique renderer instance ONCE on the phone. Never make a material per
    // tap; the two dining undersides share an asset but not mutable runtime state.
    var m=Application.isPlaying?glowingDiffusers[i].material:glowingDiffusers[i].sharedMaterial;
    emissiveInstanceMaterials[i]=m;
   }
  }
  cacheReady=true;CacheBuildCount++;
 }
 public void ApplyMode(bool birthday){
  if(hasApplied && birthdayMode==birthday)return;
  birthdayMode=birthday;
  EnsureCache();
  SetFixtureVisuals(birthday);
  if(cacheReady){
   var lp=LightmapSettings.lightProbes;
   if(lp!=null&&lp.count==baked.probesPerPreset){
    // The probe POSITIONS and tetrahedra never change between presets.
    // Updating coefficients invalidates the lighting values, not topology.
    lp.bakedProbes=birthday?birthdayProbes:dayProbes;
   }
   LightmapSettings.lightmaps=birthday?birthdayMaps:dayMaps;
  }
  hasApplied=true;ModeApplyCount++;
 }
 public void SetFixtureVisuals(bool birthday){
  // Brighter but still physically located at the three glowing fixtures.
  if(pendant0){pendant0.intensity=birthday?2.12f:1.14f;pendant0.range=4.05f;}
  if(pendant1){pendant1.intensity=birthday?2.12f:1.14f;pendant1.range=4.05f;}
  if(standingLamp){standingLamp.intensity=birthday?1.52f:.90f;standingLamp.range=4.0f;}
  if(sun)sun.intensity=birthday?.29f:1.13f;
  RenderSettings.ambientMode=AmbientMode.Flat;
  RenderSettings.ambientLight=birthday?new Color(.50f,.46f,.41f):new Color(.84f,.81f,.76f);
  Color emission=birthday?new Color(2.00f,1.30f,.64f):new Color(.98f,.69f,.38f);
  if(glowingDiffusers!=null)for(int i=0;i<glowingDiffusers.Length;i++)if(glowingDiffusers[i]){
   var mat=emissiveInstanceMaterials!=null&&i<emissiveInstanceMaterials.Length&&emissiveInstanceMaterials[i]
      ?emissiveInstanceMaterials[i]
      :Application.isPlaying?glowingDiffusers[i].material:glowingDiffusers[i].sharedMaterial;
   if(mat.HasProperty("_EmissionColor")){
    mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",emission);
   }
  }
 }
}
}
