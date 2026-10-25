using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;
namespace HefuVR {
/// Opt-in offscreen, no-UI scene exports and measured on-device tier benchmark.
public sealed class HefuM4Presentation:MonoBehaviour {
 public Camera sourceCamera;
 public HefuM3QualityController quality;
 public HefuCeilingVisibility ceiling;
 public bool Busy {get;private set;}
 public string Status {get;private set;}="M4 ready";
 public string LastExportDirectory {get;private set;}="";
 public string LastBenchmarkCSV {get;private set;}="";
 public static string Csv(string value){return "\""+(value??"").Replace("\"","\"\"")+"\"";}
 public static float Percentile(List<float> sorted,float q){
  if(sorted==null||sorted.Count==0)return 0;
  float x=Mathf.Clamp01(q)*(sorted.Count-1);
  int a=Mathf.FloorToInt(x),b=Mathf.Min(a+1,sorted.Count-1);
  return Mathf.Lerp(sorted[a],sorted[b],x-a);
 }
 public void ExportFiveViews(){if(!Busy)StartCoroutine(ExportRoutine());}
 public void BenchmarkThreeTiers(){if(!Busy)StartCoroutine(BenchmarkRoutine());}
 IEnumerator ExportRoutine(){
  Busy=true;GameObject helper=null;
  try {
   if(!sourceCamera||!quality)throw new InvalidOperationException("Missing camera/quality");
   string stamp=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss",CultureInfo.InvariantCulture);
   string dir=Path.Combine(Application.persistentDataPath,"Exhibits","M4_"+stamp);
   Directory.CreateDirectory(dir);LastExportDirectory=dir;
   helper=new GameObject("M4_TEMP_EDITOR_OR_PHONE_CAPTURE");
   var cam=helper.AddComponent<Camera>();cam.CopyFrom(sourceCamera);cam.enabled=false;
   cam.fieldOfView=HefuM4PresentationPlan.Fov;cam.allowHDR=false;
   var manifest=new System.Text.StringBuilder("id,file,width,height,fov,eyeX,eyeY,eyeZ,targetX,targetY,targetZ,tier,utc\n");
   foreach(var pose in HefuM4PresentationPlan.Views){
    Status="Export "+pose.id;
    yield return new WaitForEndOfFrame();
    cam.transform.position=pose.eye;
    cam.transform.rotation=Quaternion.LookRotation((pose.target-pose.eye).normalized);
    if(ceiling)ceiling.UpdateVisibility(pose.eye);
    RenderTexture rt=null;Texture2D tex=null;
    RenderTexture restore=RenderTexture.active;
    try{
     rt=new RenderTexture(HefuM4PresentationPlan.Width,HefuM4PresentationPlan.Height,24);
     rt.Create();cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
     tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
     tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
     var bytes=tex.EncodeToPNG();if(bytes==null||bytes.Length<7000)throw new InvalidDataException("Empty frame");
     string file="HefuVR_M4_"+pose.id+"_"+stamp+".png";
     File.WriteAllBytes(Path.Combine(dir,file),bytes);
     bool gallery=HefuPhotoComparison.TrySaveGallery(file,bytes);
     var n=CultureInfo.InvariantCulture;
     manifest.Append(pose.id).Append(',').Append(Csv(file)).Append(',').Append(rt.width).Append(',').Append(rt.height).Append(',')
      .Append(HefuM4PresentationPlan.Fov.ToString("F1",n)).Append(',')
      .Append(pose.eye.x.ToString("F3",n)).Append(',').Append(pose.eye.y.ToString("F3",n)).Append(',').Append(pose.eye.z.ToString("F3",n)).Append(',')
      .Append(pose.target.x.ToString("F3",n)).Append(',').Append(pose.target.y.ToString("F3",n)).Append(',').Append(pose.target.z.ToString("F3",n)).Append(',')
      .Append(quality.CurrentName).Append(',').Append(stamp).Append('\n');
     Debug.Log("HEFU_M4_SHOT_PASS "+pose.id+" bytes="+bytes.Length+" gallery="+gallery);
    }finally{
     cam.targetTexture=null;RenderTexture.active=restore;
     if(rt){rt.Release();Destroy(rt);}if(tex)Destroy(tex);
     if(ceiling)ceiling.UpdateVisibility(sourceCamera.transform.position);
    }
   }
   File.WriteAllText(Path.Combine(dir,"manifest.csv"),manifest.ToString());
   Status="5 images saved to Gallery/HefuVR or app Exhibits";
   Debug.Log("HEFU_M4_EXPORT_COMPLETE fiveViews=true manifest="+dir);
  }finally {if(ceiling&&sourceCamera)ceiling.UpdateVisibility(sourceCamera.transform.position);
   if(helper)Destroy(helper);Busy=false;}
 }
 IEnumerator BenchmarkRoutine(){
  Busy=true;
  if(!quality){Status="Benchmark requires quality controller";Busy=false;yield break;}
  var original=quality.SelectedTier;
  var csv=new System.Text.StringBuilder("utc,device,gpu,resolution,tier,frames,seconds,mean_fps,p50_ms,p95_ms,p99_ms,max_ms,mono_mib,allocated_mib\n");
  try{
   foreach(var tier in new[]{HefuM3QualityController.Tier.Battery,HefuM3QualityController.Tier.Balanced,HefuM3QualityController.Tier.Quality}){
    quality.Apply(tier);Status="Warmup "+tier;
    float warm=0;
    while(warm<1f){yield return null;warm+=Mathf.Max(0,Time.unscaledDeltaTime);}
    var frameTimes=new List<float>(512);float elapsed=0,max=0;
    Status="Benchmark "+tier;
    while(elapsed<6f){yield return null;
     float dt=Time.unscaledDeltaTime;
     if(dt<=.00001f||dt>.5f)continue;
     elapsed+=dt;float ms=dt*1000f;frameTimes.Add(ms);max=Mathf.Max(max,ms);
    }
    if(frameTimes.Count<20)throw new InvalidOperationException("Insufficient frame samples");
    frameTimes.Sort();var n=CultureInfo.InvariantCulture;
    csv.Append(DateTime.UtcNow.ToString("s",n)).Append(',')
     .Append(Csv(SystemInfo.deviceModel)).Append(',').Append(Csv(SystemInfo.graphicsDeviceName)).Append(',')
     .Append(Csv(Screen.width+"x"+Screen.height)).Append(',').Append(tier).Append(',')
     .Append(frameTimes.Count).Append(',').Append(elapsed.ToString("F3",n)).Append(',')
     .Append((frameTimes.Count/elapsed).ToString("F2",n)).Append(',')
     .Append(Percentile(frameTimes,.50f).ToString("F2",n)).Append(',')
     .Append(Percentile(frameTimes,.95f).ToString("F2",n)).Append(',')
     .Append(Percentile(frameTimes,.99f).ToString("F2",n)).Append(',')
     .Append(max.ToString("F2",n)).Append(',')
     .Append((Profiler.GetMonoUsedSizeLong()/1048576f).ToString("F2",n)).Append(',')
     .Append((Profiler.GetTotalAllocatedMemoryLong()/1048576f).ToString("F2",n)).Append('\n');
    Debug.Log("HEFU_M4_DEVICE_BENCHMARK "+tier+" samples="+frameTimes.Count);
   }
   string folder=Path.Combine(Application.persistentDataPath,"M4Benchmarks");
   Directory.CreateDirectory(folder);
   LastBenchmarkCSV=Path.Combine(folder,"HefuVR_M4_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss",CultureInfo.InvariantCulture)+".csv");
   File.WriteAllText(LastBenchmarkCSV,csv.ToString());
   Status="Benchmark done: CSV saved in app M4Benchmarks";
   Debug.Log("HEFU_M4_BENCHMARK_COMPLETE "+LastBenchmarkCSV);
  }finally{quality.Apply(original);Busy=false;}
 }
}
}
