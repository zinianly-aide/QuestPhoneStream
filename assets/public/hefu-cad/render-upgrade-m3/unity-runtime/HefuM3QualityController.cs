using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR{
/// <summary>Portable Built-in renderer quality tiers. Offline room reflection
/// cubemaps are never re-rendered on mobile. Completely independent of layout,
/// birthday, Day/Birthday GI and Quest XR camera controls.</summary>
public sealed class HefuM3QualityController:MonoBehaviour{
 public enum Tier { Battery=0, Balanced=1, Quality=2 }
 const string QualityPref="Hefu_M3_QualityTier_v1";
 const string HudPref="Hefu_M3_PerformanceHUD_v1";
 public ReflectionProbe[] roomProbes;
 public Camera sceneCamera;
 public Tier tier=Tier.Balanced;
 public bool showPerformance;
 int sampleFrames;
 float sampleSeconds;
 float displayedFps;
 float displayedMs;
 public bool Initialized {get;private set;}
 public Tier SelectedTier=>tier;
 public float SampledFPS=>displayedFps;
 public float SampledFrameMs=>displayedMs;
 public string CurrentName=>tier==Tier.Battery?"Battery":tier==Tier.Balanced?"Balanced":"Quality";
 public void Initialize(){
  tier=(Tier)Mathf.Clamp(PlayerPrefs.GetInt(QualityPref,(int)Tier.Balanced),0,2);
  showPerformance=PlayerPrefs.GetInt(HudPref,0)!=0;
  Apply(tier);Initialized=true;
 }
 public void Next(){
  Apply((Tier)(((int)tier+1)%3));
  PlayerPrefs.SetInt(QualityPref,(int)tier);PlayerPrefs.Save();
 }
 public void TogglePerformance(){
  showPerformance=!showPerformance;
  PlayerPrefs.SetInt(HudPref,showPerformance?1:0);PlayerPrefs.Save();
 }
 /// <summary>Do not persist when invoked from a diagnostic or editor QA.</summary>
 public void Apply(Tier next){
  tier=next;
  QualitySettings.realtimeReflectionProbes=false;
  QualitySettings.masterTextureLimit=tier==Tier.Battery?1:0;
  QualitySettings.antiAliasing=tier==Tier.Battery?0:tier==Tier.Balanced?2:4;
  QualitySettings.pixelLightCount=tier==Tier.Battery?2:tier==Tier.Balanced?3:4;
  QualitySettings.shadows=tier==Tier.Battery?ShadowQuality.Disable:ShadowQuality.All;
  QualitySettings.shadowDistance=tier==Tier.Battery?0f:tier==Tier.Balanced?9f:13f;
  QualitySettings.shadowResolution=tier==Tier.Quality?ShadowResolution.High:ShadowResolution.Medium;
  QualitySettings.anisotropicFiltering=tier==Tier.Battery?AnisotropicFiltering.Disable:AnisotropicFiltering.Enable;
  if(roomProbes!=null)foreach(var p in roomProbes)if(p){
   // No Custom -> Realtime mode anywhere in the tier state machine.
   p.enabled=tier!=Tier.Battery;
  }
  if(sceneCamera)sceneCamera.allowHDR=tier==Tier.Quality;
  Application.targetFrameRate=tier==Tier.Battery?45:tier==Tier.Balanced?60:72;
 }
 void Update(){
  if(!showPerformance)return;
  float delta=Time.unscaledDeltaTime;
  if(delta<=.00001f||delta>.5f)return;
  sampleFrames++;sampleSeconds+=delta;
  if(sampleSeconds>=.75f){
   displayedFps=sampleFrames/sampleSeconds;
   displayedMs=1000f*sampleSeconds/sampleFrames;
   sampleFrames=0;sampleSeconds=0;
  }
 }
 public string PerformanceLabel=>"Actual device FPS "+displayedFps.ToString("F1")+
  " | frame "+displayedMs.ToString("F1")+" ms | "+CurrentName;
}
}
