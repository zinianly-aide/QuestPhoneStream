using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR.Editor {
/// <summary>Mobile-safe, non-shadowed warm practical fill light preview.
/// GI / baked probes are P2 next gates, NOT implemented by this class.</summary>
public static class HefuP2InteriorLightBuilder {
 public const string RootName="Hefu_InteriorPracticalLights_P2Preview";
 static Transform Find(Transform root,string name){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;throw new Exception("HEFU_P2_LIGHT_ANCHOR_MISSING_"+name);}
 static void Add(Transform root,string id,Transform anchor,float intensity,float range,Color color){
  var go=new GameObject(id);go.transform.SetParent(root,false);
  go.transform.position=new Vector3(anchor.position.x,2.46f,anchor.position.z);
  var light=go.AddComponent<Light>();
  light.type=LightType.Point;light.color=color;light.intensity=intensity;
  light.range=range;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.Auto;
 }
 public static void Apply(Transform house){
  var old=house.Find(RootName);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var root=new GameObject(RootName).transform;root.SetParent(house,false);
  var table=Find(house,"Move_Dining_Table");
  var sofa=Find(house,"Move_Sofa");
  Add(root,"DiningWarmPendant_Unshadowed",table,.38f,3.5f,new Color(1f,.83f,.67f));
  Add(root,"LivingWarmBounce_Unshadowed",sofa,.27f,4.4f,new Color(1f,.88f,.76f));
  // The existing panoramic skybox now contributes to material specular ambient
  // reflection. Avoid costly runtime reflection captures on mobile/Quest.
  RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;
  RenderSettings.defaultReflectionResolution=128;
  RenderSettings.reflectionIntensity=.63f;
  QualitySettings.pixelLightCount=3;
  Debug.Log("HEFU_P2_INTERIOR_LIGHT_PASS nonshadowPoints=2 skyboxReflections=true"+
   " oneShadowedSun=true probesNotBaked=true originalMovablesPreserved=true");
 }
}
}
