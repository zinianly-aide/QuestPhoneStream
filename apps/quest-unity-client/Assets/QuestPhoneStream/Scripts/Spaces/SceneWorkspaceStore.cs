using System;
using System.IO;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// Device-local, private persistence only. Scene index is metadata, not actual
    /// CAD/scanning geometry. Atomic replace + previous file backup; no network IO.
    /// </summary>
    public sealed class SceneWorkspaceStore
    {
        public const string CatalogName = "catalog.json";
        public readonly string Folder;
        public SceneWorkspaceStore(string persistentRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentRoot))
                throw new ArgumentException("Persistent root required");
            Folder = Path.Combine(persistentRoot, "SceneWorkspaces");
            Directory.CreateDirectory(Folder);
        }

        public string CatalogPath => Path.Combine(Folder, CatalogName);
        public string ScenePath(string id)
        {
            if (!SceneWorkspaceValidation.SafeId(id)) throw new ArgumentException("Unsafe ID");
            return Path.Combine(Folder, id + ".qpsspace");
        }

        public SceneWorkspaceIndex LoadCatalog()
        {
            var index = LoadJson<SceneWorkspaceIndex>(CatalogPath);
            if (index == null)
            {
                if(File.Exists(CatalogPath) || File.Exists(CatalogPath+".bak"))
                    throw new InvalidDataException("Catalog unreadable; original files preserved");
                return new SceneWorkspaceIndex();
            }
            if (index.schemaVersion != 1 || index.scenes == null || index.scenes.Count > 200)
                throw new InvalidDataException("Unsupported workspace catalog");
            foreach (var scene in index.scenes) SceneWorkspaceValidation.RequireValid(scene);
            return index;
        }

        public void SaveCatalog(SceneWorkspaceIndex index)
        {
            if (index == null || index.schemaVersion != 1 || index.scenes == null ||
                index.scenes.Count > 200)throw new ArgumentException("Invalid catalog");
            foreach (var scene in index.scenes)SceneWorkspaceValidation.RequireValid(scene);
            SaveJson(CatalogPath, index);
        }

        public SceneWorkspaceRecord LoadScene(string id)
        {
            var scene = LoadJson<SceneWorkspaceRecord>(ScenePath(id));
            if (scene == null)return null;
            SceneWorkspaceValidation.RequireValid(scene);
            if (!string.Equals(scene.id,id,StringComparison.Ordinal))
                throw new InvalidDataException("Scene ID mismatched path");
            return scene;
        }

        public void SaveScene(SceneWorkspaceRecord scene)
        {
            SceneWorkspaceValidation.RequireValid(scene);
            SaveJson(ScenePath(scene.id), scene);
        }

        public static SceneWorkspaceRecord Create(string displayName,string sourceType = "blank",string assetKey=null)
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var scene=new SceneWorkspaceRecord{
                id=Guid.NewGuid().ToString("N"),displayName=displayName,
                sourceType=sourceType,assetKey=assetKey,
                createdUtcMs=now,modifiedUtcMs=now};
            SceneWorkspaceValidation.RequireValid(scene);
            return scene;
        }

        static T LoadJson<T>(string path) where T:class
        {
            foreach(var name in new[]{path,path+".bak"})
            {
                if (!File.Exists(name)) continue;
                try
                {
                    var json=File.ReadAllText(name);
                    if(json.Length > 16*1024*1024)throw new InvalidDataException("Scene file too large");
                    var result=JsonUtility.FromJson<T>(json);
                    if(result!=null)return result;
                }
                catch(Exception ex){Debug.LogWarning("[Spaces] Corrupt file, trying backup: "+ex.Message);}
            }
            return null;
        }

        static void SaveJson(string path,object value)
        {
            string temp=path+".tmp",backup=path+".bak";
            File.WriteAllText(temp,JsonUtility.ToJson(value,true));
            if(File.Exists(path))
            {
                File.Copy(path,backup,true);
                File.Delete(path);
            }
            File.Move(temp,path);
        }
    }
}
