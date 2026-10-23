using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    // Decorative-only content driven by loaded CAD furniture bounds. No personal assets in Git.
    public sealed class SceneWorkspaceBirthday : MonoBehaviour
    {
        public const string Preset = "birthday-v1";
        static readonly string[] Anchors = {
            "EntranceShoeCabinet", "DiningSideboard", "Move_Dining_Table",
            "Move_Sofa", "Move_Coffee_Table"
        };
        public int AnchorCount { get; private set; }
        public int DecorationCount { get; private set; }
        public bool LightsOn { get; private set; } = true;
        readonly List<Material> _owned = new List<Material>();
        readonly List<Mesh> _ownedMeshes = new List<Mesh>();
        readonly List<Renderer> _bulbs = new List<Renderer>();
        readonly List<Light> _lights = new List<Light>();
        Material _ivory, _gold, _rose, _white, _leaves, _glow, _photoMat;

        static Transform FindFurniture(Transform root, string id)
        {
            foreach (var item in root.GetComponentsInChildren<SceneWorkspaceObject>(true))
                if (item.persistentId == "hefu-" + id && item.gameObject.activeInHierarchy)
                    return item.transform;
            return null;
        }
        public static bool HasRequiredFurniture(Transform root)
        {
            if (!root) return false;
            foreach (var anchor in Anchors)
                if (!FindFurniture(root, anchor)) return false;
            return true;
        }
        public static SceneWorkspaceBirthday Build(Transform root, SceneWorkspaceRecord scene)
        {
            if (!root || scene == null || scene.decorationPreset != Preset ||
                scene.sourceType != "cad-bundle" || scene.assetKey != "hefu/home.bundle")
                throw new InvalidOperationException("Birthday mode requires HefuVR CAD copy");
            if (!HasRequiredFurniture(root))
                throw new InvalidOperationException("Birthday furniture anchors not found");
            var go = new GameObject("BirthdayDecor_BasedOnRealHefuLayout");
            go.transform.SetParent(root, false);
            var instance = go.AddComponent<SceneWorkspaceBirthday>();
            instance.Create(root);
            Debug.Log("[Spaces] BIRTHDAY_READY anchors=" + instance.AnchorCount +
                " decor=" + instance.DecorationCount + " north entry, south lounge");
            return instance;
        }
        Material MakeMaterial(string name, Color color, bool metallic = false)
        {
            var mat = SceneWorkspaceBirthdayMaterial.Create(color);
            if (!mat) return null; // Keep the room even if this material cannot render.
            mat.name = "Birthday_" + name;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic ? .75f : 0);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", metallic ? .78f : .32f);
            if (name == "LED" && mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(.42f,.24f,.07f));
            _owned.Add(mat);
            return mat;
        }
        static Bounds BoundsOf(Transform parent)
        {
            var result = new Bounds(parent.position, Vector3.one * .06f);
            bool first = true;
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                bool attachedDecor = false;
                for(var node=renderer.transform; node && node!=parent; node=node.parent)
                    if(node.name.StartsWith("Birthday_", StringComparison.Ordinal))
                    { attachedDecor=true; break; }
                if(attachedDecor) continue;
                if (first) { result = renderer.bounds; first = false; }
                else result.Encapsulate(renderer.bounds);
            }
            if (first) throw new InvalidOperationException("CAD furniture mesh missing: " + parent.name);
            return result;
        }
        Transform MakeAnchor(Transform root, string furniture, string purpose, Vector3 worldPosition)
        {
            var item = FindFurniture(root, furniture);
            var go = new GameObject("Birthday_" + purpose);
            go.transform.SetParent(item, false);
            go.transform.position = worldPosition;
            go.transform.rotation = Quaternion.identity;
            AnchorCount++;
            return go.transform;
        }
        GameObject Shape(Transform parent, PrimitiveType type, string name, Vector3 pos,
            Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider) collider.enabled = false; // No birthday decoration blocks locomotion.
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            DecorationCount++;
            return go;
        }
        GameObject FineMesh(Transform parent, string name, Vector3 localPosition,
            Mesh mesh, params Material[] materials)
        {
            if (!mesh) throw new InvalidOperationException("Birthday detail mesh missing: " + name);
            _ownedMeshes.Add(mesh);
            var go = new GameObject("Birthday_Detail_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            DecorationCount++;
            return go;
        }
        void Box(Transform p, string n, Vector3 pos, Vector3 size, Material m) =>
            Shape(p, PrimitiveType.Cube, n, pos, size, m);
        GameObject Ball(Transform p, string n, Vector3 pos, float size, Material m) =>
            Shape(p, PrimitiveType.Sphere, n, pos, Vector3.one * size, m);
        void Cylinder(Transform p, string n, Vector3 pos, Vector3 size, Material m) =>
            Shape(p, PrimitiveType.Cylinder, n, pos, size, m);
        GameObject Words(Transform p, string value, Vector3 pos, float size, float rotationY)
        {
            var go = new GameObject("BirthdayText_" + value);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, rotationY, 0);
            var txt = go.AddComponent<TextMesh>();
            txt.text = value;
            txt.characterSize = size;
            txt.fontSize = 52;
            txt.anchor = TextAnchor.MiddleCenter;
            txt.alignment = TextAlignment.Center;
            txt.color = _gold.color;
            DecorationCount++;
            return go;
        }
        void Bouquet(Transform p, Vector3 pos, float height)
        {
            // One procedural cluster contains the folded rose petals, stems and
            // leaves. 3 material slots replace ~19 sphere/cube renderers per vase.
            Cylinder(p, "FlowerVase", pos + Vector3.up * height * .23f,
                new Vector3(height*.22f, height*.23f, height*.22f), _gold);
            Cylinder(p, "VaseLip", pos + Vector3.up * height * .455f,
                new Vector3(height*.235f,height*.018f,height*.235f),_ivory);
            FineMesh(p,"RoseBouquet_FoldedPetals",pos,BirthdayFineMesh.Bouquet(height),
                _rose,_ivory,_leaves);
        }
        void Gift(Transform p, Vector3 pos, float size, bool pink)
        {
            Box(p,"GiftBox",pos+Vector3.up*size*.5f,Vector3.one*size,pink?_rose:_ivory);
            Box(p,"GoldRibbon",pos+Vector3.up*size*.5f,new Vector3(.025f,size*1.04f,size*1.02f),_gold);
            Box(p,"CrossRibbon",pos+Vector3.up*size*.5f,new Vector3(size*1.02f,size*1.04f,.025f),_gold);
            for (int i=0; i<2; i++)
            {
                var bow=Ball(p,"BowLoop",pos+new Vector3((i==0?-1:1)*size*.18f,size*1.055f,0),
                    size*.27f,_gold);
                bow.transform.localScale=new Vector3(size*.29f,size*.10f,size*.15f);
                bow.transform.localRotation=Quaternion.Euler(0,0,(i==0?-1:1)*18f);
            }
        }
        void Balloons(Transform p, Vector3 center, int count, float spread)
        {
            var tiePositions = new Vector3[count];
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996f;
                float radial=spread*(.3f+.7f*Mathf.Sqrt((i+1f)/count));
                Vector3 pos=center+new Vector3(Mathf.Cos(a)*radial,(i%4)*.12f,
                    Mathf.Sin(a)*radial*.5f);
                tiePositions[i] = pos;
                var balloon=Ball(p,"Balloons_"+i,pos,.20f+(i%3)*.035f,
                    i%3==0?_gold:i%3==1?_ivory:_rose);
                balloon.transform.localScale=new Vector3(.21f+(i%3)*.028f,
                    .25f+(i%3)*.035f,.20f+(i%3)*.027f);
                // Subtle tie knots on alternate balloons avoid 26 additional draw calls.
                if ((i&1)==0)
                    Ball(p,"BalloonKnot",pos-Vector3.up*(.133f+(i%3)*.017f),.03f,_gold);
            }
            // One merged swaying-string mesh for the whole balloon cluster.
            FineMesh(p,"BalloonSatinStrings",Vector3.zero,
                BirthdayFineMesh.BalloonStrings(tiePositions),_gold);
        }
        /// <summary>
        /// Single mesh ribbon, 18 curve samples in one draw rather than dozens of cubes.
        /// Lives on real CAD furniture anchors and shares the existing rose-gold material.
        /// Does not add any dynamic lights or physical obstacles.
        /// </summary>
        void Ribbon(Transform parent, string name, Vector3 start, Vector3 end,
            Vector3 faceNormal, float rise, float width, Material mat)
        {
            const int segments = 18;
            var positions = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                positions[i] = Vector3.Lerp(start, end, t) + Vector3.up *
                    (rise * Mathf.Sin(t * Mathf.PI * 3f));
            }
            var verts = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[verts.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                Vector3 tangent = (positions[Mathf.Min(segments, i+1)] -
                    positions[Mathf.Max(0, i-1)]).normalized;
                Vector3 perpendicular = Vector3.Cross(faceNormal, tangent).normalized;
                if (perpendicular.sqrMagnitude < .0001f) perpendicular = Vector3.up;
                verts[i*2] = positions[i] + perpendicular * width * .5f;
                verts[i*2+1] = positions[i] - perpendicular * width * .5f;
                uv[i*2] = new Vector2((float)i/segments, 0);
                uv[i*2+1] = new Vector2((float)i/segments, 1);
                if (i == segments) continue;
                int t = i*6, v=i*2;
                triangles[t] = v; triangles[t+1] = v+2; triangles[t+2] = v+1;
                triangles[t+3] = v+1; triangles[t+4] = v+2; triangles[t+5] = v+3;
            }
            var mesh = new Mesh { name="BirthdayRibbon_"+name, vertices=verts,
                triangles=triangles, uv=uv };
            mesh.RecalculateNormals();
            _ownedMeshes.Add(mesh);
            var ribbon = new GameObject("Birthday_Ribbon_" + name);
            ribbon.transform.SetParent(parent, false);
            ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ribbon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            DecorationCount++;
        }

        void StringLights(Transform p, Vector3 start, Vector3 finish, int count)
        {
            for(int i=0;i<count;i++)
            {
                float t=count<=1?0:(float)i/(count-1);
                var pos=Vector3.Lerp(start,finish,t);
                pos.y-=.04f*Mathf.Sin(t*Mathf.PI);
                var bulb=Ball(p,"Fairy_LED_"+i,pos,.04f,_glow);
                _bulbs.Add(bulb.GetComponent<Renderer>());
            }
        }
        void Frame(Transform p, string number, float z)
        {
            // Four true picture-moulding rails in one mesh; the center remains open
            // so private photo pixels are never covered by a solid gold rectangle.
            FineMesh(p,"PhotoFrameMoulding_"+number,new Vector3(0,1.65f,z),
                BirthdayFineMesh.FrameMoulding(.31f,.40f),_gold);
            var placeholder=Shape(p,PrimitiveType.Cube,"PrivatePhotoPlaceholder_"+number,
                new Vector3(-.024f,1.65f,z),new Vector3(.011f,.34f,.25f),_photoMat);
            var label=Words(p,"PHOTO " + number,new Vector3(-.035f,1.65f,z),.0065f,90);
            // B1 memory gallery reads explicit numbered local photo slots on launch.
            var photo=GameObject.CreatePrimitive(PrimitiveType.Quad);
            photo.name="Birthday_LocalMemoryPhoto_"+number;
            photo.transform.SetParent(p,false);
            photo.transform.localPosition=new Vector3(-.041f,1.65f,z);
            photo.transform.localRotation=Quaternion.Euler(0,-90f,0);
            photo.transform.localScale=new Vector3(.25f,.32f,1);
            var col=photo.GetComponent<Collider>();if(col)col.enabled=false;
            var view=photo.GetComponent<Renderer>();
            int slot=int.Parse(number);
            view.enabled=SceneWorkspaceBirthdayPhotoSlots.TryApply(view,slot);
            if (view.enabled)
            {
                // A loaded memory photo must not have PHOTO 01 lettering in front of it.
                label.SetActive(false);
                placeholder.GetComponent<Renderer>().enabled=false;
            }
        }
        void Create(Transform environment)
        {
            _ivory=MakeMaterial("Ivory",new Color(.96f,.90f,.78f));
            _gold=MakeMaterial("Champagne",new Color(.78f,.54f,.23f),true);
            _rose=MakeMaterial("Blush",new Color(.94f,.7f,.74f));
            _white=MakeMaterial("White",new Color(1f,.97f,.90f));
            _leaves=MakeMaterial("Leaves",new Color(.21f,.45f,.29f));
            _glow=MakeMaterial("LED",new Color(1f,.80f,.38f));
            _photoMat=MakeMaterial("PlaceholderPhotoMat",new Color(.30f,.23f,.20f));
            var entry=BoundsOf(FindFurniture(environment,"EntranceShoeCabinet"));
            var cabinet=BoundsOf(FindFurniture(environment,"DiningSideboard"));
            var dining=BoundsOf(FindFurniture(environment,"Move_Dining_Table"));
            var sofa=BoundsOf(FindFurniture(environment,"Move_Sofa"));
            var coffee=BoundsOf(FindFurniture(environment,"Move_Coffee_Table"));

            // North entry: use the real shoe-cabinet top, not the doorway floor.
            var foyer=MakeAnchor(environment,"EntranceShoeCabinet","NorthWelcome",
                new Vector3(entry.center.x,entry.max.y,entry.center.z));
            Box(foyer,"WelcomeBoard",new Vector3(0,.39f,0),
                new Vector3(.62f,.68f,.045f),_ivory);
            Words(foyer,"HAPPY\nBIRTHDAY",new Vector3(0,.42f,-.03f),.014f,180);
            Balloons(foyer,new Vector3(-.41f,.64f,0),10,.16f);
            Ribbon(foyer,"WelcomeSwag",new Vector3(-.32f,.78f,-.048f),
                new Vector3(.32f,.78f,-.048f), Vector3.back, .055f,.013f,_gold);
            Gift(foyer,new Vector3(.38f,.01f,-.1f),.18f,true);

            // Sideboard against northern wall: bouquet + gifts and shelf-level fairy lights.
            var shelf=MakeAnchor(environment,"DiningSideboard","NorthCabinetGifts",
                new Vector3(cabinet.center.x,cabinet.max.y,cabinet.center.z));
            Bouquet(shelf,new Vector3(-.28f,0,0),.28f);
            Gift(shelf,new Vector3(.20f,.01f,0),.17f,true);
            Gift(shelf,new Vector3(.41f,.01f,.025f),.13f,false);
            StringLights(shelf,new Vector3(-.5f,.025f,-.12f),new Vector3(.5f,.025f,-.12f),6);
            Ribbon(shelf,"CabinetSwag",new Vector3(-.50f,.08f,-.14f),
                new Vector3(.50f,.08f,-.14f),Vector3.back,.045f,.012f,_gold);

            // Dining: the cake sits on the actual CAD tabletop.
            var table=MakeAnchor(environment,"Move_Dining_Table","DiningCake",
                new Vector3(dining.center.x,dining.max.y,dining.center.z));
            Cylinder(table,"CakePlate",new Vector3(0,.016f,0),new Vector3(.25f,.016f,.25f),_gold);
            Cylinder(table,"CakeBase",new Vector3(0,.115f,0),new Vector3(.19f,.10f,.19f),_white);
            Cylinder(table,"CakeCreamTop",new Vector3(0,.23f,0),new Vector3(.15f,.052f,.15f),_ivory);
            FineMesh(table,"CakeIcingScallops",Vector3.zero,
                BirthdayFineMesh.CakeIcing(.152f,.240f),_ivory);
            FineMesh(table,"CakeThreeCandles",Vector3.zero,
                BirthdayFineMesh.CandleTrio(),_white,_glow);
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2/10f;
                Ball(table,"CakePearl",new Vector3(Mathf.Cos(a)*.125f,.281f,Mathf.Sin(a)*.125f),
                    .030f,i%2==0?_rose:_gold);
            }
            Words(table,"HAPPY BIRTHDAY",new Vector3(0,.42f,0),.005f,180);
            Bouquet(table,new Vector3(-.38f,0,.03f),.15f);

            // South living: sofa is east of the coffee table in the Hefu CAD layout.
            // Gallery faces WEST toward the coffee table, 20cm before the east wall.
            var gallery=MakeAnchor(environment,"Move_Sofa","SouthLivingGallery",
                new Vector3(sofa.max.x-.20f,0,sofa.center.z));
            Frame(gallery,"01",-.39f);Frame(gallery,"02",0);Frame(gallery,"03",.39f);
            Words(gallery,"OUR MOMENTS",new Vector3(-.04f,2.10f,0),.010f,90);
            Words(gallery,"HAPPY BIRTHDAY",new Vector3(-.04f,2.39f,0),.013f,90);
            StringLights(gallery,new Vector3(-.03f,2.59f,-.75f),
                new Vector3(-.03f,2.59f,.75f),8);
            Ribbon(gallery,"PhotoWallSwag",new Vector3(-.045f,2.52f,-.74f),
                new Vector3(-.045f,2.52f,.74f),Vector3.left,.075f,.020f,_gold);
            Balloons(gallery,new Vector3(.02f,1.76f,-.74f),8,.11f);
            Balloons(gallery,new Vector3(.02f,1.76f,.74f),8,.11f);
            var coffeeTop=MakeAnchor(environment,"Move_Coffee_Table","SouthCoffeeFlowers",
                new Vector3(coffee.center.x,coffee.max.y,coffee.center.z));
            Bouquet(coffeeTop,Vector3.zero,.18f);
            AddGlow(table,new Vector3(0,.45f,0),1.1f,.18f);
            AddGlow(gallery,new Vector3(-.30f,1.90f,0),2.2f,.14f);
            ApplyLights();
        }
        void AddGlow(Transform parent,Vector3 pos,float range,float intensity)
        {
            var go=new GameObject("BirthdaySoftLight");
            go.transform.SetParent(parent,false);go.transform.localPosition=pos;
            var l=go.AddComponent<Light>();l.type=LightType.Point;
            l.range=range;l.intensity=intensity;l.color=new Color(1f,.76f,.46f);
            _lights.Add(l);
        }
        public void ToggleLights(){LightsOn=!LightsOn;ApplyLights();}
        void ApplyLights()
        {
            foreach(var light in _lights)if(light)light.enabled=LightsOn;
            foreach(var bulb in _bulbs)if(bulb)bulb.enabled=LightsOn;
        }
        void OnDestroy()
        {
            foreach(var mat in _owned)if(mat)
                {if(Application.isPlaying)Destroy(mat);else DestroyImmediate(mat);}
            foreach(var mesh in _ownedMeshes)if(mesh)
                {if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        }
    }
}
