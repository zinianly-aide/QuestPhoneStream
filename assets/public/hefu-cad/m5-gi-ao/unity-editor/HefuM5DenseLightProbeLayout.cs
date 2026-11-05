using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
/// Dedicated M5 candidate: preserve original 46 probes, add furniture-near
/// spatial samples and separately rebake. NOT release-ready Day/Birthday SH.
public static class HefuM5DenseLightProbeLayout {
 public const string Source="Assets/Hefu/Scenes/M5GiAoExperiments/M5_GI20_NoAO.unity";
 public const string Dense="Assets/Hefu/Scenes/M5GiAoExperiments/M5_GI20_DenseProbes.unity";
 public const string Blob="Assets/Hefu/Scenes/M5GiAoExperiments/M5_GI20_DenseProbes_Blob.unity";
 public static void StageDense(){
  if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Dense))throw new Exception("M5_DENSE_SCENE_PROTECTED");
  if(!AssetDatabase.CopyAsset(Source,Dense))throw new Exception("M5_DENSE_COPY_FAIL");
  var scene=EditorSceneManager.OpenScene(Dense,OpenSceneMode.Single);
  var h=GameObject.Find("Hefu_CAD_Architectural_Base_v0.2")?.transform;
  if(!h)throw new Exception("M5_DENSE_CAD_MISSING");
  var ms=h.GetComponentsInChildren<HefuMovable>(true);
  var g=h.GetComponentsInChildren<LightProbeGroup>(true).Single();
  if(ms.Length!=17||g.probePositions.Length!=46)throw new Exception("M5_DENSE_BASELINE");
  var points=g.probePositions.ToList();
  int visible=0;
  foreach(var m in ms.OrderBy(x=>x.name,StringComparer.Ordinal)){
   var rs=m.GetComponentsInChildren<MeshRenderer>(true);
   if(rs.Length==0)continue;
   visible++;
   var b=rs[0].bounds;
   foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
   float x=Mathf.Max(.12f,b.extents.x*.66f);
   float z=Mathf.Max(.12f,b.extents.z*.66f);
   var offsets=new[]{Vector2.zero,new Vector2(-x,-z),new Vector2(x,-z),
     new Vector2(-x,z),new Vector2(x,z)};
   foreach(var pos in offsets)foreach(float y in new[]{.22f,.78f,1.55f}){
    var point=g.transform.InverseTransformPoint(new Vector3(b.center.x+pos.x,y,b.center.z+pos.y));
    if(!points.Any(p=>Vector3.SqrMagnitude(p-point)<.17f*.17f))points.Add(point);
   }
  }
  if(visible!=14||points.Count<150||points.Count>350)
   throw new Exception("M5_DENSE_COUNT="+points.Count+" visible="+visible);
  g.probePositions=points.ToArray();
  EditorUtility.SetDirty(g);EditorSceneManager.MarkSceneDirty(scene);
  if(!EditorSceneManager.SaveScene(scene))throw new Exception("M5_DENSE_SAVE_FAIL");
  AssetDatabase.SaveAssets();
  Debug.Log("M5_DENSE_STAGE_PASS probes="+points.Count+
   " oldProbesPreserved46=true furniture17=true renderable14=true noOtherChanges=true");
 }
 public static void BakeDense(){
  var scene=EditorSceneManager.OpenScene(Dense,OpenSceneMode.Single);
  var group=UnityEngine.Object.FindObjectOfType<LightProbeGroup>();
  if(!group||group.probePositions.Length<150)throw new Exception("M5_DENSE_NOT_STAGED");
  var ls=Lightmapping.lightingSettings;
  if(!ls||ls.lightmapResolution!=20||
    new SerializedObject(ls).FindProperty("m_AO").boolValue)
    throw new Exception("M5_DENSE_GI_SETTINGS_NOT_NOAO20");
  var start=DateTime.UtcNow;
  bool ok=Lightmapping.Bake();
  var lp=LightmapSettings.lightProbes;
  var maps=LightmapSettings.lightmaps;
  var data=Lightmapping.lightingDataAsset;
  string path=data?AssetDatabase.GetAssetPath(data):"";
  Debug.Log("M5_DENSE_BAKE_METRICS ok="+ok+" sec="+
   (DateTime.UtcNow-start).TotalSeconds.ToString("F1")+
   " bakedProbes="+(lp?lp.count:0)+" atlases="+(maps!=null?maps.Length:0)+
   " atlasWidth="+(maps!=null&&maps.Length>0&&maps[0].lightmapColor?
      maps[0].lightmapColor.width:0)+" data="+path);
  if(!ok||!lp||lp.count!=group.probePositions.Length||maps==null||
    maps.Length<1||maps.Length>4||!path.Contains("M5_GI20_DenseProbes/"))
    throw new Exception("M5_DENSE_BAKE_FAILED");
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  AssetDatabase.SaveAssets();
  Debug.Log("M5_DENSE_BAKE_PASS probes="+lp.count+" independentData=true");
 }
}
}
