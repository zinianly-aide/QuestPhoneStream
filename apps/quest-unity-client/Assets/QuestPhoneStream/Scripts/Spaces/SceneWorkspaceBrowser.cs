using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream.Spaces
{
    /// <summary>First S0 world-space browser. No new XR controllers/input routes.</summary>
    public sealed class SceneWorkspaceBrowser:MonoBehaviour
    {
        Canvas _canvas;
        RectTransform _list;
        Text _status;
        SceneWorkspaceManager _spaces;
        Camera _eye;
        int _blankCounter;
        public void Show(SceneWorkspaceManager spaces,Camera eye)
        {
            _spaces=spaces;_eye=eye;
            if(!_canvas)Build();
            Position();Refresh();_canvas.gameObject.SetActive(true);
        }
        public void Hide()
        {
            if(_canvas)_canvas.gameObject.SetActive(false);
            var home=FindObjectOfType<QuestHomeUI>();
            if(home!=null)home.Show();
        }
        void Position()
        {
            Vector3 heading=Vector3.ProjectOnPlane(_eye.transform.forward,Vector3.up).normalized;
            if(heading.sqrMagnitude<.01f)heading=Vector3.forward;
            _canvas.transform.position=_eye.transform.position+heading*1.6f;
            _canvas.transform.rotation=Quaternion.LookRotation(heading,Vector3.up);
        }
        static void Rect(RectTransform rect,float x,float y,float w,float h)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);
            rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);
            rect.sizeDelta=new Vector2(w,h);
        }
        static Text Label(Transform root,string value,int size,Vector2 pos,Vector2 dimensions)
        {
            var go=new GameObject("Text_"+value,typeof(RectTransform));go.transform.SetParent(root,false);
            var text=go.AddComponent<Text>();text.text=value;text.font=Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize=size;text.color=new Color(.91f,.94f,.98f);
            text.alignment=TextAnchor.MiddleLeft;
            Rect(go.GetComponent<RectTransform>(),pos.x,pos.y,dimensions.x,dimensions.y);
            return text;
        }
        static Button Action(Transform root,string title,float x,float y,float width,Action onClick)
        {
            var go=new GameObject("Action_"+title,typeof(RectTransform));go.transform.SetParent(root,false);
            var rect=go.GetComponent<RectTransform>();Rect(rect,x,y,width,56);
            var img=go.AddComponent<Image>();img.color=new Color(.18f,.27f,.36f,.94f);
            var button=go.AddComponent<Button>();button.targetGraphic=img;
            button.onClick.AddListener(()=>onClick());
            Label(go.transform,title,21,new Vector2(12,0),new Vector2(width-18,56));
            return button;
        }
        void Build()
        {
            var root=new GameObject("SceneWorkspaceCanvas",typeof(RectTransform));
            root.transform.SetParent(transform,false);
            _canvas=root.AddComponent<Canvas>();_canvas.renderMode=RenderMode.WorldSpace;
            _canvas.worldCamera=_eye;_canvas.sortingOrder=105;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(880,670);
            root.GetComponent<RectTransform>().localScale=Vector3.one*.0015f;
            var bg=root.AddComponent<Image>();bg.color=new Color(.045f,.06f,.09f,.97f);
            Label(root.transform,"Spaces · Scene Workspace",32,new Vector2(30,20),new Vector2(650,66));
            Action(root.transform,"Close",710,24,135,Hide);
            _status=Label(root.transform,"",18,new Vector2(34,90),new Vector2(810,60));
            var scroll=new GameObject("SceneList",typeof(RectTransform));
            scroll.transform.SetParent(root.transform,false);
            _list=scroll.GetComponent<RectTransform>();Rect(_list,34,163,810,335);
            Action(root.transform,"New blank",34,521,179,()=>{
                var added=_spaces.CreateBlank("New Studio "+(++_blankCounter));
                if(added!=null)_spaces.Select(added.id);
                Refresh();});
            Action(root.transform,"Add cube",225,521,179,()=>{
                if(_spaces.AddPrimitive("cube",new Vector3(0,1.4f,1f)))Refresh();});
            Action(root.transform,"Undo",416,521,130,()=>{_spaces.Undo();Refresh();});
            Action(root.transform,"Copy scene",559,521,168,()=>{
                if(_spaces.Active!=null)_spaces.Duplicate(_spaces.Active.id);
                Refresh();});
            Label(root.transform,"Local private storage · HefuVR needs a separately installed private bundle",
                18,new Vector2(34,595),new Vector2(830,54));
        }
        public void Refresh()
        {
            if(!_list || _spaces==null || _spaces.Catalog==null)return;
            _status.text=(_spaces.Active==null?"No active scene":"Active: "+_spaces.Active.displayName)+
                (string.IsNullOrWhiteSpace(_spaces.LastError)?"":"   ·   "+_spaces.LastError);
            for(int i=_list.childCount-1;i>=0;i--)
            {
                var old=_list.GetChild(i).gameObject;
                if(Application.isPlaying)Destroy(old);else DestroyImmediate(old);
            }
            int number=0;
            foreach(var entry in _spaces.Catalog.scenes)
            {
                if(entry.isDeleted)continue;
                var scene=entry;
                float y=number*69;
                if(y>=335)break;
                string marker=_spaces.Active!=null&&_spaces.Active.id==scene.id?"● ":"○ ";
                Action(_list,marker+scene.displayName,2,y,558,()=>{
                    if(!_spaces.Select(scene.id))Refresh();
                    else Refresh();});
                if(_spaces.Active==null || _spaces.Active.id!=scene.id)
                    Action(_list,"Delete",575,y,115,()=>{_spaces.DeleteScene(scene.id);Refresh();});
                number++;
            }
        }
    }
}
