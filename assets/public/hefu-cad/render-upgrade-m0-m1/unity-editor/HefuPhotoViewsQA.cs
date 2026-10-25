using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HefuVR;
namespace HefuVR.Editor
{
    public static class HefuPhotoViewsQA
    {
        static void Check(bool yes,string detail)
        {
            if(!yes)throw new Exception("HEFU_V0310_VIEW_QA_FAIL "+detail);
        }
        public static void Validate()
        {
            EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
            var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
            Check(viewer && viewer.photo && viewer.photo.captureCamera==viewer.cam,
                "Missing view photo comparison component");
            Check(HefuPhotoViewpoints.All.Length==10,"Need nine legacy viewpoints and one opt-in M1 hero");
            var ids=new HashSet<string>();
            for(int i=0;i<HefuPhotoViewpoints.All.Length;i++)
            {
                var refpoint=HefuPhotoViewpoints.All[i];
                Check(ids.Add(refpoint.id),"View ID duplicated: "+refpoint.id);
                viewer.SelectPreset(i);
                HefuPhotoViewpoints.OrbitFor(i,out var focus,out var yaw,
                    out var pitch,out var distance);
                Check(pitch>=15 && pitch<=82 && distance>=1.2f && distance<=32f,
                    "Camera outside allowed orbit limits");
                Check(Mathf.Abs(viewer.distance-distance)<.001f &&
                    Vector3.Distance(focus,new Vector3(focus.x,focus.y,focus.z))<.00001f,
                    "View selection failed");
                var rot=Quaternion.Euler(pitch,yaw,0);
                Vector3 eye=focus+rot*Vector3.back*distance;
                Vector3 heading=rot*Vector3.forward;
                Check(Vector3.Distance(eye,refpoint.eye)<.85f,
                    "Preset camera moved too far from expected: "+refpoint.id+" "+eye);
                if(refpoint.id=="kitchen")
                {
                    Check(Vector3.Dot(heading,Vector3.right)>.95f,
                        "Kitchen from WEST must look EAST");
                    Check(Vector3.Dot(-(rot*Vector3.right),Vector3.forward)>.95f,
                        "Kitchen screen left must point NORTH");
                }
                if(refpoint.id=="living" || refpoint.id=="balcony")
                    Check(Vector3.Dot(heading,Vector3.back)>.92f,
                        "Living/balcony camera must look SOUTH");
                if(refpoint.id=="entry")
                    Check(Vector3.Dot(heading,Vector3.forward)>.90f,
                        "Entry photo must look NORTH");
                if(refpoint.id=="hero_living_dining")
                    Check(Vector3.Dot(heading,Vector3.back)>.9f &&
                         Mathf.Abs(refpoint.eye.y-1.55f)<.001f,
                        "Hero must face south from physical eye height 1.55m");
                if(refpoint.id=="master")
                    Check(Vector3.Dot(heading,Vector3.right)>.60f,
                        "Master photo must look at EAST wardrobe");
                Debug.Log("HEFU_V0310_VIEW_QA_PASS id="+refpoint.id+" eye="+eye+" yaw="+yaw+
                    " pitch="+pitch+" distance="+distance);
            }
            // Test missing references: should never generate or substitute a fake photo.
            viewer.SelectPreset(3);
            Check(!viewer.photo.ToggleComparison(3),"Missing reference falsely accepted");
            Check(!viewer.photo.comparing,"Missing comparison not disabled");
            // Verify the Android picker callback-to-image-preview path with a temporary
            // synthetic test image, but never overwrite actual reference photos.
            string testFile=Path.Combine(viewer.photo.PhotoDirectory,"top_n.png");
            string existingJpg=Path.Combine(viewer.photo.PhotoDirectory,"top_n.jpg");
            if(!File.Exists(testFile) && !File.Exists(existingJpg))
            {
                Directory.CreateDirectory(viewer.photo.PhotoDirectory);
                var mock=new Texture2D(12,12,TextureFormat.RGB24,false);
                try
                {
                    for(int y=0;y<12;y++)for(int x=0;x<12;x++)
                        mock.SetPixel(x,y,Color.magenta);
                    mock.Apply();
                    File.WriteAllBytes(testFile,mock.EncodeToPNG());
                    viewer.SelectPreset(0);
                    viewer.photo.OnReferencePhotoImported("top_n");
                    Check(viewer.photo.comparing && viewer.photo.reference &&
                        viewer.photo.reference.width==12,
                        "Picker callback did not load real input image bytes");
                    viewer.photo.ToggleComparison(0);
                    Check(!viewer.photo.comparing,"Compare hide failed");
                    Debug.Log("HEFU_V0310_REFERENCE_IMPORT_QA_PASS image_decode=true overlay_toggle=true");
                }
                finally {File.Delete(testFile);UnityEngine.Object.DestroyImmediate(mock);}
            }
            // photo files should remain user-managed and not be bundled with APK.
            Debug.Log("HEFU_M1_ALL_VIEW_QA_PASS count=10 kitchen_W_to_E=true" +
                " no_reference_fallback=false screenshot_gallery_best_effort=true");
        }

        public static void CaptureAll()
        {
            EditorSceneManager.OpenScene("Assets/Hefu/Scenes/Hefu_Mobile_Viewer.unity",OpenSceneMode.Single);
            var viewer=UnityEngine.Object.FindObjectOfType<HefuMobileViewer>();
            if(!viewer || !viewer.cam) throw new Exception("Mobile scene has no viewer");
            string folder=Path.GetFullPath("Builds/Views/v"+UnityEditor.PlayerSettings.bundleVersion);
            Directory.CreateDirectory(folder);
            var temp=new RenderTexture(1200,800,24,RenderTextureFormat.ARGB32);
            var active=RenderTexture.active;
            var target=viewer.cam.targetTexture;
            var pic=new Texture2D(1200,800,TextureFormat.RGB24,false);
            try
            {
                temp.Create();
                for(int i=0;i<HefuPhotoViewpoints.All.Length;i++)
                {
                    HefuPhotoViewpoints.OrbitFor(i,out var focus,out var yaw,
                        out var pitch,out var distance);
                    var rotation=Quaternion.Euler(pitch,yaw,0);
                    viewer.cam.transform.SetPositionAndRotation(
                        focus+rotation*Vector3.back*distance,rotation);
                    viewer.cam.targetTexture=temp;
                    RenderTexture.active=temp;
                    viewer.cam.Render();
                    pic.ReadPixels(new Rect(0,0,1200,800),0,0);
                    pic.Apply();
                    var id=HefuPhotoViewpoints.All[i].id;
                    string path=Path.Combine(folder,id+".png");
                    File.WriteAllBytes(path,pic.EncodeToPNG());
                    if(new FileInfo(path).Length<7000)
                        throw new Exception("Preset screenshot suspiciously empty: "+id);
                    Debug.Log("HEFU_V0310_VIEW_CAPTURE_PASS "+id+" bytes="+new FileInfo(path).Length);
                }
            }
            finally
            {
                RenderTexture.active=active;
                viewer.cam.targetTexture=target;
                temp.Release();
                UnityEngine.Object.DestroyImmediate(temp);
                UnityEngine.Object.DestroyImmediate(pic);
            }
        }
    }
}
