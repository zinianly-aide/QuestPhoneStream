using System;
using UnityEngine;
namespace HefuVR.Editor {
/// <summary>NOAA-style low-cost solar position approximation. +X=East, +Z=North,
/// +Y=Up; azimuth clockwise from North. Fixed China Standard Time UTC+8,
/// geography Luoyang (34.6836 N, 112.4536 E). Not a weather forecast.</summary>
public static class HefuLuoyangSolar {
 public const double Latitude=34.6836, Longitude=112.4536, Timezone=8.0;
 public const int Year=2026, Month=10, Day=25;
 public const double AfternoonHour=14.5;
 public struct Solar {
  public double elevation,azimuth,declination;
  public Vector3 toSun;
 }
 static double Rad(double d){return d*Math.PI/180.0;}
 static double Deg(double r){return r*180.0/Math.PI;}
 public static Solar Calculate(DateTime localDate,double hour){
  if(hour<0||hour>=24)throw new ArgumentOutOfRangeException(nameof(hour));
  double gamma=2*Math.PI/365.0*(localDate.DayOfYear-1+(hour-12)/24.0);
  double eq=229.18*(.000075+.001868*Math.Cos(gamma)-.032077*Math.Sin(gamma)-
    .014615*Math.Cos(2*gamma)-.040849*Math.Sin(2*gamma));
  double dec=.006918-.399912*Math.Cos(gamma)+.070257*Math.Sin(gamma)-
    .006758*Math.Cos(2*gamma)+.000907*Math.Sin(2*gamma)-
    .002697*Math.Cos(3*gamma)+.00148*Math.Sin(3*gamma);
  double minutes=hour*60+eq+4*Longitude-60*Timezone;
  double ha=Rad(minutes/4.0-180.0);
  double lat=Rad(Latitude);
  double elevation=Math.Asin(Math.Sin(lat)*Math.Sin(dec)+Math.Cos(lat)*Math.Cos(dec)*Math.Cos(ha));
  double azimuth=Math.Atan2(Math.Sin(ha),Math.Cos(ha)*Math.Sin(lat)-Math.Tan(dec)*Math.Cos(lat))+Math.PI;
  azimuth=(Deg(azimuth)+360.0)%360.0;
  var el=(float)elevation;var az=(float)Rad(azimuth);
  var vector=new Vector3(Mathf.Sin(az)*Mathf.Cos(el),Mathf.Sin(el),Mathf.Cos(az)*Mathf.Cos(el)).normalized;
  return new Solar{elevation=Deg(elevation),azimuth=azimuth,declination=Deg(dec),toSun=vector};
 }
 public static Solar Default(){return Calculate(new DateTime(Year,Month,Day),AfternoonHour);}
}
}
