using System;
using UnityEngine;
namespace HefuVR
{
    // CAD axis: +X east / +Z north. Anchors use camera-height and actual room regions.
    // Camera positions are design approximations, not photo-calibrated measurements.
    public static class HefuPhotoViewpoints
    {
        [Serializable] public struct View
        {
            public string id,title;
            public Vector3 eye,look;
            public View(string id,string title,Vector3 eye,Vector3 look)
            {this.id=id;this.title=title;this.eye=eye;this.look=look;}
        }
        public static readonly View[] All={
            new View("top_n","01 Top North",new Vector3(-6.30f,15.8f,-7.20f),new Vector3(-6.30f,0,-4.7f)),
            new View("entry","02 Entry -> North",new Vector3(-5.90f,1.70f,-3.60f),new Vector3(-5.60f,.85f,-.55f)),
            new View("living","03 Living -> South",new Vector3(-5.55f,1.72f,-3.85f),new Vector3(-5.30f,.65f,-7.80f)),
            new View("kitchen","04 Kitchen W -> E",new Vector3(-3.18f,1.62f,-1.25f),new Vector3(-.75f,.90f,-1.02f)),
            new View("master","05 Master Head East",new Vector3(-4.38f,1.71f,-8.61f),new Vector3(-2.48f,1.05f,-6.51f)),
            new View("balcony","06 Balcony South",new Vector3(-6.30f,1.70f,-7.05f),new Vector3(-6.30f,.82f,-9.25f)),
            new View("corridor","07 Bath Corridor",new Vector3(-7.70f,1.68f,-4.27f),new Vector3(-8.90f,1.30f,-3.90f)),
            new View("study","08 Study Sliding South",new Vector3(-9.60f,1.65f,-6.55f),new Vector3(-9.60f,1.20f,-8.18f)),
            new View("vanity","09 Vanity Look North",new Vector3(-8.69f,1.57f,-4.67f),new Vector3(-8.69f,1.20f,-3.41f)),
            // M1 opt-in hero; APPENDED to preserve all existing preset indices,
            // photo comparison IDs and persisted selected views.
            new View("hero_living_dining","10 Hero Living + Dining",new Vector3(-6.46f,1.55f,-3.96f),new Vector3(-5.44f,.60f,-7.46f))
        };
        public static void OrbitFor(int index,out Vector3 focus,out float yaw,out float pitch,out float distance)
        {
            if(index<0 || index>=All.Length)throw new ArgumentOutOfRangeException("index");
            var view=All[index];
            Vector3 forward=view.look-view.eye;
            // Quaternion.eulerAngles folds around 180 degrees for west-facing views.
            // Derive yaw/pitch directly from world-forward for all compass directions.
            yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            pitch=Mathf.Clamp(Mathf.Atan2(-forward.y,
                new Vector2(forward.x,forward.z).magnitude)*Mathf.Rad2Deg,15f,82f);
            distance=Mathf.Clamp(forward.magnitude,1.2f,32f);
            focus=view.look;
            if(index==0){yaw=0;pitch=81;distance=16;focus=new Vector3(-6.30f,0,-4.70f);}
        }
    }
}
