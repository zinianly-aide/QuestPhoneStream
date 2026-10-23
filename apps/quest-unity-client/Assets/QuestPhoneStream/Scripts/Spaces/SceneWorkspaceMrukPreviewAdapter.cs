using System;
using System.IO;
using UnityEngine;

// This adapter is disabled until a separately verified Meta XR Core + MRUK SDK
// is installed and QPS_HAS_MRUK is defined. The default Quest app does not
// claim to have scanned a room.
#if QPS_HAS_MRUK
using Meta.XR.MRUtilityKit;
#endif
namespace QuestPhoneStream.Spaces
{
    public static class SceneWorkspaceMrukPreviewAdapter
    {
#if QPS_HAS_MRUK
        // User gesture only. The Meta OS owns Space Setup and the spatial-data consent.
        // Requesting permission is not equivalent to capturing a complete 3D room.
        public static bool TryExportCurrentPhysicalRoom(SceneWorkspaceManager manager, out string status)
        {
            const string permission="com.oculus.permission.USE_SCENE";
            status="No physical scene available";
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                UnityEngine.Android.Permission.RequestUserPermission(permission);
                status="Grant Spatial Data, complete Quest Space Setup, then retry";
                return false;
            }
#endif
            if(MRUK.Instance==null || MRUK.Instance.GetCurrentRoom()==null)
            {
                status="Run Quest Settings > Environment Setup > Space Setup first";
                return false;
            }
            var room=MRUK.Instance.GetCurrentRoom();
            var doc=new SceneWorkspacePreviewExport.Document{
                sceneId=Guid.NewGuid().ToString("N"),name="Quest physical room",
                source="quest-scene-api"
            };
            foreach(var anchor in room.Anchors)
            {
                if(doc.objects.Count>=512)break;
                string semantic=anchor.Label.ToString().ToLowerInvariant();
                string kind=semantic.Contains("floor")?"floor":
                  semantic.Contains("ceiling")?"ceiling":
                  semantic.Contains("wall")?"wall":"volume";
                Vector3 center,size;
                if(anchor.PlaneRect.HasValue)
                {
                    var rect=anchor.PlaneRect.Value;
                    center=anchor.transform.TransformPoint(new Vector3(rect.center.x,rect.center.y,0));
                    size=new Vector3(Mathf.Max(.01f,rect.width),Mathf.Max(.01f,rect.height),.025f);
                }
                else if(anchor.VolumeBounds.HasValue)
                {
                    var volume=anchor.VolumeBounds.Value;
                    center=anchor.transform.TransformPoint(volume.center);
                    size=Vector3.Max(volume.size,Vector3.one*.01f);
                    kind="furniture";
                }
                else continue;
                // Does not export Meta anchor handles, passthrough frames or raw mesh.
                doc.objects.Add(new SceneWorkspacePreviewExport.Node{
                    id="anchor_"+doc.objects.Count.ToString("D4"),kind=kind,
                    position=center,size=size,rotation=anchor.transform.rotation
                });
            }
            if(doc.objects.Count==0)
            {status="No eligible room planes/volumes; finish Space Setup";return false;}
            var dir=Path.Combine(manager.Store.Folder,"Exports");Directory.CreateDirectory(dir);
            var file=Path.Combine(dir,doc.sceneId+".json");
            File.WriteAllText(file,JsonUtility.ToJson(doc,true));
            status="Physical room proxy saved privately, "+doc.objects.Count+" surfaces";
            return true;
        }
#endif
    }
}
