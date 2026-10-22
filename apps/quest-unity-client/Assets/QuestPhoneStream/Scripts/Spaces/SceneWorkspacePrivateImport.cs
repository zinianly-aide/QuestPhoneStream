using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace QuestPhoneStream.Spaces
{
    [Serializable]
    internal sealed class PrivateFurnitureManifestItem
    {
        public string id;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
    [Serializable]
    internal sealed class PrivateFurnitureManifest
    {
        public int schemaVersion;
        public float metersPerUnit;
        public float averageCeilingHeight;
        public List<PrivateFurnitureManifestItem> items;
    }
    internal static class SceneWorkspacePrivateImport
    {
        public static bool TrySeedFurniture(SceneWorkspaceStore store,SceneWorkspaceRecord scene,out string error)
        {
            error=null;
            if(scene.sourceType!="cad-bundle" || scene.objects.Count!=0)return true;
            if(scene.assetKey!="hefu/home.bundle")
            {
                error="No supported private CAD manifest adapter";return false;
            }
            string manifestPath=Path.Combine(store.Folder,"Assets/hefu/home-manifest.json");
            if(!File.Exists(manifestPath))
            {
                error="Private furniture manifest not installed";return false;
            }
            try
            {
                if(new FileInfo(manifestPath).Length>1024*1024)
                    throw new InvalidDataException("Furniture manifest too large");
                var manifest=JsonUtility.FromJson<PrivateFurnitureManifest>(File.ReadAllText(manifestPath));
                if(manifest==null || manifest.schemaVersion!=1 ||
                   Mathf.Abs(manifest.metersPerUnit-1f)>.001f ||
                   Mathf.Abs(manifest.averageCeilingHeight-2.7f)>.01f ||
                   manifest.items==null || manifest.items.Count!=16)
                    throw new InvalidDataException("Hefu model manifest/scale mismatch");
                var added=new List<SceneObjectRecord>();
                var unique=new HashSet<string>(StringComparer.Ordinal);
                foreach(var item in manifest.items)
                {
                    if(item==null || !SceneWorkspaceValidation.SafeId(item.id) ||
                       !unique.Add(item.id))throw new InvalidDataException("Invalid furniture name");
                    added.Add(new SceneObjectRecord{
                        id="hefu-"+item.id,
                        assetId="cad/"+item.id,
                        label=item.id,kind="furniture",editable=true,
                        localPosition=item.position,localRotation=item.rotation,localScale=item.scale});
                }
                scene.objects=added;
                SceneWorkspaceValidation.RequireValid(scene);
                return true;
            }
            catch(Exception ex)
            {
                scene.objects.Clear();error="CAD furniture import rejected: "+ex.Message;
                return false;
            }
        }
        public static GameObject FindAndBind(Transform staged,SceneObjectRecord record)
        {
            if(!record.assetId.StartsWith("cad/",StringComparison.Ordinal))return null;
            string originalName=record.assetId.Substring(4);
            // No arbitrary hierarchy paths. Single explicit model object by known name.
            Transform found=null;
            foreach(var t in staged.GetComponentsInChildren<Transform>(true))
            {
                if(t.name!=originalName)continue;
                if(found!=null)throw new InvalidDataException("Ambiguous CAD object name: "+originalName);
                found=t;
            }
            if(!found)throw new InvalidDataException("Missing CAD furniture object: "+originalName);
            if(record.deleted){found.gameObject.SetActive(false);return null;}
            found.SetLocalPositionAndRotation(record.localPosition,record.localRotation);
            found.localScale=record.localScale;
            var tag=found.GetComponent<SceneWorkspaceObject>() ?? found.gameObject.AddComponent<SceneWorkspaceObject>();
            tag.persistentId=record.id;tag.editable=record.editable;
            return found.gameObject;
        }
    }
}
