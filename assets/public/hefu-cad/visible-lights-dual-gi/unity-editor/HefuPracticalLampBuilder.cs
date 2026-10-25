using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HefuVR;
namespace HefuVR.Editor {
/// <summary>Physically attaches three mobile-safe lights to visible fixtures:
/// two dining pendants and one floor lamp. Existing invisible fill lights are
/// removed. Glow is rendered as emissive geometry, not a post-process halo.</summary>
public static class HefuPracticalLampBuilder {
 public const string RootName="Hefu_VisibleLuminaires_v1";
 const string AssetDir="Assets/Hefu/Generated/VisibleLamps/";
 static Material M(string id,Color c,float gloss,bool glow){
  Directory.CreateDirectory(AssetDir);string p=AssetDir+id+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(p);
  if(!m){m=new Material(Shader.Find("Standard"));m.name=id;AssetDatabase.CreateAsset(m,p);}
  m.color=c;m.SetFloat("_Metallic",id.Contains("Brass")?.52f:0f);m.SetFloat("_Glossiness",gloss);
  if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.38f,.25f,.13f));}
  EditorUtility.SetDirty(m);return m;
 }
 static Transform Primitive(Transform parent,string name,PrimitiveType kind,Vector3 pos,Vector3 size,Material mat){
  var g=GameObject.CreatePrimitive(kind);g.name=name;g.transform.SetParent(parent,false);
  g.transform.position=pos;g.transform.localScale=size;
  g.GetComponent<Renderer>().sharedMaterial=mat;
  UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
  g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
  return g.transform;
 }
 static Light Spot(Transform parent,Vector3 pos,string name){
  var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=pos;
  go.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
  var l=go.AddComponent<Light>();l.type=LightType.Spot;l.spotAngle=72f;
  l.range=3.6f;l.intensity=.85f;l.color=new Color(1f,.83f,.66f);
  l.shadows=LightShadows.None;l.renderMode=LightRenderMode.Auto;
  return l;
 }
 public static void Apply(Transform house,Camera camera){
  var warm=house.Find(HefuWarmReferenceStyleBuilder.GroupName);
  var sofa=house.Find("Move_Sofa");
  var old=house.Find(HefuP2InteriorLightBuilder.RootName);
  if(!warm||!sofa||!old)throw new Exception("HEFU_LIGHT_FIXTURE_EXISTING_MODELS_MISSING");
  var a=warm.Find("DiningPendant_0");var b=warm.Find("DiningPendant_1");
  if(!a||!b)throw new Exception("HEFU_LIGHT_FIXTURE_PENDANTS_MISSING");
  // Destroy the detached invisible point lights, never quietly double-light.
  foreach(var l in old.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(l.gameObject);
  var existing=house.Find(RootName);if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);
  var root=new GameObject(RootName).transform;root.SetParent(house,false);
  var bulb=M("HefuWarmEmissiveDiffuser",new Color(1f,.92f,.79f),.36f,true);
  var brass=M("HefuVisibleBrassStem",new Color(.59f,.46f,.29f),.55f,false);
  var linen=M("HefuFloorLinenShadeLit",new Color(.91f,.86f,.73f),.21f,true);
  var glowing=new List<Renderer>();
  foreach(var p in new[]{a,b}){
   var diffuser=p.Find("WarmDiffuserUnderShade");
   if(!diffuser)throw new Exception("HEFU_LAMP_DIFFUSER_MISSING "+p.name);
   diffuser.GetComponent<Renderer>().sharedMaterial=bulb;
   glowing.Add(diffuser.GetComponent<Renderer>());
  }
  var s0=Spot(a,a.position+new Vector3(0,1.974f,0),"PendantSpot_0");
  var s1=Spot(b,b.position+new Vector3(0,1.974f,0),"PendantSpot_1");
  // Sofa bounds are the CAD anchor: a floor lamp near NE end, out of sofa.
  var bounds=sofa.GetComponentInChildren<Renderer>().bounds;
  foreach(var r in sofa.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(r.bounds);
  // Position on the LIVING/TV side of CAD wall 12, not behind the wall.
  // Keep base outside the sofa footprint on its southern corner.
  var lampPos=new Vector3(bounds.min.x-.20f,0f,bounds.min.z-.20f);
  var floor=new GameObject("LivingFloorLamp_PhysicalFixture").transform;
  floor.SetParent(root,false);floor.position=lampPos;
  Primitive(floor,"BrassFoot",PrimitiveType.Cylinder,lampPos+new Vector3(0,.025f,0),new Vector3(.135f,.025f,.135f),brass);
  Primitive(floor,"BrassStem",PrimitiveType.Cylinder,lampPos+new Vector3(0,.69f,0),new Vector3(.013f,.69f,.013f),brass);
  var shade=Primitive(floor,"LinenShade",PrimitiveType.Cylinder,lampPos+new Vector3(0,1.40f,0),new Vector3(.195f,.195f,.195f),linen);
  // The cylinder has a solid bottom: make its fabric itself emissive so it
  // reads as a lit shade. A small luminous diffuser protrudes below the rim.
  Primitive(floor,"WarmBulb_Emission",PrimitiveType.Sphere,lampPos+new Vector3(0,1.19f,0),new Vector3(.140f,.09f,.140f),bulb);
  glowing.Add(shade.GetComponent<Renderer>());
  var lightRoot=new GameObject("FloorLampPoint_InsideShade");lightRoot.transform.SetParent(floor,false);
  lightRoot.transform.position=lampPos+new Vector3(0,1.40f,0);
  var pl=lightRoot.AddComponent<Light>();pl.type=LightType.Point;pl.range=4f;pl.intensity=.45f;
  pl.color=new Color(1f,.83f,.66f);pl.shadows=LightShadows.None;
  var controller=camera.gameObject.GetComponent<HefuFixtureLighting>();
  if(!controller)controller=camera.gameObject.AddComponent<HefuFixtureLighting>();
  controller.pendant0=s0;controller.pendant1=s1;controller.standingLamp=pl;
  var sun=GameObject.Find("BakedLike_Sun_VisualPreview");
  controller.sun=sun?sun.GetComponent<Light>():null;
  controller.glowingDiffusers=glowing.ToArray();
  controller.SetFixtureVisuals(false);
  QualitySettings.pixelLightCount=4; // Sun (1) + three visible practical fixtures.
  if(root.GetComponentsInChildren<Collider>(true).Length!=0)
   throw new Exception("HEFU_VISIBLE_LAMP_COLLIDERS_LEAKED");
  Debug.Log("HEFU_PHYSICAL_LAMPS_PASS pendants=2 floorLamp=1 attachedSpots=2"+
    " lampPoint=1 oldInvisiblePoints=0 emissiveSurfaces=3"+
    " realtimeShadowCastersExtra=0 colliderCount=0");
 }
}
}
