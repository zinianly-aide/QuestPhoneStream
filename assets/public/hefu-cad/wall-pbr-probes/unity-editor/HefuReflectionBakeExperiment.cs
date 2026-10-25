using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HefuVR.Editor {
public static class HefuReflectionBakeExperiment {
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var room=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2");if(!room)throw new Exception("REFLECTION_ROOT_MISSING");
  string path="Assets/Hefu/Generated/Reflections/Hefu_Dining_64.exr";
  Directory.CreateDirectory("Assets/Hefu/Generated/Reflections");
  var go=new GameObject("HEFU_PROBE_BAKE_TEMP");
  go.transform.position=new Vector3(-5.97f,1.33f,-2.77f);
  var probe=go.AddComponent<ReflectionProbe>();
  probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Baked;
  probe.resolution=64;probe.size=new Vector3(4.7f,2.7f,4.1f);
  probe.intensity=.72f;probe.hdr=true;
  var start=DateTime.UtcNow;
  bool success=Lightmapping.BakeReflectionProbe(probe,path);
  Debug.Log("HEFU_REFLECTION_BAKE_EXPERIMENT_PASS="+success+" sec="+(DateTime.UtcNow-start).TotalSeconds.ToString("F1")+
    " cubemapExists="+File.Exists(path)+" bytes="+(File.Exists(path)?new FileInfo(path).Length:0)+
    " loaded="+(AssetDatabase.LoadAssetAtPath<Cubemap>(path)!=null));
  UnityEngine.Object.DestroyImmediate(go);
  if(!success ||AssetDatabase.LoadAssetAtPath<Cubemap>(path)==null)throw new Exception("HEFU_REFLECTION_BAKE_FAILED");
 }
}
}
