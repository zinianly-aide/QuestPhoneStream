using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace HefuVR
{
    public sealed class HefuMobileViewer : MonoBehaviour
    {
        public Camera cam;
        public Transform house;
        public HefuDecorSwitcher decor;
        public GameObject diningSideboard;
        public HefuEntryDoor entryDoor;
        public HefuEntryDoor[] interiorDoors;
        public HefuSlidingDoor balconySlidingDoor;
        int chosenDoor=0;
        public HefuPhotoComparison photo;
        int selectedView=0;
        public float distance=17f, yaw=22f, pitch=58f;
        Vector3 focus;
        bool edit,drag;
        HefuMovable selected;
        Vector3 offset;
        float oldPinch;
        GUIStyle label,button;
        string layoutMessage = "Not saved";
        bool initialized;
        HefuGenericScenePreview genericPreview;
        bool birthdayEnabled;
        HefuBirthdayScene birthdayScene;
        public HefuM2DailyDecor dailyDecor;
        public HefuM3QualityController quality;
        const string BirthdayKey = "HefuVR_Birthday_B23_Enabled_v1";
        bool pendingReset;
        float resetExpiry;

        void Awake()
        {
            if(!cam)cam=GetComponent<Camera>();
            if(!photo)photo=GetComponent<HefuPhotoComparison>();
            FocusHome();
            HefuReturnToHost.Reset();
            Application.targetFrameRate=72;
            Screen.sleepTimeout=SleepTimeout.NeverSleep;
        }
        void Start()
        {
            LoadLayout();
            if(dailyDecor) dailyDecor.Initialize();
            if(quality) quality.Initialize();
            initialized = true;
            birthdayEnabled = PlayerPrefs.GetInt(BirthdayKey, 1) == 1;
            ApplyBirthdayScene();
            ApplyFixtureLightingMode();
            genericPreview=gameObject.AddComponent<HefuGenericScenePreview>();
            genericPreview.Initialize(this);
            ReportUnifiedAppReady();
        }

        static void ReportUnifiedAppReady()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Private Unity is optional and runs in :spaces, but Android package
            // preferences are shared by the main sender process and the renderer.
            if(Application.identifier != "com.questphonestream.agent") return;
            try
            {
                using(var cls=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=cls.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var intent=new AndroidJavaObject("android.content.Intent"))
                {
                    string pkg=activity.Call<string>("getPackageName");
                    intent.Call<AndroidJavaObject>("setClassName",pkg,
                        "com.questphonestream.agent.SpacesReadyReceiver");
                    intent.Call<AndroidJavaObject>("setAction",
                        "com.questphonestream.agent.SPACES_READY");
                    activity.Call("sendBroadcast",intent);
                }
                Debug.Log("[QPS3D] Unity Hefu Home initialized; broadcast readiness sent");
            }
            catch(Exception error)
            {
                Debug.LogWarning("[QPS3D] startup marker unavailable: "+error.GetType().Name);
            }
#endif
        }

        public void SetGenericPreviewFocus(Vector3 next) { focus=next; }

        HefuMovable[] Furniture()
        {
            return house ? house.GetComponentsInChildren<HefuMovable>(true) : new HefuMovable[0];
        }

        HefuLayoutState CaptureLayout()
        {
            var data = new HefuLayoutState();
            data.stylePreset = decor ? decor.preset : 0;
            data.sideboardVisible = !diningSideboard || diningSideboard.activeSelf;
            data.yaw = yaw;
            data.pitch = Mathf.Clamp(pitch, 15f, 82f);
            data.distance = Mathf.Clamp(distance, 1.2f, 32f);
            data.focus = focus;
            foreach (var movable in Furniture())
            {
                data.furniture.Add(new HefuFurnitureState {
                    id = movable.gameObject.name,
                    position = movable.transform.position,
                    rotation = movable.transform.rotation,
                    localScale = movable.transform.localScale
                });
            }
            return data;
        }

        void ApplyBirthdayScene()
        {
            if (birthdayScene)
            {
                birthdayScene.ClearDecorations();
                Destroy(birthdayScene.gameObject);
                birthdayScene = null;
            }
            if (!birthdayEnabled || !house) return;
            try { birthdayScene = HefuBirthdayScene.Build(house); }
            catch (Exception ex)
            {
                Debug.LogWarning("[HefuPhone] birthday display unavailable: " + ex.Message);
                layoutMessage = "Birthday decoration unavailable - home kept";
            }
        }

        void ApplyFixtureLightingMode()
        {
            var fixture=GetComponent<HefuFixtureLighting>();
            if(fixture)fixture.ApplyMode(birthdayEnabled && birthdayScene &&
                birthdayScene.ThemeMode==HefuBirthdayScene.BirthdayThemeMode.Night);
        }

        void ToggleBirthdayTheme()
        {
            if (!birthdayEnabled || !birthdayScene) return;
            birthdayScene.ToggleTheme();
            ApplyFixtureLightingMode();
            PlayerPrefs.SetInt("Hefu_BirthdayThemeMode_v1",
                birthdayScene.ThemeMode==HefuBirthdayScene.BirthdayThemeMode.Night?1:0);
            PlayerPrefs.Save();
            layoutMessage="Birthday ambience: "+birthdayScene.ThemeMode;
        }

        void ToggleBirthdayScene()
        {
            birthdayEnabled = !birthdayEnabled;
            PlayerPrefs.SetInt(BirthdayKey,birthdayEnabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyBirthdayScene();
            ApplyFixtureLightingMode();
            layoutMessage = birthdayEnabled ? "Birthday B3.0 visible" : "Birthday hidden; home preserved";
        }

        void SaveLayout()
        {
            string message;
            HefuLayoutPersistence.Save(CaptureLayout(), out message);
            layoutMessage = message;
        }

        void LoadLayout()
        {
            HefuLayoutState data;
            string message;
            if (!HefuLayoutPersistence.Load(out data, out message))
            {
                layoutMessage = message;
                return;
            }
            // Reject unrelated or duplicate furniture ids, but preserve new furniture
            // introduced in a later minor app update.
            var states = new Dictionary<string, HefuFurnitureState>();
            foreach (var item in data.furniture) states.Add(item.id, item);
            foreach (var movable in Furniture())
            {
                HefuFurnitureState item;
                if (!states.TryGetValue(movable.gameObject.name, out item)) continue;
                Vector3 position = item.position;
                // v0.3.8 migrated the original estimated master bed 22cm WEST,
                // away from the new east-wall fitted wardrobe. Preserve deliberate
                // user moves, but migrate only the exact old default placement.
                if(movable.name=="Move_Master_Bed")
                {
                    // Migrate ONLY earlier generated bed defaults; preserve users' own
                    // manually edited positions and rotations.
                    bool legacyEastWest=Quaternion.Angle(item.rotation,Quaternion.identity)<1f;
                    bool legacyPosition=
                        Vector3.Distance(position,new Vector3(-3.0054f,0f,-6.75669f))<.05f ||
                        Vector3.Distance(position,new Vector3(-3.2254f,0f,-6.75669f))<.05f;
                    if(legacyPosition && legacyEastWest)
                    {
                        position=HefuMasterBedroomLayout.BedPosition;
                        movable.transform.SetPositionAndRotation(position,HefuMasterBedroomLayout.BedRotation);
                        movable.transform.localScale=item.localScale;
                        continue;
                    }
                }
                movable.transform.SetPositionAndRotation(position, item.rotation);
                movable.transform.localScale = item.localScale;
            }
            if (diningSideboard) diningSideboard.SetActive(data.sideboardVisible);
            if (decor) { decor.preset = data.stylePreset; decor.Apply(); }
            yaw = data.yaw;
            pitch = data.pitch;
            distance = data.distance;
            focus = data.focus;
            drag = false;
            selected = null;
            layoutMessage = message;
            if (initialized && birthdayEnabled) ApplyBirthdayScene();
        }

        void ResetAll()
        {
            foreach (var movable in Furniture()) movable.ResetLayout();
            if (diningSideboard) diningSideboard.SetActive(true);
            if (decor) { decor.preset = 0; decor.Apply(); }
            FocusHome();
            distance = 17f;
            yaw = 22f;
            pitch = 58f;
            selected = null;
            drag = false;
            edit = false;
            pendingReset = false;
            layoutMessage = "Default layout restored; tap Save to keep";
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && initialized && !(genericPreview && genericPreview.HasPreview)) SaveLayout();
        }

        void OnApplicationQuit()
        {
            if (initialized && !(genericPreview && genericPreview.HasPreview)) SaveLayout();
        }

        public void SelectPreset(int index)
        {
            selectedView=(index+HefuPhotoViewpoints.All.Length)%HefuPhotoViewpoints.All.Length;
            HefuPhotoViewpoints.OrbitFor(selectedView,out focus,out yaw,out pitch,out distance);
            drag=false;oldPinch=0;selected=null;edit=false;
            if(photo)photo.OnPresetChanged(selectedView);
        }

        void FocusHome()
        {
            Bounds b=new Bounds(Vector3.zero,Vector3.one);bool first=true;
            if(house)foreach(Renderer r in house.GetComponentsInChildren<Renderer>())
            {
                if(r.name.StartsWith("APPROX_"))continue;
                if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);
            }
            focus=first?new Vector3(-6,0,-4):new Vector3(b.center.x,0,b.center.z);
        }
        void LateUpdate()
        {
            // Unity maps the Android system Back action to Escape on many devices.
            if (Input.GetKeyDown(KeyCode.Escape)) { HefuReturnToHost.Back(); return; }
            TouchInput();
            pitch=Mathf.Clamp(pitch,15,82);
            distance=Mathf.Clamp(distance,1.2f,32);
            var q=Quaternion.Euler(pitch,yaw,0);
            cam.transform.SetPositionAndRotation(focus+q*Vector3.back*distance,q);
        }
        void TouchInput()
        {
            int n=Input.touchCount;
            if(n==0){drag=false;oldPinch=0;return;}
            Touch a=Input.GetTouch(0);
            if(n>=2)
            {
                drag=false;
                Touch b=Input.GetTouch(1);
                float pinch=Vector2.Distance(a.position,b.position);
                if(oldPinch>1)distance*=Mathf.Clamp(oldPinch/Mathf.Max(pinch,1),.75f,1.3f);
                oldPinch=pinch;
                Vector2 delta=(a.deltaPosition+b.deltaPosition)*.5f;
                Vector3 right=cam.transform.right;
                Vector3 forward=Vector3.Cross(right,Vector3.up).normalized;
                focus-=(right*delta.x+forward*delta.y)*(distance/Mathf.Max(500,Screen.height)*1.6f);
                return;
            }
            oldPinch=0;
            // GUI uses top-left, touch uses bottom-left. Reserve both toolbar rows.
            float toolbarHeight = Mathf.Max(65f, Screen.height * .12f) + 195f;
            if(a.position.y < toolbarHeight){drag=false;return;}
            float touchTop=Screen.height-a.position.y;
            if(a.position.x>Screen.width-210 && touchTop<260f) { drag=false;return; }
            if(photo && photo.comparing && a.position.x>Screen.width*.52f &&
               touchTop>=215f && touchTop<Screen.height-toolbarHeight-35f)
            { drag=false;return; }
            Ray ray=cam.ScreenPointToRay(a.position);
            if(a.phase==TouchPhase.Began)
            {
                drag=false;
                if(edit)
                {
                    var hits=Physics.RaycastAll(ray,100);
                    Array.Sort(hits,(x,y)=>x.distance.CompareTo(y.distance));
                    selected=null;
                    foreach(var hit in hits)
                    {
                        var m=hit.collider.GetComponentInParent<HefuMovable>();
                        if(m){selected=m;break;}
                    }
                    if(selected)
                    {
                        var plane=new Plane(Vector3.up,selected.transform.position);
                        if(plane.Raycast(ray,out float t)){offset=selected.transform.position-ray.GetPoint(t);drag=true;}
                    }
                }
                return;
            }
            if(a.phase==TouchPhase.Ended || a.phase==TouchPhase.Canceled){drag=false;return;}
            if(a.phase!=TouchPhase.Moved)return;
            if(edit && drag && selected)
            {
                var plane=new Plane(Vector3.up,selected.transform.position);
                if(plane.Raycast(ray,out float t))
                {
                    Vector3 pos=ray.GetPoint(t)+offset;
                    selected.transform.position=new Vector3(pos.x,selected.transform.position.y,pos.z);
                }
            }
            else
            {
                yaw+=a.deltaPosition.x*.16f;
                pitch-=a.deltaPosition.y*.12f;
            }
        }
        void OnGUI()
        {
            if(label==null)
            {
                label=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold};
                label.normal.textColor=Color.white;
                button=new GUIStyle(GUI.skin.button){fontSize=19,fontStyle=FontStyle.Bold};
            }
            float w=Screen.width,h=Screen.height;
            GUI.Box(new Rect(10,8,Mathf.Min(w-20,665),74),GUIContent.none);
            GUI.Label(new Rect(20,13,640,40),"HEFU | HOME DESIGN",label);
            GUI.Label(new Rect(20,44,650,29),"N: Entry / Fridge / Cabinet  -  Dining > Living > Balcony: S");
            // The phone's system gesture is not reliably delivered by Unity on
            // Android 16; a visible exit is always available at the top-right.
            if (GUI.Button(new Rect(Mathf.Max(10f,w-195f),10f,185f,60f),
                Application.identifier=="com.questphonestream.agent" ? "Back to App" : "Exit 3D", button))
                HefuReturnToHost.Back();
            float gap=7,bh=Mathf.Max(65,h*.12f),bw=(w-31-5*gap)/6;
            float y=h-bh-10;
            if(GUI.Button(new Rect(10,y,bw,bh),edit?"Move ON":"Explore",button)){edit=!edit;drag=false;}
            if(GUI.Button(new Rect(10+(bw+gap),y,bw,bh),"Style",button) && decor)decor.Next();
            if(GUI.Button(new Rect(10+2*(bw+gap),y,bw,bh),"Rotate",button) && selected)selected.Rotate(15);
            if(GUI.Button(new Rect(10+3*(bw+gap),y,bw,bh),"Reset",button))
            {
                if(edit && selected)selected.ResetLayout();
                else {FocusHome();distance=17;yaw=22;pitch=58;}
            }
            // The CAD north is -source-Y, corresponding to Unity +Z.
            // Reset yaw as well as pitch so North is at the top of the plan.
            if(GUI.Button(new Rect(10+4*(bw+gap),y,bw,bh),"Top N",button))
            {
                SelectPreset(0);
                layoutMessage="North is up | Entry N - Balcony S";
            }
            if(diningSideboard && GUI.Button(new Rect(10+5*(bw+gap),y,bw,bh),
                diningSideboard.activeSelf?"Cabinet ON":"Cabinet OFF",button))
            {
                if(selected && selected.gameObject==diningSideboard) selected=null;
                diningSideboard.SetActive(!diningSideboard.activeSelf);
            }
            // Birthday is a separate, non-destructive visual overlay, not furniture layout.
            // Its preference is saved independently from the existing v3 CAD layout.
            float toolsY = y - 61f;
            float toolWidth = Mathf.Max(80, Mathf.Min(170f, (w - 34f) / 4f));
            if(GUI.Button(new Rect(10,toolsY,toolWidth,48),"Save",button))
            {
                pendingReset = false;
                SaveLayout();
            }
            if(GUI.Button(new Rect(17+toolWidth,toolsY,toolWidth,48),"Load",button))
            {
                pendingReset = false;
                LoadLayout();
            }
            if(pendingReset && Time.unscaledTime > resetExpiry) pendingReset=false;
            if(GUI.Button(new Rect(24+toolWidth*2,toolsY,toolWidth,48),
                pendingReset?"Confirm reset":"Reset all",button))
            {
                if(pendingReset && Time.unscaledTime<=resetExpiry) ResetAll();
                else {pendingReset=true;resetExpiry=Time.unscaledTime+5f;layoutMessage="Tap Confirm reset within 5s";}
            }
            float right=w-185f;
            if(right>31+toolWidth*3 && GUI.Button(new Rect(right,toolsY,175f,48f),"Cabinets",button))
            {
                SaveLayout();
                SceneManager.LoadScene("Hefu_MakerCabinetGallery");
            }
            HefuEntryDoor currentDoor=chosenDoor==0?entryDoor:
                (interiorDoors!=null && chosenDoor-1<interiorDoors.Length?interiorDoors[chosenDoor-1]:null);
            bool isSlider=chosenDoor==1+(interiorDoors!=null?interiorDoors.Length:0);
            if(currentDoor && GUI.Button(new Rect(right,96f,175f,48f),
                currentDoor.isOpen?"Close door":"Open door",button))currentDoor.Toggle();
            if(isSlider && balconySlidingDoor && GUI.Button(new Rect(right,96f,175f,48f),
                balconySlidingDoor.isOpen?"Close slider":"Open slider",button))balconySlidingDoor.Toggle();
            if(photo && GUI.Button(new Rect(right,151f,175f,48f),"Import photo",button))
                photo.ImportReference(selectedView);
            string doorLabel=isSlider?"Balcony slider":
                (currentDoor?currentDoor.name.Replace("Door_",""):"Entry");
            if(GUI.Button(new Rect(right,206f,175f,48f),"Door: "+doorLabel,button))
                chosenDoor=(chosenDoor+1)%(2+(interiorDoors!=null?interiorDoors.Length:0));
            GUI.Label(new Rect(31+toolWidth*3,toolsY+7,Mathf.Max(70,right-(41+toolWidth*3)),39),layoutMessage);
            // A third toolbar row for repeatable CAD/photo viewpoints.
            float viewY=toolsY-58f;
            float viewW=(w-20)/5f;
            if(GUI.Button(new Rect(10f,viewY,viewW,50f),"Prev view",button))
                SelectPreset(selectedView-1);
            if(GUI.Button(new Rect(10f+viewW,viewY,viewW,50f),
                HefuPhotoViewpoints.All[selectedView].id,button))
                SelectPreset(selectedView);
            if(GUI.Button(new Rect(10f+viewW*2,viewY,viewW,50f),"Next view",button))
                SelectPreset(selectedView+1);
            if(photo && GUI.Button(new Rect(10f+viewW*3,viewY,viewW,50f),"Shot",button))
                photo.Capture(selectedView);
            if(photo && GUI.Button(new Rect(10f+viewW*4,viewY,viewW,50f),
                photo.comparing?"Hide photo":"Compare",button))
                photo.ToggleComparison(selectedView);
            if(quality){
                float qWidth=Mathf.Min(165f,Mathf.Max(100f,w*.17f));
                float qY=viewY-84f;
                if(GUI.Button(new Rect(w-2*qWidth-17f,qY,qWidth,38f),
                    "Quality: "+quality.CurrentName,button)){
                    quality.Next();layoutMessage="Quality: "+quality.CurrentName;
                }
                if(GUI.Button(new Rect(w-qWidth-10f,qY,qWidth,38f),
                    quality.showPerformance?"Hide FPS":"Show FPS",button))
                    quality.TogglePerformance();
                if(quality.showPerformance)
                    GUI.Label(new Rect(14f,qY-35f,Mathf.Min(480f,w*.5f),33f),
                        quality.PerformanceLabel,label);
            }
            float birthdayWidth = Mathf.Min(195f, Mathf.Max(105f,w*.23f));
            // Daily accents and Birthday are two independent non-layout switches.
            if(dailyDecor && GUI.Button(new Rect(w-birthdayWidth*3-30f,viewY-40f,birthdayWidth,38f),
                dailyDecor.Visible?"Daily decor ON":"Daily decor OFF",button)){
                dailyDecor.Toggle();
                layoutMessage=dailyDecor.Visible?"Daily soft furnishing visible":"Daily soft furnishing hidden";
            }
            if(GUI.Button(new Rect(w-birthdayWidth-10f,viewY-40f,birthdayWidth,38f),
                birthdayEnabled ? "Birthday ON" : "Birthday OFF",button))
                ToggleBirthdayScene();
            if(GUI.Button(new Rect(w-birthdayWidth*2-20f,viewY-40f,birthdayWidth,38f),
                birthdayScene && birthdayScene.ThemeMode==HefuBirthdayScene.BirthdayThemeMode.Night ?
                    "Night ambience" : "Day ambience",button)) ToggleBirthdayTheme();
            GUI.Box(new Rect(10,viewY-40f,Mathf.Max(100,w-birthdayWidth*3-52f),37f),GUIContent.none);
            GUI.Label(new Rect(20,viewY-34f,Mathf.Max(75,Mathf.Min(w-birthdayWidth*3-72f,700)),31),
                photo?photo.status:"Drag to orbit / pinch to zoom.");
            if(photo && photo.comparing)
                photo.DrawOverlay(selectedView,
                    new Rect(w*.52f,215f,w*.47f,Mathf.Max(130f,viewY-268f)));
            if(selected)
            {
                GUI.Box(new Rect(Mathf.Max(10,w-360),8,340,65),GUIContent.none);
                GUI.Label(new Rect(Mathf.Max(20,w-350),24,320,35),selected.description,label);
            }
        }
    }
}
