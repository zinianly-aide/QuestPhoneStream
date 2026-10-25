using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace HefuVR
{
    /// <summary>User-provided reference photos + scene-only PNG export. No fake photos.</summary>
    public sealed class HefuPhotoComparison : MonoBehaviour
    {
        public Camera captureCamera;
        public bool comparing { get; private set; }
        public Texture2D reference { get; private set; }
        public int selectedIndex=-1;
        public string status="Views ready";
        bool busy;
        public string PhotoDirectory{get{return Path.Combine(Application.persistentDataPath,"ReferencePhotos");}}
        public string ScreenshotDirectory{get{return Path.Combine(Application.persistentDataPath,"Comparisons");}}
        void OnDestroy(){Clear();}
        void Clear(){comparing=false;if(reference)
            {
                if(Application.isPlaying)Destroy(reference);else DestroyImmediate(reference);
                reference=null;
            }}
        public void OnPresetChanged(int index)
        {
            Clear();selectedIndex=index;status=HefuPhotoViewpoints.All[index].title;
        }
        public void ImportReference(int index)
        {
            selectedIndex=index;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var intent=new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setClassName",activity,
                        "com.hefuvr.PhotoImportActivity");
                    intent.Call<AndroidJavaObject>("putExtra","referenceId",
                        HefuPhotoViewpoints.All[index].id);
                    activity.Call("startActivity",intent);
                }
                status="Choose a real room photo";
            }
            catch(Exception e)
            {
                status="Photo picker failed";
                Debug.LogWarning("HEFU_PHOTO_PICKER "+e);
            }
#else
            status="Picker works on Android phone only";
#endif
        }
        // Called by the private Android system picker after a photo is copied.
        public void OnReferencePhotoImported(string key)
        {
            for(int i=0;i<HefuPhotoViewpoints.All.Length;i++)
            {
                if(HefuPhotoViewpoints.All[i].id != key)continue;
                if(comparing)Clear();
                if(ToggleComparison(i))status="Photo imported: "+key;
                return;
            }
            status="Unknown reference photo ID";
        }
        public void OnReferencePhotoImportFailed(string error)
        {
            status="Photo import failed: "+error;
        }

        public bool ToggleComparison(int index)
        {
            if(comparing){Clear();status="Compare hidden";return false;}
            Clear();selectedIndex=index;
            string key=HefuPhotoViewpoints.All[index].id;
            string jpg=Path.Combine(PhotoDirectory,key+".jpg");
            string png=Path.Combine(PhotoDirectory,key+".png");
            string path=File.Exists(jpg)?jpg:File.Exists(png)?png:null;
            if(path==null){status="No ReferencePhotos/"+key+".jpg";return false;}
            try
            {
                byte[] data=File.ReadAllBytes(path);
                if(data.Length==0||data.Length>24*1024*1024)throw new InvalidDataException("Invalid size");
                var tex=new Texture2D(2,2,TextureFormat.RGB24,false);
                if(!tex.LoadImage(data)){Destroy(tex);throw new InvalidDataException("Image decode failed");}
                reference=tex;comparing=true;status="Real reference (not calibrated)";
                return true;
            }
            catch(Exception ex){status="Reference error: "+ex.GetType().Name;return false;}
        }
        public void Capture(int index)
        {
            if(busy||!captureCamera){status="Capture unavailable";return;}
            StartCoroutine(CaptureRoutine(index));
        }
        IEnumerator CaptureRoutine(int index)
        {
            busy=true;
            yield return new WaitForEndOfFrame();
            RenderTexture rt=null;Texture2D tex=null;
            RenderTexture prior=RenderTexture.active;
            RenderTexture old=captureCamera.targetTexture;
            try
            {
                rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);rt.Create();
                captureCamera.targetTexture=rt;RenderTexture.active=rt;captureCamera.Render();
                tex=new Texture2D(1440,900,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();
                Directory.CreateDirectory(ScreenshotDirectory);
                string file="HefuVR_"+HefuPhotoViewpoints.All[index].id+"_"+
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+".png";
                byte[] bytes=tex.EncodeToPNG();
                string path=Path.Combine(ScreenshotDirectory,file);
                File.WriteAllBytes(path,bytes);
                bool gallery=TrySaveGallery(file,bytes);
                status=gallery?"Saved: Gallery / HefuVR":"Saved: Comparisons/"+file;
                Debug.Log("HEFU_V0310_CAPTURE "+path+" gallery="+gallery+" bytes="+bytes.Length);
            }
            catch(Exception ex){status="Screenshot failed: "+ex.GetType().Name;Debug.LogWarning(ex);}
            finally
            {
                captureCamera.targetTexture=old;RenderTexture.active=prior;
                if(rt){rt.Release();Destroy(rt);}if(tex)Destroy(tex);busy=false;
            }
        }
        internal static bool TrySaveGallery(string file,byte[] png)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var version=new AndroidJavaClass("android.os.Build$VERSION"))
                    if(version.GetStatic<int>("SDK_INT")<29)return false;
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var resolver=activity.Call<AndroidJavaObject>("getContentResolver"))
                using(var media=new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
                using(var values=new AndroidJavaObject("android.content.ContentValues"))
                {
                    values.Call("put","_display_name",file);
                    values.Call("put","mime_type","image/png");
                    values.Call("put","relative_path","Pictures/HefuVR");
                    using(var uri=resolver.Call<AndroidJavaObject>("insert",
                        media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"),values))
                    {
                        if(uri==null)return false;
                        using(var stream=resolver.Call<AndroidJavaObject>("openOutputStream",uri))
                        {
                            if(stream==null)return false;
                            stream.Call("write",png);stream.Call("flush");stream.Call("close");
                        }
                    }
                }
                return true;
            }
            catch(Exception e){Debug.LogWarning("HEFU_GALLERY_EXPORT_WARN "+e.Message);}
#endif
            return false;
        }
        public void DrawOverlay(int index,Rect area)
        {
            if(!comparing||!reference||index!=selectedIndex)return;
            GUI.Box(area,GUIContent.none);
            GUI.Label(new Rect(area.x+8,area.y+3,area.width-16,32),
                "REAL PHOTO | "+HefuPhotoViewpoints.All[index].title+" (not calibrated)");
            GUI.DrawTexture(new Rect(area.x+6,area.y+37,area.width-12,area.height-44),
                reference,ScaleMode.ScaleToFit);
        }
    }
}
