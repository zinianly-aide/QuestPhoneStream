using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream.Spaces
{
    // Separate opt-in edit mode; uses EXISTING QuestXrUiRig right-hand Grip input.
    // No XRI select binding, WebRTC protocol changes, or extra XR controllers.
    public sealed class SceneWorkspaceManipulator : MonoBehaviour
    {
        public SceneWorkspaceManager Workspace { get; private set; }
        public SceneWorkspaceObject Selected { get; private set; }
        public bool Editing { get; private set; }
        public bool Dragging { get; private set; }
        public string Status { get; private set; } = "Edit inactive";
        public float maxReach = 4.5f, maxMove = 3f;
        Transform _right;
        InputAction _grip;
        bool _pressed;
        float _depth;
        Vector3 _startPoint, _startWorld, _startLocal;
        Quaternion _startRotation;
        Vector3 _startScale;
        readonly HashSet<Collider> _existingHits = new HashSet<Collider>();
        readonly HashSet<Collider> _currentHits = new HashSet<Collider>();
        static readonly Collider[] _hits = new Collider[256];
        Canvas _toolbar;
        Text _status;
        Camera _eye;
        LineRenderer _hoverOutline;
        TextMesh _hoverLabel;
        readonly Dictionary<SceneWorkspaceObject,GameObject> _editableMarkers =
            new Dictionary<SceneWorkspaceObject,GameObject>();
        bool _showMarkers = true;
        int _focusedId;
        int _birthdayAnchorIndex;
        bool _birthdaySnap = true;
        Button _anchorButton, _snapButton;
        readonly List<Button> _birthdayButtons=new List<Button>();
        static readonly string[] BirthdayAnchors = SceneWorkspaceBirthdayAssets.Anchors;

        public void Initialize(SceneWorkspaceManager manager)
        {
            if(Workspace==manager)return;
            if(Workspace!=null)Workspace.SceneChanged-=OnSceneChanged;
            Workspace=manager;
            if(manager!=null)manager.SceneChanged+=OnSceneChanged;
        }
        void OnSceneChanged(SceneWorkspaceRecord _)
        {
            Selected=null;Dragging=false;SetStatus("Scene changed");
            ClearMarkers();
            if(Editing){BuildMarkers();UpdateBirthdayToolbar();}
        }
        public void Enter(Camera camera)
        {
            if(Workspace?.Active==null || camera==null)return;
            _eye=camera;Editing=true;_pressed=false;
            if(!_toolbar)BuildToolbar();
            UpdateBirthdayToolbar();
            BuildMarkers();
            EnsureHoverVisuals();
            var horizontal=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            if(horizontal.sqrMagnitude<.01f)horizontal=Vector3.forward;
            _toolbar.transform.position=camera.transform.position+horizontal*1.45f-
                Vector3.up*.47f+camera.transform.right*.36f;
            _toolbar.transform.rotation=Quaternion.LookRotation(horizontal,Vector3.up);
            _toolbar.gameObject.SetActive(true);
            SetStatus("Grip furniture to move; Trigger/UI unchanged");
        }
        public void Exit()
        {
            FinishGrab(false);
            Editing=false;Selected=null;_pressed=false;
            ClearMarkers();
            if(_hoverOutline)_hoverOutline.gameObject.SetActive(false);
            if(_hoverLabel)_hoverLabel.gameObject.SetActive(false);
            if(_toolbar)_toolbar.gameObject.SetActive(false);
            SetStatus("Edit inactive");
            Workspace?.ShowBrowser(_eye);
        }
        void Update()
        {
            if(!Editing)return;
            if(!_right || _grip==null)
            {
                var rig=FindObjectOfType<QuestXrUiRig>();
                if(rig?.Actions!=null)
                {
                    _grip=rig.Actions.FindAction("RightHand Grab",false);
                    var go=GameObject.Find("Right Controller");
                    if(go)_right=go.transform;
                }
            }
            if(!_right || _grip==null)return;
            UpdateAffordances();
            bool pressed=_grip.IsPressed();
            var pointer=new Ray(_right.position,_right.forward);
            if(pressed&&!_pressed)BeginGrab(pointer);
            if(pressed&&Dragging)Drag(pointer);
            if(!pressed&&_pressed&&Dragging)FinishGrab(true);
            _pressed=pressed;
        }
        static bool IsBirthdayDecoration(Transform node, Transform editableRoot)
        {
            for (var current = node; current != null && current != editableRoot; current = current.parent)
                if (current.name.StartsWith("Birthday_", StringComparison.Ordinal)) return true;
            return false;
        }

        static Bounds VisualBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds=new Bounds(root.position,Vector3.one*.10f);
            bool first=true;
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                   IsBirthdayDecoration(renderer.transform,root))continue;
                if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        void ClearMarkers()
        {
            foreach(var marker in _editableMarkers.Values)
                if(marker)Destroy(marker);
            _editableMarkers.Clear();
        }

        void BuildMarkers()
        {
            ClearMarkers();
            if(!Workspace || !Workspace.EnvironmentRoot)return;
            var shader=Shader.Find("Unlit/Color");
            foreach(var tag in Workspace.EnvironmentRoot.GetComponentsInChildren<SceneWorkspaceObject>(true))
            {
                if(!tag.editable || !tag.gameObject.activeInHierarchy)continue;
                var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name="EditableMarker_"+tag.persistentId;
                marker.transform.SetParent(transform,false);
                marker.transform.localScale=Vector3.one*.075f;
                var collider=marker.GetComponent<Collider>();
                if(collider)Destroy(collider);
                var renderer=marker.GetComponent<Renderer>();
                if(shader)
                    renderer.material=new Material(shader){color=new Color(.21f,1f,.49f)};
                marker.SetActive(_showMarkers);
                _editableMarkers.Add(tag,marker);
            }
        }

        void EnsureHoverVisuals()
        {
            if(!_hoverOutline)
            {
                var go=new GameObject("WorkspaceHoverOutline");
                go.transform.SetParent(transform,false);
                _hoverOutline=go.AddComponent<LineRenderer>();
                _hoverOutline.useWorldSpace=true;
                _hoverOutline.positionCount=16;
                _hoverOutline.loop=false;
                _hoverOutline.widthMultiplier=.011f;
                _hoverOutline.material=new Material(Shader.Find("Sprites/Default"));
                _hoverOutline.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                _hoverOutline.receiveShadows=false;
            }
            if(!_hoverLabel)
            {
                var go=new GameObject("WorkspaceHoverLabel");
                go.transform.SetParent(transform,false);
                _hoverLabel=go.AddComponent<TextMesh>();
                _hoverLabel.fontSize=40;
                _hoverLabel.characterSize=.012f;
                _hoverLabel.anchor=TextAnchor.MiddleCenter;
            }
        }

        static void DrawBounds(LineRenderer line,Bounds b)
        {
            var c=b.center;var e=Vector3.Max(b.extents,Vector3.one*.045f);
            Vector3[] v=new Vector3[8];
            for(int i=0;i<8;i++)
                v[i]=c+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z);
            // One line shows the upper and lower rectangles plus side rails.
            int[] walk={0,1,3,2,0,4,5,7,6,4,5,1,3,7,6,2};
            for(int i=0;i<walk.Length;i++)line.SetPosition(i,v[walk[i]]);
        }

        void UpdateAffordances()
        {
            if(!_hoverOutline || !_hoverLabel)return;
            foreach(var pair in _editableMarkers)
                if(pair.Key && pair.Value)
                {
                    var b=VisualBounds(pair.Key.transform);
                    pair.Value.transform.position=b.center+Vector3.up*(b.extents.y+.10f);
                    pair.Value.SetActive(_showMarkers && pair.Key.gameObject.activeInHierarchy);
                }

            Collider hitCollider=null;
            SceneWorkspaceObject hover=null;
            SceneWorkspaceDoors.Door hoveredDoor=null;
            if(_right && RaycastSelection(new Ray(_right.position,_right.forward),out var hit))
            {
                hitCollider=hit.collider;
                if(hitCollider && hitCollider.transform.IsChildOf(Workspace.EnvironmentRoot))
                {
                    hover=hitCollider.GetComponentInParent<SceneWorkspaceObject>();
                    hoveredDoor=hitCollider.GetComponentInParent<SceneWorkspaceDoors.Door>();
                }
                else hitCollider=null;
            }
            Transform target=Dragging && Selected ? Selected.transform:
                (hover && hover.editable ? hover.transform:
                    hoveredDoor ? hoveredDoor.movingPart : hitCollider ? hitCollider.transform : null);
            if(!target)
            {
                _hoverOutline.gameObject.SetActive(false);
                _hoverLabel.gameObject.SetActive(false);
                _focusedId=0;
                return;
            }
            var editable=Dragging && Selected || hover && hover.editable;
            var record=editable && target ? Workspace.GetObjectRecord(
                Dragging && Selected ? Selected.persistentId : hover ? hover.persistentId : "") : null;
            var caption=record != null && record.kind == "birthday" ? "BIRTHDAY · Grip to place" :
                editable?"EDITABLE · Grip to move":
                hoveredDoor?"DOOR · Trigger to open in Explore":"LOCKED · Structure";
            var color=editable?new Color(.18f,1f,.45f):
                hoveredDoor?new Color(.20f,.85f,1f):new Color(.65f,.67f,.74f);
            Bounds bounds=VisualBounds(target);
            _hoverOutline.gameObject.SetActive(true);
            _hoverLabel.gameObject.SetActive(true);
            _hoverOutline.startColor=color;
            _hoverOutline.endColor=color;
            DrawBounds(_hoverOutline,bounds);
            _hoverLabel.color=color;
            _hoverLabel.text=caption;
            _hoverLabel.transform.position=bounds.center+Vector3.up*(bounds.extents.y+.17f);
            if(_eye)_hoverLabel.transform.rotation=Quaternion.LookRotation(
                _hoverLabel.transform.position-_eye.transform.position,Vector3.up);
            int id=target.GetInstanceID();
            if(_focusedId!=id && !Dragging)
            {
                _focusedId=id;
                SetStatus(caption+" : "+target.name);
            }
        }

        public void ToggleEditableMarkers()
        {
            _showMarkers=!_showMarkers;
            SetStatus(_showMarkers?"Editable markers ON":"Editable markers OFF");
        }

        // Ray interaction with B2 decorations uses trigger colliders so player locomotion
        // never hits them. The nearest non-trigger surface still blocks click-through.
        bool RaycastSelection(Ray pointer, out RaycastHit chosen)
        {
            chosen = default;
            var hits = Physics.RaycastAll(pointer,maxReach,~0,QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b) => a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(!hit.collider)continue;
                if(hit.collider.isTrigger)
                {
                    var decor=hit.collider.GetComponentInParent<SceneWorkspaceObject>();
                    if(decor == null || Workspace.GetObjectRecord(decor.persistentId)?.kind != "birthday")
                        continue;
                }
                chosen=hit;
                return true;
            }
            return false;
        }
        public bool TrySelect(Ray pointer)
        {
            if(!Editing || Workspace?.EnvironmentRoot==null)return false;
            if(!RaycastSelection(pointer,out RaycastHit hit))return false;
            var furniture=hit.collider.GetComponentInParent<SceneWorkspaceObject>();
            if(!furniture || !furniture.editable ||
               !furniture.transform.IsChildOf(Workspace.EnvironmentRoot))return false;
            Selected=furniture;SetStatus("Selected: "+furniture.name);return true;
        }
        public bool BeginGrab(Ray pointer)
        {
            if(!TrySelect(pointer))return false;
            if(!RaycastSelection(pointer,out RaycastHit hit))return false;
            var tr=Selected.transform;
            _depth=hit.distance;_startPoint=hit.point;
            _startWorld=tr.position;_startLocal=tr.localPosition;
            _startRotation=tr.localRotation;_startScale=tr.localScale;
            if(!IsBirthdayItem(Selected))Collect(Selected,_existingHits);
            else _existingHits.Clear();
            Dragging=true;return true;
        }
        bool IsBirthdayItem(SceneWorkspaceObject item)
        {
            return item != null && Workspace?.GetObjectRecord(item.persistentId)?.kind == "birthday";
        }
        public bool Drag(Ray pointer)
        {
            if(!Dragging||!Selected)return false;
            var next=_startWorld+(pointer.GetPoint(_depth)-_startPoint);
            next.y=_startWorld.y; // Furniture remains floor-based; decorations remain surface-based.
            if(Vector3.Distance(next,_startWorld)>maxMove)return false;
            var tr=Selected.transform;var previous=tr.position;
            if(IsBirthdayItem(Selected))
            {
                var anchor=tr.parent;
                if(!anchor) return false;
                var record=Workspace.GetObjectRecord(Selected.persistentId);
                if(record==null || anchor.GetComponent<SceneWorkspaceObject>()?.persistentId != record.anchorId)
                    return false;
                Bounds bounds=new Bounds(anchor.position,Vector3.one*.08f);
                bool hasBounds=false;
                foreach(var renderer in anchor.GetComponentsInChildren<Renderer>(true))
                {
                    if(!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                       IsBirthdayDecoration(renderer.transform,anchor))continue;
                    if(!hasBounds){bounds=renderer.bounds;hasBounds=true;}
                    else bounds.Encapsulate(renderer.bounds);
                }
                if(!hasBounds)return false;
                next.x=Mathf.Clamp(next.x,bounds.min.x+.05f,bounds.max.x-.05f);
                next.z=Mathf.Clamp(next.z,bounds.min.z+.05f,bounds.max.z-.05f);
                if(_birthdaySnap)
                {
                    next.x=Mathf.Round(next.x*20f)/20f;
                    next.z=Mathf.Round(next.z*20f)/20f;
                }
                tr.position=next;
                return true;
            }
            tr.position=next;
            if(HasNewCollision()){tr.position=previous;Physics.SyncTransforms();return false;}
            return true;
        }
        public bool FinishGrab(bool save)
        {
            if(!Dragging||!Selected){Dragging=false;return false;}
            Dragging=false;
            var tr=Selected.transform;
            if(!save || (tr.position-_startWorld).sqrMagnitude<.000001f)
            {RestoreInitial();return false;}
            if(!Commit())
            {RestoreInitial();SetStatus("Move rejected");return false;}
            SetStatus("Move saved · Undo available");return true;
        }
        void RestoreInitial()
        {
            if(!Selected)return;
            var tr=Selected.transform;
            tr.localPosition=_startLocal;tr.localRotation=_startRotation;tr.localScale=_startScale;
            Physics.SyncTransforms();
        }
        bool Commit()
        {
            var tr=Selected.transform;
            return Workspace.UpdateObject(Selected.persistentId,tr.localPosition,tr.localRotation,tr.localScale);
        }
        static bool IsProtected(Collider col,SceneWorkspaceObject item)
        {
            if(!col || !col.enabled || col.transform.IsChildOf(item.transform))return false;
            var another=col.GetComponentInParent<SceneWorkspaceObject>();
            if(another!=null)return another!=item;
            var node=col.transform;
            while(node!=null)
            {
                if(node.name.StartsWith("Installed_")||node.name.StartsWith("Door_"))return true;
                node=node.parent;
            }
            return col.name.Contains("CAD_wall");
        }
        static void Collect(SceneWorkspaceObject item,HashSet<Collider> output)
        {
            output.Clear();Physics.SyncTransforms();
            Bounds b=new Bounds(item.transform.position,Vector3.one*.10f);
            bool first=true;
            foreach(var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                   IsBirthdayDecoration(renderer.transform,item.transform))continue;
                if(first){b=renderer.bounds;first=false;}else b.Encapsulate(renderer.bounds);
            }
            Vector3 ext=Vector3.Max(b.extents-Vector3.one*.025f,Vector3.one*.015f);
            int count=Physics.OverlapBoxNonAlloc(b.center,ext,_hits,Quaternion.identity,~0,
                QueryTriggerInteraction.Ignore);
            if(count>=_hits.Length)throw new InvalidOperationException("Spatial edit overlap overflow");
            for(int i=0;i<count;i++)if(IsProtected(_hits[i],item))output.Add(_hits[i]);
        }
        bool HasNewCollision()
        {
            Collect(Selected,_currentHits);
            foreach(var col in _currentHits)if(!_existingHits.Contains(col))return true;
            return false;
        }
        public bool RotateSelected(float degrees)
        {
            if(!Editing||!Selected||Dragging || Mathf.Abs(degrees)>30f)return false;
            var tr=Selected.transform;var original=tr.localRotation;
            if(!IsBirthdayItem(Selected))Collect(Selected,_existingHits);
            tr.localRotation=Quaternion.Euler(0,degrees,0)*original;
            bool ok=(IsBirthdayItem(Selected)||!HasNewCollision())&&Commit();
            if(!ok)tr.localRotation=original;
            Physics.SyncTransforms();return ok;
        }
        public bool ScaleSelected(float factor)
        {
            if(!Editing||!Selected||Dragging || factor<.8f||factor>1.2f)return false;
            var tr=Selected.transform;var original=tr.localScale;
            Vector3 next=original*factor;
            if(next.x<.02f||next.y<.02f||next.z<.02f||
               next.x>10f||next.y>10f||next.z>10f)return false;
            if(!IsBirthdayItem(Selected))Collect(Selected,_existingHits);
            tr.localScale=next;
            bool ok=(IsBirthdayItem(Selected)||!HasNewCollision())&&Commit();
            if(!ok)tr.localScale=original;
            Physics.SyncTransforms();return ok;
        }
        public bool DeleteSelected()
        {
            if(!Editing||!Selected||Dragging)return false;
            bool ok=Workspace.RemoveObject(Selected.persistentId);
            if(ok){Selected=null;SetStatus("Deleted (Undo restores)");}
            return ok;
        }
        public bool Undo()
        {
            if(!Editing||Dragging)return false;
            Selected=null;bool ok=Workspace.Undo();
            if(ok)SetStatus("Undo applied");return ok;
        }
        void SetStatus(string text)
        {Status=text;if(_status)_status.text=text;}
        void BuildToolbar()
        {
            var root=new GameObject("WorkspaceEditToolbar",typeof(RectTransform));
            root.transform.SetParent(transform,false);
            _toolbar=root.AddComponent<Canvas>();_toolbar.renderMode=RenderMode.WorldSpace;
            _toolbar.worldCamera=_eye;_toolbar.sortingOrder=108;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rect=root.GetComponent<RectTransform>();
            rect.sizeDelta=new Vector2(650,464);
            rect.localScale=Vector3.one*.00125f;
            root.AddComponent<Image>().color=new Color(.045f,.07f,.12f,.95f);
            _status=SceneWorkspaceBrowser.Label(root.transform,"Edit",22,
                new Vector2(20,17),new Vector2(600,57));
            SceneWorkspaceBrowser.Action(root.transform,"Rotate -15",15,82,145,()=>RotateSelected(-15f));
            SceneWorkspaceBrowser.Action(root.transform,"Rotate +15",170,82,145,()=>RotateSelected(15f));
            SceneWorkspaceBrowser.Action(root.transform,"Scale -",324,82,140,()=>ScaleSelected(.95f));
            SceneWorkspaceBrowser.Action(root.transform,"Scale +",478,82,145,()=>ScaleSelected(1.05f));
            SceneWorkspaceBrowser.Action(root.transform,"Delete",15,155,142,()=>DeleteSelected());
            SceneWorkspaceBrowser.Action(root.transform,"Undo",171,155,142,()=>Undo());
            SceneWorkspaceBrowser.Action(root.transform,"Done",476,155,146,Exit);
            SceneWorkspaceBrowser.Action(root.transform,"Show/Hide marks",15,228,225,ToggleEditableMarkers);
            SceneWorkspaceBrowser.Label(root.transform,
                "Green: editable  /  Cyan: door  /  Gray: locked",
                19,new Vector2(252,228),new Vector2(395,58));
            string[] items=SceneWorkspaceBirthdayAssets.Items;
            for(int i=0;i<items.Length;i++)
            {
                string asset=items[i];
                _birthdayButtons.Add(SceneWorkspaceBrowser.Action(root.transform,"+"+asset,12+i*128,300,118,
                    ()=>AddBirthday(asset)));
            }
            _anchorButton=SceneWorkspaceBrowser.Action(root.transform,"Anchor: Table",15,371,305,
                CycleBirthdayAnchor);
            _snapButton=SceneWorkspaceBrowser.Action(root.transform,"Grid snap: ON",330,371,305,
                ()=>{_birthdaySnap=!_birthdaySnap;UpdateBirthdayToolbar();});
            SceneWorkspaceBrowser.Label(root.transform,
                "Birthday: choose furniture anchor + item; Grip to move (5cm grid)",
                17,new Vector2(18,430),new Vector2(620,30));
        }
        void CycleBirthdayAnchor()
        {
            _birthdayAnchorIndex=(_birthdayAnchorIndex+1)%BirthdayAnchors.Length;
            UpdateBirthdayToolbar();
        }
        void UpdateBirthdayToolbar()
        {
            bool active=Workspace?.Active?.decorationPreset=="birthday-v1";
            foreach(var item in _birthdayButtons)if(item)item.gameObject.SetActive(active);
            if(_anchorButton)_anchorButton.gameObject.SetActive(active);
            if(_snapButton)_snapButton.gameObject.SetActive(active);
            if(!active)return;
            var names=new []{"Dining table","Sideboard","Coffee table","Shoe cabinet"};
            if(_anchorButton)_anchorButton.GetComponentInChildren<Text>().text="Anchor: "+names[_birthdayAnchorIndex];
            if(_snapButton)_snapButton.GetComponentInChildren<Text>().text=
                "Grid snap: "+(_birthdaySnap?"ON":"OFF");
        }
        public bool AddBirthday(string asset)
        {
            if(!Editing || Workspace?.Active?.decorationPreset!="birthday-v1")return false;
            var tag=Workspace.AddBirthdayDecoration(asset,BirthdayAnchors[_birthdayAnchorIndex]);
            if(!tag){SetStatus("Could not place "+asset+": "+Workspace.LastError);return false;}
            Selected=tag; BuildMarkers();
            SetStatus("Birthday "+asset+" added to "+BirthdayAnchors[_birthdayAnchorIndex]+
                " · Grip to move, Undo/Delete available");
            return true;
        }

        void OnDestroy()
        {if(Workspace!=null)Workspace.SceneChanged-=OnSceneChanged;}
    }
}
