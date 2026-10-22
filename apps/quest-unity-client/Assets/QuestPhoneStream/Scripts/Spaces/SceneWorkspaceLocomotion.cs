using UnityEngine;
using UnityEngine.XR;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// Local, opt-in room exploration. Never changes headset tracking or Quest Guardian.
    /// Stick input is used only while the workspace is being explored, and Mac screen
    /// scrolling has priority over right-stick turning.
    /// </summary>
    public sealed class SceneWorkspaceLocomotion : MonoBehaviour
    {
        public enum StickMode { Off, Move, SnapTurn, SmoothTurn }
        const string Prefix = "qps.space.locomotion.s2.";
        public StickMode LeftMode { get; private set; } = StickMode.Move;
        public StickMode RightMode { get; private set; } = StickMode.SnapTurn;
        public float MoveSpeed { get; private set; } = 1.2f;
        public int SnapDegrees { get; private set; } = 30;

        SceneWorkspaceManager _spaces;
        QuestXrUiRig _rig;
        CharacterController _character;
        PanelInputMapper _panel;
        bool _leftLatched, _rightLatched;
        bool _spawnPending;

        void Awake()
        {
            _spaces = GetComponent<SceneWorkspaceManager>();
            LeftMode = ReadMode("left", StickMode.Move);
            RightMode = ReadMode("right", StickMode.SnapTurn);
            MoveSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "speed", 1.2f), .5f, 2.0f);
            int degrees = PlayerPrefs.GetInt(Prefix + "snap", 30);
            SnapDegrees = degrees == 45 ? 45 : 30;
        }

        StickMode ReadMode(string key, StickMode fallback)
        {
            int value = PlayerPrefs.GetInt(Prefix + key, (int)fallback);
            return value >= 0 && value <= 3 ? (StickMode)value : fallback;
        }

        void OnEnable()
        {
            if (!_spaces) _spaces = GetComponent<SceneWorkspaceManager>();
            if (_spaces != null) _spaces.SceneChanged += HandleScene;
            _spawnPending = true;
        }

        void OnDisable()
        {
            if (_spaces != null) _spaces.SceneChanged -= HandleScene;
            _leftLatched = _rightLatched = false;
        }

        void HandleScene(SceneWorkspaceRecord _)
        {
            _spawnPending = true; _leftLatched = _rightLatched = false;
            // Scene can be switched while the browser is open: move its anchor before refreshing.
            ResetToSceneStart();
        }

        public void Configure(bool left, StickMode mode)
        {
            if (left) LeftMode = mode; else RightMode = mode;
            PlayerPrefs.SetInt(Prefix + (left ? "left" : "right"), (int)mode);
            PlayerPrefs.Save();
            _leftLatched = _rightLatched = false;
        }

        public void ConfigureSpeed(float value)
        {
            MoveSpeed = Mathf.Clamp(value, .5f, 2f);
            PlayerPrefs.SetFloat(Prefix + "speed", MoveSpeed);
            PlayerPrefs.Save();
        }

        public void ConfigureSnap(int value)
        {
            SnapDegrees = value == 45 ? 45 : 30;
            PlayerPrefs.SetInt(Prefix + "snap", SnapDegrees);
            PlayerPrefs.Save();
        }

        void EnsureRig()
        {
            if (!_rig) _rig = FindObjectOfType<QuestXrUiRig>();
            if (_rig == null || _rig.Origin == null) return;
            if (!_character)
            {
                _character = _rig.Origin.GetComponent<CharacterController>();
                if (!_character)
                    _character = _rig.Origin.gameObject.AddComponent<CharacterController>();
                _character.radius = .22f;
                _character.skinWidth = .035f;
                _character.stepOffset = .20f;
                _character.slopeLimit = 50f;
                _character.minMoveDistance = 0f;
            }
            if (!_panel) _panel = FindObjectOfType<PanelInputMapper>();
        }

        public bool CanExplore()
        {
            if (_spaces == null || _spaces.Active == null || _rig == null || _rig.Origin == null)
                return false;
            var editor = GetComponent<SceneWorkspaceManipulator>();
            if (editor && editor.Editing) return false;
            var browser = GetComponent<SceneWorkspaceBrowser>();
            if (browser && browser.IsVisible) return false;
            var home = FindObjectOfType<QuestHomeUI>();
            if (home && home.IsVisible) return false;
            var settings = FindObjectOfType<SettingsUI>();
            if (settings && settings.IsVisible) return false;
            return true;
        }

        public void ResetToSceneStart()
        {
            EnsureRig();
            if (_rig == null || _rig.Origin == null || _spaces?.Active == null) return;
            Vector3 target = Vector3.zero;
            // HefuCAD '00_' floor is the same model reference used by HefuVR's
            // original XR rig. For empty studios, use their centered origin.
            if (_spaces.Active.sourceType == "cad-bundle")
            {
                Renderer floor = null;
                foreach (var renderer in _spaces.EnvironmentRoot.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("00_")) { floor = renderer; break; }
                if (floor)
                    target = new Vector3(floor.bounds.center.x, floor.bounds.min.y, floor.bounds.center.z);
                else
                {
                    Debug.LogWarning("[Spaces] CAD floor missing; retaining camera starting position");
                    _spawnPending = false;
                    return;
                }
            }
            var origin = _rig.Origin.transform;
            var eye = _rig.Origin.Camera.transform;
            var delta = target - new Vector3(eye.position.x, 0f, eye.position.z);
            origin.position += new Vector3(delta.x, 0f, delta.z);
            // Do not edit XR camera tracking pose or override physical boundary.
            Debug.Log("[Spaces] exploration start, scene=" + _spaces.Active.displayName + " target=" + target);
            _spawnPending = false;
        }

        public static Vector2 Deadzone(Vector2 axis, float deadzone = .22f)
        {
            float length = axis.magnitude;
            if (length <= deadzone) return Vector2.zero;
            return axis / length * Mathf.Clamp01((length - deadzone) / (1f - deadzone));
        }

        void Update()
        {
            EnsureRig();
            if (_spaces == null || _rig == null || _rig.Origin == null) return;
            if (_spawnPending && _spaces.Active != null) ResetToSceneStart();
            if (!CanExplore()) { _leftLatched = _rightLatched = false; return; }

            var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Vector2 left = Vector2.zero, right = Vector2.zero;
            if (leftDevice.isValid) leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out left);
            if (rightDevice.isValid) rightDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out right);
            left = Deadzone(left); right = Deadzone(right);

            // Right stick scroll on a Mac surface wins over any room action.
            bool macOwnsRight = _panel != null && _panel.IsMacScrollTargeted();
            if (macOwnsRight) { right = Vector2.zero; _rightLatched = false; }

            float turn = 0f;
            Vector2 move = Vector2.zero;
            ApplyMode(LeftMode, left, ref _leftLatched, ref move, ref turn);
            ApplyMode(RightMode, right, ref _rightLatched, ref move, ref turn);
            if (Mathf.Abs(turn) > 0f)
                _rig.Origin.transform.RotateAround(_rig.Origin.Camera.transform.position, Vector3.up, turn);

            if (move.sqrMagnitude <= .0001f) return;
            var forward = Vector3.ProjectOnPlane(_rig.Origin.Camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var rightward = new Vector3(forward.z, 0f, -forward.x);
            var displacement = Vector3.ClampMagnitude(forward * move.y + rightward * move.x, 1f)
                * (MoveSpeed * Time.deltaTime);
            if (_character)
            {
                var localHead = _rig.Origin.transform.InverseTransformPoint(_rig.Origin.Camera.transform.position);
                float height = Mathf.Clamp(localHead.y, 1.2f, 2.1f);
                _character.height = height;
                _character.center = new Vector3(localHead.x, height * .5f, localHead.z);
                _character.Move(displacement);
            }
        }

        void ApplyMode(StickMode mode, Vector2 axis, ref bool latch, ref Vector2 move, ref float turn)
        {
            if (mode == StickMode.Move)
            {
                move += axis;
                latch = false;
            }
            else if (mode == StickMode.SnapTurn)
            {
                if (Mathf.Abs(axis.x) < .25f) latch = false;
                if (!latch && Mathf.Abs(axis.x) >= .7f)
                {
                    turn += Mathf.Sign(axis.x) * SnapDegrees;
                    latch = true;
                }
            }
            else if (mode == StickMode.SmoothTurn)
            {
                turn += axis.x * 45f * Time.deltaTime;
                latch = false;
            }
            else latch = false;
        }
    }
}
