using System;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>Whitelisted B2 decoration assets. They contain no user pictures or network content.</summary>
    public static class SceneWorkspaceBirthdayAssets
    {
        public const string Prefix = "birthday/";
        public static readonly string[] Items = {"cake", "flowers", "gift", "balloon", "frame"};
        public static readonly string[] Anchors = {
            "Move_Dining_Table", "DiningSideboard", "Move_Coffee_Table", "EntranceShoeCabinet"
        };
        public static bool IsSupported(string asset)
        {
            if (string.IsNullOrEmpty(asset) || !asset.StartsWith(Prefix, StringComparison.Ordinal))return false;
            foreach (var item in Items) if (asset == Prefix + item) return true;
            return false;
        }
        public static bool IsAnchor(string id)
        {
            foreach (var a in Anchors) if (id == "hefu-" + a) return true;
            return false;
        }
        static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            if (!shader) throw new InvalidOperationException("Birthday asset shader missing");
            return new Material(shader) { color = c };
        }
        static void Mesh(Transform root, PrimitiveType shape, string label,
            Vector3 local, Vector3 size, Color color)
        {
            var go=GameObject.CreatePrimitive(shape);
            go.name="BirthdayPiece_"+label;
            go.transform.SetParent(root,false);
            go.transform.localPosition=local;
            go.transform.localScale=size;
            var r=go.GetComponent<Renderer>();if(r)r.material=MakeMaterial(color);
            // Trigger colliders can be targeted by edit ray, never block locomotion.
            var col=go.GetComponent<Collider>();if(col)col.isTrigger=true;
        }
        public static float Height(string asset)
        {
            switch(asset)
            {
                case "birthday/cake":return .26f;
                case "birthday/flowers":return .29f;
                case "birthday/gift":return .21f;
                case "birthday/balloon":return .43f;
                case "birthday/frame":return .36f;
                default:throw new ArgumentException("Unknown birthday asset");
            }
        }
        public static GameObject Create(Transform anchor, SceneObjectRecord record)
        {
            if (!anchor || record == null || !IsSupported(record.assetId) || !IsAnchor(record.anchorId))
                throw new ArgumentException("Unknown birthday decoration or anchor");
            var go=new GameObject("Birthday_Editable_"+record.assetId.Substring(Prefix.Length)+"_"+record.id);
            go.transform.SetParent(anchor,false);
            go.transform.SetLocalPositionAndRotation(record.localPosition,record.localRotation);
            go.transform.localScale=record.localScale;
            var ivory=new Color(.96f,.90f,.79f);
            var gold=new Color(.80f,.59f,.28f);
            var blush=new Color(.97f,.67f,.72f);
            var green=new Color(.27f,.51f,.31f);
            switch(record.assetId)
            {
                case "birthday/cake":
                    Mesh(go.transform,PrimitiveType.Cylinder,"CakeBase",new Vector3(0,.11f,0),new Vector3(.34f,.11f,.34f),ivory);
                    Mesh(go.transform,PrimitiveType.Cylinder,"CakeLayer",new Vector3(0,.22f,0),new Vector3(.28f,.035f,.28f),blush);
                    break;
                case "birthday/flowers":
                    Mesh(go.transform,PrimitiveType.Cylinder,"Vase",new Vector3(0,.08f,0),new Vector3(.12f,.08f,.12f),gold);
                    for(int i=0;i<5;i++)
                    {float a=i*1.2566f;Mesh(go.transform,PrimitiveType.Sphere,"Flower"+i,
                        new Vector3(Mathf.Cos(a)*.085f,.245f,Mathf.Sin(a)*.085f),
                        Vector3.one*.09f,i%2==0?blush:ivory);}
                    Mesh(go.transform,PrimitiveType.Cylinder,"Stem",new Vector3(0,.18f,0),new Vector3(.04f,.10f,.04f),green);
                    break;
                case "birthday/gift":
                    Mesh(go.transform,PrimitiveType.Cube,"Box",new Vector3(0,.105f,0),Vector3.one*.21f,ivory);
                    Mesh(go.transform,PrimitiveType.Cube,"RibbonX",new Vector3(0,.105f,0),new Vector3(.025f,.215f,.22f),gold);
                    Mesh(go.transform,PrimitiveType.Cube,"RibbonZ",new Vector3(0,.105f,0),new Vector3(.22f,.215f,.025f),gold);
                    break;
                case "birthday/balloon":
                    Mesh(go.transform,PrimitiveType.Sphere,"Balloon",new Vector3(0,.30f,0),new Vector3(.24f,.28f,.24f),blush);
                    Mesh(go.transform,PrimitiveType.Cylinder,"Stand",new Vector3(0,.12f,0),new Vector3(.008f,.12f,.008f),gold);
                    break;
                case "birthday/frame":
                    Mesh(go.transform,PrimitiveType.Cube,"PhotoFrame",new Vector3(0,.18f,0),new Vector3(.29f,.36f,.025f),gold);
                    Mesh(go.transform,PrimitiveType.Cube,"NoPhotoPlaceholder",new Vector3(0,.18f,-.015f),new Vector3(.24f,.31f,.01f),ivory);
                    break;
            }
            var tag=go.AddComponent<SceneWorkspaceObject>();
            tag.persistentId=record.id;tag.editable=record.editable;
            return go;
        }
    }
}
