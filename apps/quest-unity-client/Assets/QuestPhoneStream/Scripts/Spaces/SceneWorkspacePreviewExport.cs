using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    // Safe low-poly metadata interchange for phone previews. Deliberately never exports
    // raw camera data, passthrough images, private photographs or proprietary FBX meshes.
    public static class SceneWorkspacePreviewExport
    {
        [Serializable] public sealed class Node
        {
            public string id;
            public string kind;
            public Vector3 position;
            public Vector3 size;
            // Unity world-space AABB exports use identity; MRUK-based surfaces may
            // use explicit orientation when supported by their geometry provider.
            public Quaternion rotation=Quaternion.identity;
        }
        [Serializable] public sealed class Document
        {
            public string format="qps-room-preview-v1";
            public int schemaVersion=1;
            public string sceneId;
            public string name;
            public string source;
            public float metersPerUnit=1f;
            public List<Node> objects=new List<Node>();
        }
        static string Kind(string name)
        {
            var n=name.ToLowerInvariant();
            if(n.Contains("floor")||n.Contains("地面"))return "floor";
            if(n.Contains("ceiling")||n.Contains("顶面"))return "ceiling";
            if(n.Contains("wall")||n.Contains("墙"))return "wall";
            if(n.Contains("cabinet")||n.Contains("bed")||n.Contains("sofa")||
                n.Contains("table")||n.Contains("chair")||n.Contains("wardrobe"))return "furniture";
            return "volume";
        }
        static int Priority(string kind)
        {
            return kind=="floor"?0:kind=="wall"?1:kind=="ceiling"?2:
                kind=="furniture"?3:4;
        }
        public static Document Capture(Transform world, string name, string source)
        {
            if(!world)throw new ArgumentNullException(nameof(world));
            var data=new Document{sceneId=Guid.NewGuid().ToString("N"),name=name,
                source=source};
            var renderers=new List<Renderer>(world.GetComponentsInChildren<Renderer>(false));
            // If huge CAD contains more than the 512-node preview budget, structural
            // floor/walls/ceiling are retained before cosmetic furniture and details.
            renderers.Sort((a,b)=>Priority(Kind(a.name)).CompareTo(Priority(Kind(b.name))));
            foreach(var r in renderers)
            {
                if(data.objects.Count>=512)break;
                // Only static opaque mesh volumes. Excludes text, XR overlays,
                // photos, particles and skinned hands without extra Unity modules.
                if(!r.enabled || !(r is MeshRenderer))continue;
                bool privateDecoration=false;
                for(var p=r.transform;p && p!=world;p=p.parent)
                    if(p.name.StartsWith("Birthday_",StringComparison.Ordinal) ||
                       p.name.StartsWith("PrivatePhoto",StringComparison.Ordinal))
                       {privateDecoration=true;break;}
                if(privateDecoration)continue;
                var b=r.bounds;
                if(b.size.sqrMagnitude<.00001f || b.center.sqrMagnitude>10000f)continue;
                // World-space AABBs are approximate proxy meshes, NOT a high fidelity scan.
                data.objects.Add(new Node {
                    id="node_"+data.objects.Count.ToString("D4"),kind=Kind(r.name),
                    position=b.center,size=Vector3.Max(b.size,Vector3.one*.01f),
                    rotation=Quaternion.identity
                });
            }
            if(data.objects.Count==0)throw new InvalidOperationException("No visible preview geometry");
            return data;
        }
        public static string SaveToPrivateFiles(SceneWorkspaceManager mgr)
        {
            if(mgr==null || mgr.Active==null || mgr.EnvironmentRoot==null)
                throw new InvalidOperationException("No selected scene");
            var doc=Capture(mgr.EnvironmentRoot,mgr.Active.displayName,
                "quest-workspace-proxy");
            string dir=Path.Combine(mgr.Store.Folder,"Exports");
            Directory.CreateDirectory(dir);
            string path=Path.Combine(dir,doc.sceneId+".json");
            File.WriteAllText(path,JsonUtility.ToJson(doc,true));
            Debug.Log("[ScenePreviewExport] READY id="+doc.sceneId+" nodes="+doc.objects.Count+
                " path="+path+" (private local; transfer requires explicit action)");
            return path;
        }
    }
}
