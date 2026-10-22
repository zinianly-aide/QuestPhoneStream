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

        public void Initialize(SceneWorkspaceManager manager)
        {
            if(Workspace==manager)return;
            if(Workspace!=null)Workspace.SceneChanged-=OnSceneChanged;
            Workspace=manager;
            if(manager!=null)manager.SceneChanged+=OnSceneChanged;
        }
        void OnSceneChanged(SceneWorkspaceRecord _) {Selected=null;Dragging=false;SetStatus("Scene changed");}
        public void Enter(Camera camera)
        {
            if(Workspace?.Active==null || camera==null)return;
            _eye=camera;Editing=true;_pressed=false;
            if(!_toolbar)BuildToolbar();
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
            bool pressed=_grip.IsPressed();
            var pointer=new Ray(_right.position,_right.forward);
            if(pressed&&!_pressed)BeginGrab(pointer);
            if(pressed&&Dragging)Drag(pointer);
            if(!pressed&&_pressed&&Dragging)FinishGrab(true);
            _pressed=pressed;
        }
        // Only closest physical collider wins: phone/window/UI blocks furniture behind it.
        public bool TrySelect(Ray pointer)
        {
            if(!Editing || Workspace?.EnvironmentRoot==null)return false;
            if(!Physics.Raycast(pointer,out RaycastHit hit,maxReach,~0,QueryTriggerInteraction.Ignore))return false;
            var furniture=hit.collider.GetComponentInParent<SceneWorkspaceObject>();
            if(!furniture || !furniture.editable ||
               !furniture.transform.IsChildOf(Workspace.EnvironmentRoot))return false;
            Selected=furniture;SetStatus("Selected: "+furniture.name);return true;
        }
        public bool BeginGrab(Ray pointer)
        {
            if(!TrySelect(pointer))return false;
            if(!Physics.Raycast(pointer,out RaycastHit hit,maxReach,~0,QueryTriggerInteraction.Ignore))return false;
            var tr=Selected.transform;
            _depth=hit.distance;_startPoint=hit.point;
            _startWorld=tr.position;_startLocal=tr.localPosition;
            _startRotation=tr.localRotation;_startScale=tr.localScale;
            Collect(Selected,_existingHits);Dragging=true;return true;
        }
        public bool Drag(Ray pointer)
        {
            if(!Dragging||!Selected)return false;
            var next=_startWorld+(pointer.GetPoint(_depth)-_startPoint);
            next.y=_startWorld.y; // Retain original floor placement.
            if(Vector3.Distance(next,_startWorld)>maxMove)return false;
            var tr=Selected.transform;var previous=tr.position;
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
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
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
            Collect(Selected,_existingHits);
            tr.localRotation=Quaternion.Euler(0,degrees,0)*original;
            bool ok=!HasNewCollision()&&Commit();
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
            Collect(Selected,_existingHits);
            tr.localScale=next;
            bool ok=!HasNewCollision()&&Commit();
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
            rect.sizeDelta=new Vector2(650,230);
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
        }
        void OnDestroy()
        {if(Workspace!=null)Workspace.SceneChanged-=OnSceneChanged;}
    }
}
