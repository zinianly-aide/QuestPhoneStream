using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HefuVR.Editor {
public static class HefuLuoyangAutumnQA {
 public static void Validate(){
  var d=new DateTime(2026,10,25);
  var noon=HefuLuoyangSolar.Calculate(d,12+14.0/60);
  if(Math.Abs(noon.elevation-43.3)>1.0 || Math.Abs(noon.azimuth-180)>6.0)
   throw new Exception("HEFU_SOLAR_NOON_INCORRECT elev="+noon.elevation+" az="+noon.azimuth);
  var morning=HefuLuoyangSolar.Calculate(d,9.5);
  var after=HefuLuoyangSolar.Default();
  if(morning.elevation<10||morning.elevation>40||morning.azimuth>=180)
   throw new Exception("HEFU_SOLAR_MORNING_INCORRECT");
  if(after.elevation<19||after.elevation>42||after.azimuth<=180||after.azimuth>250 ||after.toSun.z>=0 ||after.toSun.x>=0)
   throw new Exception("HEFU_SOLAR_AFTERNOON_INCORRECT elev="+after.elevation+" az="+after.azimuth);
  EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
  var sun=GameObject.Find("BakedLike_Sun_VisualPreview");
  if(!sun||!sun.GetComponent<Light>())throw new Exception("HEFU_SOLAR_LIGHT_NOT_PRESENT");
  if(Vector3.Angle(-sun.transform.forward,after.toSun)>.7f)
   throw new Exception("HEFU_SOLAR_SCENE_SUN_DIRECTION_MISMATCH");
  var sky=RenderSettings.skybox;
  if(!sky||!sky.mainTexture||!sky.mainTexture.name.Contains("LuoyangAutumn"))
   throw new Exception("HEFU_AUTUMN_SKY_NOT_INCLUDED");
  var outdoor=GameObject.Find(HefuSceneAtmosphereBuilder.RootName);
  if(!outdoor)throw new Exception("HEFU_AUTUMN_TREES_ROOT_MISSING");
  var earth=outdoor.transform.Find("Illustrative_AutumnGround");
  if(!earth||!earth.GetComponent<MeshRenderer>()||
    !earth.GetComponent<MeshRenderer>().sharedMaterial.mainTexture||
    earth.GetComponent<Collider>())
   throw new Exception("HEFU_AUTUMN_GROUND_NOT_INSTALLED_OR_HAS_COLLIDER");
  var bounds=earth.GetComponent<Renderer>().bounds;
  if(bounds.min.z > -10.8f ||bounds.max.z >= -10f ||bounds.size.x < 70f)
   throw new Exception("HEFU_AUTUMN_GROUND_WRONG_PLACE="+bounds);
  int autumn=0;
  foreach(var renderer in outdoor.GetComponentsInChildren<Renderer>(true))
   foreach(var material in renderer.sharedMaterials)
    if(material&&material.name.StartsWith("LuoyangAutumnFoliage_"))autumn++;
  if(autumn<20)throw new Exception("HEFU_AUTUMN_FOLIAGE_SUBMATERIALS_MISSING count="+autumn);
  if(RenderSettings.ambientLight.r<=RenderSettings.ambientLight.b)
   throw new Exception("HEFU_AUTUMN_AMBIENT_NOT_WARM");
  Debug.Log("HEFU_LUOYANG_AUTUMN_QA_PASS noonElevation="+noon.elevation.ToString("F1")+
    " afternoonElevation="+after.elevation.ToString("F1")+" afternoonAzimuth="+after.azimuth.ToString("F1")+
    " sunDirectionMatches=true groundTexture=true foliageSubmaterials="+autumn+" skybox=autumn warmAmbient=true");
 }
}
}
