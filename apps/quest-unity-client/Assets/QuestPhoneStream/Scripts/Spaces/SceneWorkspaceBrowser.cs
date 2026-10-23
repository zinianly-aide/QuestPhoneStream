using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream.Spaces
{
    /// <summary>World-space scene browser and persistent left/right-stick control menu.</summary>
    public sealed class SceneWorkspaceBrowser : MonoBehaviour
    {
        Canvas _canvas;
        GameObject _scenePage, _controlsPage;
        RectTransform _list;
        Text _status;
        Button _leftButton, _rightButton, _speedButton, _snapButton, _heightButton;
        SceneWorkspaceManager _spaces;
        SceneWorkspaceLocomotion _movement;
        Camera _eye;
        int _blankCounter;
        public bool IsVisible => _canvas != null && _canvas.gameObject.activeInHierarchy;

        public void Show(SceneWorkspaceManager spaces, Camera eye)
        {
            _spaces = spaces; _eye = eye;
            _movement = spaces.GetComponent<SceneWorkspaceLocomotion>();
            if (!_canvas) Build();
            ShowScenePage();
            Position(); Refresh(); _canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_canvas) _canvas.gameObject.SetActive(false);
            var home = FindObjectOfType<QuestHomeUI>();
            if (home) home.Show();
        }

        public void Explore()
        {
            // No Home overlay: the left/right sticks can now control room locomotion.
            if (_canvas) _canvas.gameObject.SetActive(false);
        }

        void Position()
        {
            Vector3 heading = Vector3.ProjectOnPlane(_eye.transform.forward, Vector3.up).normalized;
            if (heading.sqrMagnitude < .01f) heading = Vector3.forward;
            _canvas.transform.position = _eye.transform.position + heading * 1.6f;
            _canvas.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        }

        static void Rect(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        internal static Text Label(Transform root, string value, int size, Vector2 pos, Vector2 dimensions)
        {
            var go = new GameObject("Text_" + value, typeof(RectTransform));
            go.transform.SetParent(root, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = new Color(.91f, .94f, .98f);
            text.alignment = TextAnchor.MiddleLeft;
            Rect(go.GetComponent<RectTransform>(), pos.x, pos.y, dimensions.x, dimensions.y);
            return text;
        }

        internal static Button Action(Transform root, string title, float x, float y, float width, Action onClick)
        {
            var go = new GameObject("Action_" + title, typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rect = go.GetComponent<RectTransform>();
            Rect(rect, x, y, width, 56);
            var img = go.AddComponent<Image>();
            img.color = new Color(.18f, .27f, .36f, .94f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            Label(go.transform, title, 21, new Vector2(12, 0), new Vector2(width - 18, 56));
            return button;
        }

        static GameObject Page(Transform canvas, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(canvas, false);
            Rect(page.GetComponent<RectTransform>(), 0, 0, 880, 780);
            return page;
        }

        void Build()
        {
            var root = new GameObject("SceneWorkspaceCanvas", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _eye;
            _canvas.sortingOrder = 105;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(880, 780);
            root.GetComponent<RectTransform>().localScale = Vector3.one * .0015f;
            var bg = root.AddComponent<Image>();
            bg.color = new Color(.045f, .06f, .09f, .97f);

            _scenePage = Page(root.transform, "ScenePage");
            Label(_scenePage.transform, "Spaces · Scene Workspace", 32, new Vector2(30, 20), new Vector2(650, 66));
            Action(_scenePage.transform, "Home", 710, 24, 135, Hide);
            _status = Label(_scenePage.transform, "", 18, new Vector2(34, 95), new Vector2(810, 60));
            var scroll = new GameObject("SceneList", typeof(RectTransform));
            scroll.transform.SetParent(_scenePage.transform, false);
            _list = scroll.GetComponent<RectTransform>();
            Rect(_list, 34, 165, 810, 310);
            Action(_scenePage.transform, "New blank", 34, 495, 175, () => {
                var added = _spaces.CreateBlank("New Studio " + (++_blankCounter));
                if (added != null) _spaces.Select(added.id);
                Refresh();
            });
            Action(_scenePage.transform, "Add cube", 222, 495, 176, () => {
                if (_spaces.AddPrimitive("cube", new Vector3(0, 1.4f, 1f))) Refresh();
            });
            Action(_scenePage.transform, "Edit room", 411, 495, 132, () => {
                _canvas.gameObject.SetActive(false);
                var editor = _spaces.GetComponent<SceneWorkspaceManipulator>();
                if (!editor) editor = _spaces.gameObject.AddComponent<SceneWorkspaceManipulator>();
                editor.Initialize(_spaces);
                editor.Enter(_eye);
            });
            Action(_scenePage.transform, "Copy scene", 556, 495, 170, () => {
                if (_spaces.Active != null) _spaces.Duplicate(_spaces.Active.id);
                Refresh();
            });
            Action(_scenePage.transform, "Stick controls", 34, 565, 246, ShowControls);
            Action(_scenePage.transform, "Reset position", 293, 565, 246, () => { _movement?.ResetToSceneStart(); Position(); });
            Action(_scenePage.transform, "Explore room", 552, 565, 272, Explore);
            Action(_scenePage.transform, "Birthday scene", 34, 634, 246, () => {
                _spaces.SelectBirthdayScene();
                Refresh();
            });
            Action(_scenePage.transform, "Original HefuVR", 293, 634, 246, () => {
                _spaces.SelectOriginalHefu();
                Refresh();
            });
            Action(_scenePage.transform, "Celebrate lights", 552, 634, 272, () => {
                var birthday = _spaces.EnvironmentRoot != null ?
                    _spaces.EnvironmentRoot.GetComponentInChildren<SceneWorkspaceBirthday>() : null;
                if (birthday) birthday.ToggleLights();
                Refresh();
            });
            Label(_scenePage.transform, "North entrance → Dining → South living room. Private CAD stays local.",
                18, new Vector2(34, 707), new Vector2(815, 42));

            _controlsPage = Page(root.transform, "StickControlsPage");
            Label(_controlsPage.transform, "Spaces · Stick Controls", 32, new Vector2(30, 20), new Vector2(660, 66));
            Action(_controlsPage.transform, "Back", 710, 24, 135, ShowScenePage);
            Label(_controlsPage.transform, "Tap a control to cycle its setting. Saved automatically.",
                21, new Vector2(34, 91), new Vector2(800, 48));
            _leftButton = Action(_controlsPage.transform, "Left stick", 34, 156, 780, () => {
                if (_movement == null) return;
                var next = (SceneWorkspaceLocomotion.StickMode)(((int)_movement.LeftMode + 1) % 4);
                _movement.Configure(true, next); RefreshControls();
            });
            _rightButton = Action(_controlsPage.transform, "Right stick", 34, 235, 780, () => {
                if (_movement == null) return;
                var next = (SceneWorkspaceLocomotion.StickMode)(((int)_movement.RightMode + 1) % 4);
                _movement.Configure(false, next); RefreshControls();
            });
            _speedButton = Action(_controlsPage.transform, "Speed", 34, 314, 375, () => {
                if (_movement == null) return;
                float next = _movement.MoveSpeed < .8f ? 1.2f : _movement.MoveSpeed < 1.5f ? 1.8f : .6f;
                _movement.ConfigureSpeed(next); RefreshControls();
            });
            _snapButton = Action(_controlsPage.transform, "Snap degrees", 422, 314, 392, () => {
                if (_movement == null) return;
                _movement.ConfigureSnap(_movement.SnapDegrees == 30 ? 45 : 30); RefreshControls();
            });
            _heightButton = Action(_controlsPage.transform, "Eye height calibration", 34, 390, 780, () => {
                var rig = FindObjectOfType<QuestXrUiRig>();
                if (!rig) return;
                var values = new [] { 0f, -.05f, -.10f, -.15f, .05f, .10f };
                int current = 0;
                for (int i = 0; i < values.Length; i++)
                    if (Mathf.Abs(rig.EyeTrimMeters - values[i]) < .001f) { current = i; break; }
                rig.SetEyeTrim(values[(current + 1) % values.Length]);
                RefreshControls();
            });
            Label(_controlsPage.transform, "Floor tracking preferred; tap Eye height to calibrate 5cm at a time.",
                18, new Vector2(34, 455), new Vector2(815, 45));
            Label(_controlsPage.transform, "Modes: Off / Move / Snap turn / Smooth turn",
                20, new Vector2(34, 509), new Vector2(810, 42));
            Label(_controlsPage.transform, "Mac screen: right stick scroll has priority when aimed at screen.",
                18, new Vector2(34, 555), new Vector2(815, 48));
            Label(_controlsPage.transform, "Movement is paused during menus or furniture editing.",
                18, new Vector2(34, 609), new Vector2(810, 36));
            Action(_controlsPage.transform, "Back to scenes", 34, 682, 342, ShowScenePage);
            Action(_controlsPage.transform, "Explore room", 398, 682, 416, Explore);
            _controlsPage.SetActive(false);
        }

        void ShowScenePage()
        {
            if (_controlsPage) _controlsPage.SetActive(false);
            if (_scenePage) _scenePage.SetActive(true);
            Refresh();
        }

        void ShowControls()
        {
            _movement = _spaces.GetComponent<SceneWorkspaceLocomotion>();
            _scenePage.SetActive(false);
            _controlsPage.SetActive(true);
            RefreshControls();
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
        }

        void RefreshControls()
        {
            if (!_movement) return;
            SetButtonLabel(_leftButton, "Left stick: " + _movement.LeftMode);
            SetButtonLabel(_rightButton, "Right stick: " + _movement.RightMode);
            SetButtonLabel(_speedButton, "Move speed: " + _movement.MoveSpeed.ToString("0.0") + " m/s");
            SetButtonLabel(_snapButton, "Snap angle: " + _movement.SnapDegrees + " degrees");
            var rig = FindObjectOfType<QuestXrUiRig>();
            if (rig)
            {
                float eye = rig.Origin != null && rig.Origin.Camera != null ?
                    rig.Origin.Camera.transform.position.y : 0f;
                SetButtonLabel(_heightButton, "Eye adjustment: " +
                    (rig.EyeTrimMeters * 100f).ToString("+0;-0;0") +
                    " cm  |  Eye Y: " + eye.ToString("F2") + " m");
            }
        }

        public void Refresh()
        {
            if (!_list || _spaces == null || _spaces.Catalog == null) return;
            if (IsVisible && _eye) Position();
            _status.text = (_spaces.Active == null ? "No active scene" : "Active: " + _spaces.Active.displayName)
                + (string.IsNullOrWhiteSpace(_spaces.LastError) ? "" : " · " + _spaces.LastError);
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                var old = _list.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            int number = 0;
            foreach (var entry in _spaces.Catalog.scenes)
            {
                if (entry.isDeleted) continue;
                var scene = entry;
                float y = number * 69;
                if (y >= 310) break;
                string marker = _spaces.Active != null && _spaces.Active.id == scene.id ? "● " : "○ ";
                Action(_list, marker + scene.displayName, 2, y, 558, () => {
                    _spaces.Select(scene.id);
                    Refresh();
                });
                if (_spaces.Active == null || _spaces.Active.id != scene.id)
                    Action(_list, "Delete", 575, y, 115, () => {
                        _spaces.DeleteScene(scene.id);
                        Refresh();
                    });
                number++;
            }
        }
    }
}
