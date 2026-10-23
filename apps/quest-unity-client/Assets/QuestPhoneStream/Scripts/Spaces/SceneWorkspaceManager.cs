using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace QuestPhoneStream.Spaces
{
    /// <summary>Replace only EnvironmentRoot; XR, WebRTC and SpatialPanels stay alive.</summary>
    public sealed class SceneWorkspaceManager:MonoBehaviour
    {
        public SceneWorkspaceStore Store {get;private set;}
        public SceneWorkspaceIndex Catalog {get;private set;}
        public SceneWorkspaceRecord Active {get;private set;}
        public Transform EnvironmentRoot {get;private set;}
        public string LastError {get;private set;}
        public event Action<SceneWorkspaceRecord> SceneChanged;
        SceneWorkspaceBrowser _browser;
        AssetBundle _bundle;
        readonly Stack<SceneWorkspaceRecord> _undo=new Stack<SceneWorkspaceRecord>();
        readonly Dictionary<string,GameObject> _objects=new Dictionary<string,GameObject>();
        bool _ready;
        // Unit-test hook; runtime uses only its own app-private persistentDataPath.
        public static string PersistentRootOverrideForTests;
        void Awake(){Initialize(PersistentRootOverrideForTests??Application.persistentDataPath);}
        public void Initialize(string path)
        {
            if(_ready)return;
            Store=new SceneWorkspaceStore(path);
            try{Catalog=Store.LoadCatalog();}
            catch(Exception ex){LastError=ex.Message;Debug.LogError("[Spaces] "+ex);enabled=false;return;}
            if(Catalog.scenes.Count==0)
            {
                var empty=SceneWorkspaceStore.Create("Blank Studio");
                Catalog.scenes.Add(empty);
                Catalog.scenes.Add(SceneWorkspaceStore.Create("HefuVR (private)","cad-bundle","hefu/home.bundle"));
                Catalog.activeSceneId=empty.id;
                Store.SaveScene(empty);Store.SaveCatalog(Catalog);
            }
            EnvironmentRoot=new GameObject("SceneWorkspace_EnvironmentRoot").transform;
            EnvironmentRoot.SetParent(transform,false);
            EnvironmentRoot.localPosition=Vector3.zero;
            EnvironmentRoot.localRotation=Quaternion.identity;
            EnvironmentRoot.localScale=Vector3.one;
            _ready=true;
            if(!Select(Catalog.activeSceneId))
            {
                var first=Catalog.scenes.Find(s=>!s.isDeleted && s.sourceType=="blank");
                if(first!=null)Select(first.id);
            }
            Debug.Log("[Spaces] ready sceneCount="+Catalog.scenes.Count);
        }
        public bool Select(string id)
        {
            if(!_ready || !SceneWorkspaceValidation.SafeId(id))return false;
            var metadata=Catalog.scenes.Find(s=>s.id==id&&!s.isDeleted);
            if(metadata==null){LastError="Scene missing or deleted";return false;}
            var record=Store.LoadScene(id)??metadata;
            if(!SceneWorkspacePrivateImport.TrySeedFurniture(Store,record,out string importError))
            {LastError=importError;return false;}
            if(!Stage(record,out GameObject staged,out AssetBundle bundle))return false;
            bool shareBundle=bundle!=null && ReferenceEquals(bundle,_bundle);
            ClearEnvironment(shareBundle);
            staged.transform.SetParent(EnvironmentRoot,false);
            _bundle=bundle;Active=record;Catalog.activeSceneId=id;
            Store.SaveScene(Active);
            Store.SaveCatalog(Catalog);
            _undo.Clear();IndexObjects();
            Physics.SyncTransforms();
            SceneChanged?.Invoke(Active);_browser?.Refresh();return true;
        }
        bool Stage(SceneWorkspaceRecord record,out GameObject staged,out AssetBundle bundle)
        {
            staged=new GameObject("Environment_"+record.id);bundle=null;
            try
            {
                if(record.sourceType=="blank")BuildBlank(staged.transform);
                else if(record.sourceType=="cad-bundle")
                {
                    if(string.IsNullOrWhiteSpace(record.assetKey) || record.assetKey.Contains("..") ||
                       Path.IsPathRooted(record.assetKey))throw new InvalidDataException("Unsafe asset key");
                    string target=Path.Combine(Store.Folder,"Assets",record.assetKey);
                    if(!File.Exists(target))throw new FileNotFoundException("Private scene not installed: "+record.assetKey);
                    bundle=(_bundle!=null && Active!=null && Active.assetKey==record.assetKey)
                        ? _bundle : AssetBundle.LoadFromFile(target);
                    if(!bundle)throw new InvalidDataException("Cannot load private CAD bundle");
                    var prefabs=bundle.LoadAllAssets<GameObject>();
                    if(prefabs.Length!=1)throw new InvalidDataException("Bundle requires one environment prefab");
                    Instantiate(prefabs[0],staged.transform,false).name="CAD_ImportedGeometry";
                }
                else throw new NotSupportedException("Room scan adapter comes in S2");
                foreach(var item in record.objects)
                {
                    if(item.assetId.StartsWith("cad/",StringComparison.Ordinal))
                    {
                        SceneWorkspacePrivateImport.FindAndBind(staged.transform,item);
                        continue;
                    }
                    if(item.deleted)continue;
                    if(CreateObject(staged.transform,item)==null)
                        throw new InvalidDataException("Unknown asset: "+item.assetId);
                }
                if (record.decorationPreset == "birthday-v1")
                    SceneWorkspaceBirthday.Build(staged.transform, record);
                LastError=null;return true;
            }
            catch(Exception ex)
            {
                LastError=ex.Message;
                Debug.LogWarning("[Spaces] "+LastError);
                if(bundle && !ReferenceEquals(bundle,_bundle)){bundle.Unload(true);bundle=null;}
                DestroySafe(staged);staged=null;return false;
            }
        }
        static void BuildBlank(Transform parent)
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="BlankStudioFloor";floor.transform.SetParent(parent,false);
            floor.transform.localPosition=new Vector3(0,-.025f,0);
            floor.transform.localScale=new Vector3(6,.05f,6);
            floor.GetComponent<Renderer>().material.color=new Color(.22f,.29f,.34f);
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name="OneMeterReference";marker.transform.SetParent(parent,false);
            marker.transform.localPosition=new Vector3(2.5f,.5f,2.5f);
            marker.transform.localScale=new Vector3(.05f,1,.05f);
        }
        static GameObject CreateObject(Transform parent,SceneObjectRecord item)
        {
            if (item.assetId.StartsWith(SceneWorkspaceBirthdayAssets.Prefix, StringComparison.Ordinal))
            {
                if (!SceneWorkspaceBirthdayAssets.IsSupported(item.assetId) ||
                    !SceneWorkspaceBirthdayAssets.IsAnchor(item.anchorId))
                    throw new InvalidDataException("Unrecognized birthday decoration");
                SceneWorkspaceObject anchor = null;
                foreach (var candidate in parent.GetComponentsInChildren<SceneWorkspaceObject>(true))
                    if (candidate.persistentId == item.anchorId) { anchor = candidate; break; }
                if (!anchor || !anchor.gameObject.activeInHierarchy)
                    throw new InvalidDataException("Decoration furniture anchor is missing");
                return SceneWorkspaceBirthdayAssets.Create(anchor.transform, item);
            }
            PrimitiveType shape;
            switch(item.assetId)
            {
                case "primitive/cube":shape=PrimitiveType.Cube;break;
                case "primitive/sphere":shape=PrimitiveType.Sphere;break;
                case "primitive/cylinder":shape=PrimitiveType.Cylinder;break;
                default:return null;
            }
            var go=GameObject.CreatePrimitive(shape);
            go.name="SpaceObject_"+item.id;go.transform.SetParent(parent,false);
            go.transform.SetLocalPositionAndRotation(item.localPosition,item.localRotation);
            go.transform.localScale=item.localScale;
            var tag=go.AddComponent<SceneWorkspaceObject>();
            tag.persistentId=item.id;tag.editable=item.editable;
            return go;
        }
        void IndexObjects()
        {
            _objects.Clear();
            foreach(var o in EnvironmentRoot.GetComponentsInChildren<SceneWorkspaceObject>(true))
                _objects[o.persistentId]=o.gameObject;
        }
        public SceneWorkspaceRecord CreateBlank(string label)
        {
            if(!_ready)return null;
            var record=SceneWorkspaceStore.Create(label);
            Store.SaveScene(record);Catalog.scenes.Add(record);Store.SaveCatalog(Catalog);
            _browser?.Refresh();return record;
        }
        public SceneWorkspaceRecord Duplicate(string id)
        {
            var source=Catalog.scenes.Find(x=>x.id==id&&!x.isDeleted);
            if(source==null)return null;
            var original=Store.LoadScene(id)??source;
            var copy=JsonUtility.FromJson<SceneWorkspaceRecord>(JsonUtility.ToJson(original));
            copy.id=Guid.NewGuid().ToString("N");copy.displayName=original.displayName+" (copy)";
            copy.revision=1;copy.isDeleted=false;
            copy.createdUtcMs=copy.modifiedUtcMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Store.SaveScene(copy);Catalog.scenes.Add(copy);Store.SaveCatalog(Catalog);
            _browser?.Refresh();return copy;
        }
        /// <summary>
        /// Opt-in private Hefu birthday copy: preserves the original room and furniture,
        /// and never publishes CAD/photos to git or a network service.
        /// </summary>
        public bool SelectBirthdayScene()
        {
            if (!_ready || Catalog == null) return false;
            var existing = Catalog.scenes.Find(x => !x.isDeleted &&
                x.decorationPreset == "birthday-v1" && x.sourceType == "cad-bundle");
            if (existing != null) return Select(existing.id);

            var original = Catalog.scenes.Find(x => !x.isDeleted &&
                x.sourceType == "cad-bundle" && x.assetKey == "hefu/home.bundle" &&
                string.IsNullOrEmpty(x.decorationPreset));
            if (original == null)
            {
                LastError = "Original HefuVR private scene missing";
                return false;
            }
            var source = Store.LoadScene(original.id) ?? original;
            if (!SceneWorkspacePrivateImport.TrySeedFurniture(Store, source, out string error))
            {
                LastError = error;
                return false;
            }
            Store.SaveScene(source);
            var copy = Duplicate(original.id);
            if (copy == null) return false;
            copy.displayName = "HefuVR Birthday";
            copy.decorationPreset = "birthday-v1";
            Store.SaveScene(copy);
            int at = Catalog.scenes.FindIndex(x => x.id == copy.id);
            if (at >= 0) Catalog.scenes[at] = copy;
            Store.SaveCatalog(Catalog);
            bool loaded = Select(copy.id);
            if (!loaded) Debug.LogWarning("[Spaces] birthday copy created but scene cannot load: " + LastError);
            return loaded;
        }

        public bool SelectOriginalHefu()
        {
            if (!_ready || Catalog == null) return false;
            var original = Catalog.scenes.Find(x => !x.isDeleted &&
                x.sourceType == "cad-bundle" && x.assetKey == "hefu/home.bundle" &&
                string.IsNullOrEmpty(x.decorationPreset));
            if (original == null) { LastError = "Original HefuVR scene unavailable"; return false; }
            return Select(original.id);
        }

        public bool Rename(string id,string name)
        {
            var record=Catalog.scenes.Find(x=>x.id==id&&!x.isDeleted);
            if(record==null||string.IsNullOrWhiteSpace(name)||name.Length>100)return false;
            record.displayName=name;record.modifiedUtcMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var full=Store.LoadScene(id)??record;full.displayName=name;
            Store.SaveScene(full);Store.SaveCatalog(Catalog);
            if(Active!=null && Active.id==id)Active.displayName=name;
            _browser?.Refresh();return true;
        }
        public bool DeleteScene(string id)
        {
            var record=Catalog.scenes.Find(x=>x.id==id&&!x.isDeleted);
            if(record==null||Catalog.activeSceneId==id)return false;
            record.isDeleted=true;Store.SaveCatalog(Catalog);_browser?.Refresh();return true;
        }
        public bool RestoreScene(string id)
        {
            var record=Catalog.scenes.Find(x=>x.id==id&&x.isDeleted);
            if(record==null)return false;
            record.isDeleted=false;Store.SaveCatalog(Catalog);_browser?.Refresh();return true;
        }
        public bool AddPrimitive(string shape,Vector3 localPosition)
        {
            if(Active==null)return false;
            var record=new SceneObjectRecord{
                id=Guid.NewGuid().ToString("N"),assetId="primitive/"+shape,
                label=shape,localPosition=localPosition,localScale=Vector3.one*.35f};
            var obj=CreateObject(EnvironmentRoot,record);
            if(obj==null)return false;
            Snapshot();Active.objects.Add(record);_objects[record.id]=obj;
            return SaveActive();
        }
        /// <summary>Snap one editable birthday asset to a real CAD furniture surface.
        /// Stored transform is furniture-local so moving furniture carries the decoration.
        /// </summary>
        public SceneWorkspaceObject AddBirthdayDecoration(string name, string furniture)
        {
            if (Active == null || Active.decorationPreset != "birthday-v1" ||
                !SceneWorkspaceBirthdayAssets.IsSupported("birthday/" + name) ||
                !SceneWorkspaceBirthdayAssets.IsAnchor("hefu-" + furniture))
                return null;
            // Limit editable additions to protect performance and scene storage.
            int count = Active.objects.FindAll(x => x.kind == "birthday" && !x.deleted).Count;
            if (count >= 40) { LastError = "Birthday limit: 40 editable objects"; return null; }
            SceneWorkspaceObject anchor = null;
            foreach (var tag in EnvironmentRoot.GetComponentsInChildren<SceneWorkspaceObject>(true))
                if (tag.persistentId == "hefu-" + furniture) { anchor = tag; break; }
            if (!anchor || !anchor.gameObject.activeInHierarchy) return null;
            // Only CAD renderers; ignore already attached birthday decorations.
            bool first = true;
            Bounds surface = new Bounds(anchor.transform.position, Vector3.zero);
            foreach (var mesh in anchor.GetComponentsInChildren<Renderer>(true))
            {
                if (!mesh.enabled || !mesh.gameObject.activeInHierarchy) continue;
                bool decoration = false;
                for (var node = mesh.transform; node && node != anchor.transform; node = node.parent)
                    if (node.name.StartsWith("Birthday_", StringComparison.Ordinal)) { decoration = true; break; }
                if (decoration) continue;
                if (first) { surface = mesh.bounds; first = false; }
                else surface.Encapsulate(mesh.bounds);
            }
            if (first) return null;
            // Small X offset keeps new item clear of original birthday hero asset.
            float offset = Mathf.Clamp(((count % 5) - 2) * .14f, -.28f, .28f);
            Vector3 desired = new Vector3(Mathf.Clamp(surface.center.x + offset,
                surface.min.x + .16f, surface.max.x - .16f),
                surface.max.y + .012f, surface.center.z);
            // For narrow furniture, clamp can invert. Fall back to true bounds center.
            if (surface.size.x < .34f) desired.x = surface.center.x;
            var record = new SceneObjectRecord {
                id = Guid.NewGuid().ToString("N"), assetId = "birthday/" + name,
                anchorId = "hefu-" + furniture, label = "Birthday " + name,
                kind = "birthday", editable = true,
                localPosition = anchor.transform.InverseTransformPoint(desired),
                localRotation = Quaternion.identity, localScale = Vector3.one
            };
            GameObject item = null;
            try
            {
                SceneWorkspaceValidation.RequireValid(Active);
                item = CreateObject(EnvironmentRoot, record);
                Snapshot(); Active.objects.Add(record); _objects[record.id] = item;
                if (!SaveActive()) throw new InvalidDataException("Cannot save birthday decor");
                return item.GetComponent<SceneWorkspaceObject>();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                if (item) DestroySafe(item);
                _objects.Remove(record.id);
                Active.objects.Remove(record);
                Debug.LogWarning("[Spaces] Add birthday decor rejected: " + ex.Message);
                return null;
            }
        }

        /// <summary>Transactional, per-scene birthday palette and private photo slot metadata.</summary>
        public bool ChangeBirthdayPresentation(string id, int style, int slot)
        {
            if (Active == null || Active.decorationPreset != "birthday-v1" ||
                style < 0 || style >= SceneWorkspaceBirthdayAssets.StyleCount ||
                !SceneWorkspaceBirthdayPhotoSlots.ValidSlot(slot))
                return false;
            var record=GetObjectRecord(id);
            if (record == null || record.kind != "birthday" ||
                (slot > 0 && record.assetId != "birthday/frame") ||
                !_objects.TryGetValue(id,out GameObject obj))
                return false;
            int beforeStyle=record.birthdayStyle, beforeSlot=record.birthdayPhotoSlot;
            if (beforeStyle==style && beforeSlot==slot) return true;
            record.birthdayStyle=style;record.birthdayPhotoSlot=slot;
            try{SceneWorkspaceValidation.RequireValid(Active);}
            catch{record.birthdayStyle=beforeStyle;record.birthdayPhotoSlot=beforeSlot;return false;}
            record.birthdayStyle=beforeStyle;record.birthdayPhotoSlot=beforeSlot;
            Snapshot();
            record.birthdayStyle=style;record.birthdayPhotoSlot=slot;
            if (!SaveActive())
            {
                record.birthdayStyle=beforeStyle;record.birthdayPhotoSlot=beforeSlot;
                SceneWorkspaceBirthdayAssets.ApplyPresentation(obj,record);
                return false;
            }
            SceneWorkspaceBirthdayAssets.ApplyPresentation(obj,record);
            return true;
        }

        public SceneObjectRecord GetObjectRecord(string id)
        {
            return Active?.objects.Find(x => x.id == id && !x.deleted);
        }

        public bool UpdateObject(string id,Vector3 localPosition,Quaternion localRotation,Vector3 localScale)
        {
            if(Active==null)return false;
            var record=Active.objects.Find(x=>x.id==id&&!x.deleted&&x.editable);
            if(record==null || !_objects.TryGetValue(id,out GameObject obj))return false;
            var beforePos=record.localPosition;var beforeRot=record.localRotation;var beforeScale=record.localScale;
            record.localPosition=localPosition;record.localRotation=localRotation;record.localScale=localScale;
            try{SceneWorkspaceValidation.RequireValid(Active);}
            catch{record.localPosition=beforePos;record.localRotation=beforeRot;record.localScale=beforeScale;return false;}
            record.localPosition=beforePos;record.localRotation=beforeRot;record.localScale=beforeScale;
            Snapshot();record.localPosition=localPosition;record.localRotation=localRotation;record.localScale=localScale;
            obj.transform.SetLocalPositionAndRotation(localPosition,localRotation);obj.transform.localScale=localScale;
            return SaveActive();
        }
        public bool RemoveObject(string id)
        {
            if(Active==null)return false;
            var record=Active.objects.Find(x=>x.id==id&&!x.deleted&&x.editable);
            if(record==null)return false;
            Snapshot();record.deleted=true;
            if(_objects.TryGetValue(id,out GameObject obj)){obj.SetActive(false);_objects.Remove(id);}
            return SaveActive();
        }
        public bool Undo()
        {
            if(Active==null||_undo.Count==0)return false;
            var previous=_undo.Peek();
            if(!Stage(previous,out GameObject staged,out AssetBundle bundle))return false;
            _undo.Pop();ClearEnvironment(bundle!=null && ReferenceEquals(bundle,_bundle));
            staged.transform.SetParent(EnvironmentRoot,false);_bundle=bundle;
            Active=previous;IndexObjects();
            Physics.SyncTransforms();
            Store.SaveScene(Active);
            int pos=Catalog.scenes.FindIndex(x=>x.id==Active.id);
            if(pos>=0)Catalog.scenes[pos]=Active;
            Store.SaveCatalog(Catalog);SceneChanged?.Invoke(Active);_browser?.Refresh();return true;
        }
        void Snapshot()
        {
            if(_undo.Count==30)
            {
                var all=_undo.ToArray();_undo.Clear();
                for(int i=all.Length-2;i>=0;i--)_undo.Push(all[i]);
            }
            _undo.Push(JsonUtility.FromJson<SceneWorkspaceRecord>(JsonUtility.ToJson(Active)));
        }
        public bool SaveActive()
        {
            if(Active==null)return false;
            SceneWorkspaceValidation.RequireValid(Active);
            Active.revision++;
            Active.modifiedUtcMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Store.SaveScene(Active);
            int pos=Catalog.scenes.FindIndex(x=>x.id==Active.id);
            if(pos>=0)Catalog.scenes[pos]=Active;
            Store.SaveCatalog(Catalog);_browser?.Refresh();return true;
        }
        void ClearEnvironment(bool keepBundle=false)
        {
            if(EnvironmentRoot!=null)
                foreach(Transform item in EnvironmentRoot)
                {
                    // Destroy is deferred in PlayMode. Disable colliders and renderers
                    // immediately so same-frame selection/Undo cannot hit stale scene.
                    item.gameObject.SetActive(false);
                    DestroySafe(item.gameObject);
                }
            if(_bundle && !keepBundle){_bundle.Unload(false);_bundle=null;}
            _objects.Clear();
        }
        static void DestroySafe(UnityEngine.Object go)
        {
            if(!go)return;
            if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);
        }
        public void ShowBrowser(Camera camera)
        {
            if(!_ready||!camera)return;
            if(!_browser)_browser=gameObject.AddComponent<SceneWorkspaceBrowser>();
            _browser.Show(this,camera);
        }
        void OnDestroy(){if(_bundle)_bundle.Unload(false);}
    }
    public sealed class SceneWorkspaceObject:MonoBehaviour
    {
        public string persistentId;
        public bool editable;
    }
    internal static class SceneWorkspaceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            foreach(var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
                if(!receiver.GetComponent<SceneWorkspaceManager>())
                    receiver.gameObject.AddComponent<SceneWorkspaceManager>();
            foreach(var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
                if(!receiver.GetComponent<SceneWorkspaceLocomotion>())
                    receiver.gameObject.AddComponent<SceneWorkspaceLocomotion>();
            foreach(var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
                if(!receiver.GetComponent<SceneWorkspaceDoors>())
                    receiver.gameObject.AddComponent<SceneWorkspaceDoors>();
        }
    }
}
