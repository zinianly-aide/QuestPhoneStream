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
            var mat = SceneWorkspaceBirthdayMaterial.Create(c);
            if (!mat) return null;
            // A restrained glossy finish on warm-gold details, softer matte fabric elsewhere.
            bool warmGold = c.r > .7f && c.g > .48f && c.g < .68f && c.b < .40f;
            if(mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic",warmGold ? .72f : 0f);
            if(mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness",warmGold ? .75f : .33f);
            return mat;
        }
        static GameObject Mesh(Transform root, PrimitiveType shape, string label,
            Vector3 local, Vector3 size, Color color)
        {
            var go=GameObject.CreatePrimitive(shape);
            go.name="BirthdayPiece_"+label;
            go.transform.SetParent(root,false);
            go.transform.localPosition=local;
            go.transform.localScale=size;
            var r=go.GetComponent<Renderer>();if(r)r.material=MakeMaterial(color);
            // Trigger colliders can be targeted by edit ray, never block locomotion.
            var col=go.GetComponent<Collider>();
            if(col) { if(shape==PrimitiveType.Quad) col.enabled=false; else col.isTrigger=true; }
            return go;
        }
        static readonly Color[] Palette = {
            new Color(.96f,.68f,.73f), // blush
            new Color(.83f,.62f,.30f), // champagne gold
            new Color(.68f,.58f,.82f), // lavender
            new Color(.53f,.72f,.57f)  // sage
        };
        public static int StyleCount => Palette.Length;
        public static Color Accent(int style) => Palette[Mathf.Clamp(style,0,Palette.Length-1)];

        /// <summary>Repaint accents without altering meshes or private photo surfaces.</summary>
        public static void ApplyPresentation(GameObject root, SceneObjectRecord record)
        {
            if (!root || record == null) return;
            var accent=Accent(record.birthdayStyle);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.Contains("LocalPhotoSurface")) continue;
                bool colored = renderer.name.Contains("CakeLayer") ||
                    renderer.name.Contains("CakePearl") || renderer.name.Contains("Flower") ||
                    renderer.name.Contains("Box") || renderer.name.Contains("Balloon") ||
                    renderer.name.Contains("Bow") || renderer.name.Contains("PhotoFrame");
                if (!colored) continue;
                var block=new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color",accent);
                renderer.SetPropertyBlock(block);
            }
            if (record.assetId == "birthday/frame")
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.Contains("LocalPhotoSurface"))
                    {
                        // On missing slot, show a neutral placeholder; no external photo requests.
                        if (!SceneWorkspaceBirthdayPhotoSlots.TryApply(renderer,record.birthdayPhotoSlot))
                            SceneWorkspaceBirthdayPhotoSlots.Reset(renderer);
                    }
            }
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
                    Mesh(go.transform,PrimitiveType.Cylinder,"CakeBoard",new Vector3(0,.016f,0),new Vector3(.43f,.016f,.43f),gold);
                    Mesh(go.transform,PrimitiveType.Cylinder,"CakeBase",new Vector3(0,.115f,0),new Vector3(.36f,.085f,.36f),ivory);
                    Mesh(go.transform,PrimitiveType.Cylinder,"CakeLayer",new Vector3(0,.21f,0),new Vector3(.29f,.035f,.29f),blush);
                    Mesh(go.transform,PrimitiveType.Cylinder,"CakeTop",new Vector3(0,.25f,0),new Vector3(.26f,.018f,.26f),ivory);
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI*.25f;
                        Mesh(go.transform,PrimitiveType.Sphere,"CakePearl",new Vector3(Mathf.Cos(a)*.14f,.235f,Mathf.Sin(a)*.14f),
                            Vector3.one*.045f,i%2==0?blush:gold);
                    }
                    Mesh(go.transform,PrimitiveType.Cylinder,"Candle",new Vector3(0,.325f,0),new Vector3(.013f,.055f,.013f),gold);
                    Mesh(go.transform,PrimitiveType.Sphere,"CandleGlow",new Vector3(0,.39f,0),Vector3.one*.033f,new Color(1f,.75f,.32f));
                    break;
                case "birthday/flowers":
                    Mesh(go.transform,PrimitiveType.Cylinder,"Vase",new Vector3(0,.08f,0),new Vector3(.12f,.08f,.12f),gold);
                    for(int i=0;i<5;i++)
                    {float a=i*1.2566f;Mesh(go.transform,PrimitiveType.Sphere,"Flower"+i,
                        new Vector3(Mathf.Cos(a)*.085f,.245f,Mathf.Sin(a)*.085f),
                        Vector3.one*.09f,i%2==0?blush:ivory);}
                    Mesh(go.transform,PrimitiveType.Cylinder,"Stem",new Vector3(0,.18f,0),new Vector3(.04f,.10f,.04f),green);
                    for(int i=0;i<4;i++)
                    {
                        float a=i*Mathf.PI*.5f;
                        var leaf=Mesh(go.transform,PrimitiveType.Sphere,"Leaf"+i,
                            new Vector3(Mathf.Cos(a)*.085f,.15f,Mathf.Sin(a)*.085f),
                            new Vector3(.12f,.028f,.07f),green);
                        leaf.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,25);
                    }
                    Mesh(go.transform,PrimitiveType.Cylinder,"VaseRim",new Vector3(0,.169f,0),
                        new Vector3(.135f,.012f,.135f),gold);
                    break;
                case "birthday/gift":
                    Mesh(go.transform,PrimitiveType.Cube,"Box",new Vector3(0,.105f,0),Vector3.one*.21f,ivory);
                    Mesh(go.transform,PrimitiveType.Cube,"RibbonX",new Vector3(0,.105f,0),new Vector3(.025f,.215f,.22f),gold);
                    Mesh(go.transform,PrimitiveType.Cube,"RibbonZ",new Vector3(0,.105f,0),new Vector3(.22f,.215f,.025f),gold);
                    for(int i=0;i<2;i++)
                    {
                        var bow=Mesh(go.transform,PrimitiveType.Sphere,"BowLoop"+i,
                            new Vector3((i==0?-1:1)*.055f,.23f,0),new Vector3(.10f,.043f,.066f),gold);
                        bow.transform.localRotation=Quaternion.Euler(0,0,(i==0?-1:1)*20f);
                    }
                    break;
                case "birthday/balloon":
                    Mesh(go.transform,PrimitiveType.Sphere,"Balloon",new Vector3(0,.30f,0),new Vector3(.24f,.28f,.24f),blush);
                    Mesh(go.transform,PrimitiveType.Sphere,"Knot",new Vector3(0,.156f,0),Vector3.one*.033f,gold);
                    Mesh(go.transform,PrimitiveType.Cylinder,"Stand",new Vector3(0,.078f,0),new Vector3(.008f,.075f,.008f),gold);
                    Mesh(go.transform,PrimitiveType.Cylinder,"StandBase",new Vector3(0,.007f,0),new Vector3(.10f,.008f,.10f),gold);
                    break;
                case "birthday/frame":
                    Mesh(go.transform,PrimitiveType.Cube,"PhotoFrame",new Vector3(0,.18f,0),new Vector3(.29f,.36f,.025f),gold);
                    var image=Mesh(go.transform,PrimitiveType.Quad,"LocalPhotoSurface",
                        new Vector3(0,.18f,-.015f),new Vector3(.24f,.31f,1f),ivory);
                    image.transform.localRotation=Quaternion.Euler(0,180f,0);
                    Mesh(go.transform,PrimitiveType.Cube,"FrameStand",new Vector3(0,.09f,.08f),
                        new Vector3(.03f,.17f,.10f),gold);
                    break;
            }
            var tag=go.AddComponent<SceneWorkspaceObject>();
            tag.persistentId=record.id;tag.editable=record.editable;
            ApplyPresentation(go,record);
            return go;
        }
    }
}
