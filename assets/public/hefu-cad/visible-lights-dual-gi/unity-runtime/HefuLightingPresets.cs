using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace HefuVR {
[CreateAssetMenu(menuName="Hefu/Day Birthday Lightmap Presets")]
public sealed class HefuLightingPresets:ScriptableObject {
 public Texture2D dayColor,dayDir,dayMask;
 public Texture2D birthdayColor,birthdayDir,birthdayMask;
 public float[] dayProbeSH,birthdayProbeSH;
 public int probesPerPreset;
 public bool Ready => dayColor&&birthdayColor&&dayProbeSH!=null&&birthdayProbeSH!=null&&
  probesPerPreset>0&&dayProbeSH.Length==probesPerPreset*27&&birthdayProbeSH.Length==probesPerPreset*27;
 public LightmapData[] Lightmaps(bool birthday){
  var d=new LightmapData();d.lightmapColor=birthday?birthdayColor:dayColor;
  d.lightmapDir=birthday?birthdayDir:dayDir;
  d.shadowMask=birthday?birthdayMask:dayMask;
  return new[]{d};
 }
 public static float[] Flatten(SphericalHarmonicsL2[] sh){
  var f=new float[sh.Length*27];int k=0;
  foreach(var x in sh)for(int c=0;c<3;c++)for(int i=0;i<9;i++)f[k++]=x[c,i];
  return f;
 }
 public SphericalHarmonicsL2[] Unflatten(bool birthday){
  var f=birthday?birthdayProbeSH:dayProbeSH;
  if(f==null||f.Length!=probesPerPreset*27)throw new InvalidOperationException("Hefu dual lighting probe payload missing");
  var v=new SphericalHarmonicsL2[probesPerPreset];int k=0;
  for(int p=0;p<v.Length;p++)for(int c=0;c<3;c++)for(int i=0;i<9;i++)v[p][c,i]=f[k++];
  return v;
 }
}
}
