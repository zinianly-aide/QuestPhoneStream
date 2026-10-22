using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuestPhoneStream.Spaces
{
    /// <summary>Restores door interactions to script-free private CAD bundles.</summary>
    public sealed class SceneWorkspaceDoors : MonoBehaviour
    {
        public sealed class Door : MonoBehaviour
        {
            public Transform movingPart;
            public bool sliding;
            public float openingAngle;
            public Vector3 slideOffset;
            public bool IsOpen { get; private set; }
            Quaternion _closedRotation;
            Vector3 _closedPosition;

            public void Configure(Transform part, float angle, Vector3 offset, bool isSlider)
            {
                movingPart = part;
                openingAngle = angle;
                slideOffset = offset;
                sliding = isSlider;
                _closedRotation = part.localRotation;
                _closedPosition = part.localPosition;
            }

            public bool Accepts(Collider collider)
            {
                return movingPart && collider && collider.transform.IsChildOf(movingPart);
            }

            public bool Toggle(Transform playerHead)
            {
                if (!movingPart) return false;
                if (IsOpen && playerHead &&
                    Vector3.Distance(
                        new Vector3(playerHead.position.x, 0, playerHead.position.z),
                        new Vector3(transform.position.x, 0, transform.position.z)) < .8f)
                    return false; // Don't close a door into the user's physical position.
                IsOpen = !IsOpen;
                return true;
            }

            void Update()
            {
                if (!movingPart) return;
                if (sliding)
                    movingPart.localPosition = Vector3.MoveTowards(
                        movingPart.localPosition, _closedPosition + (IsOpen ? slideOffset : Vector3.zero),
                        1.4f * Time.deltaTime);
                else
                    movingPart.localRotation = Quaternion.RotateTowards(
                        movingPart.localRotation, _closedRotation *
                        Quaternion.Euler(0, IsOpen ? openingAngle : 0, 0),
                        180f * Time.deltaTime);
            }
        }

        struct HingedDoor
        {
            public string name;
            public float degrees;
            public HingedDoor(string value, float yaw) { name = value; degrees = yaw; }
        }

        static readonly HingedDoor[] Hinges = {
            new HingedDoor("Hefu_NorthEntryDoor_v1", 90),
            new HingedDoor("Door_Master", -90),
            new HingedDoor("Door_Second", 90),
            new HingedDoor("Door_Kitchen", -90),
            new HingedDoor("Door_BathEnsuite", -90),
            new HingedDoor("Door_BathWest", 90),
            new HingedDoor("Door_Study", -90)
        };

        public int DoorCount { get; private set; }
        public string InteractionHint { get; private set; } = "";
        SceneWorkspaceManager _spaces;
        SceneWorkspaceLocomotion _movement;
        QuestXrUiRig _rig;
        InputAction _trigger;
        bool _pressed;
        TextMesh _hintText;
        Transform _hintRoot;

        void OnEnable()
        {
            _spaces = GetComponent<SceneWorkspaceManager>();
            _movement = GetComponent<SceneWorkspaceLocomotion>();
            if (_spaces) _spaces.SceneChanged += OnSceneChanged;
            Bind();
        }

        void OnDisable()
        {
            if (_spaces) _spaces.SceneChanged -= OnSceneChanged;
            _pressed = false;
        }

        void OnSceneChanged(SceneWorkspaceRecord _) { _pressed = false; Bind(); }

        static Transform FindNamed(Transform root, string target)
        {
            if (!root) return null;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == target) return transform;
            return null;
        }

        void Bind()
        {
            DoorCount = 0;
            if (!_spaces || !_spaces.EnvironmentRoot || _spaces.Active == null ||
                _spaces.Active.sourceType != "cad-bundle") return;
            foreach (var def in Hinges)
            {
                var doorRoot = FindNamed(_spaces.EnvironmentRoot, def.name);
                if (!doorRoot) continue;
                var hingeName = def.name == "Hefu_NorthEntryDoor_v1" ? "EntryHinge" : "DoorHinge";
                var hinge = FindNamed(doorRoot, hingeName);
                if (!hinge) continue;
                var door = doorRoot.gameObject.AddComponent<Door>();
                door.Configure(hinge, def.degrees, Vector3.zero, false);
                DoorCount++;
            }
            var slideRoot = FindNamed(_spaces.EnvironmentRoot, "StudySouth_BalconySlidingDoor");
            var slidingLeaf = FindNamed(slideRoot, "EastSlidingPanel");
            if (slideRoot && slidingLeaf)
            {
                var door = slideRoot.gameObject.AddComponent<Door>();
                door.Configure(slidingLeaf, 0f, new Vector3(-.79f, 0, 0), true);
                DoorCount++;
            }
            Debug.Log("[Spaces] restored door interaction count=" + DoorCount);
        }

        void EnsureHand()
        {
            if (!_rig) _rig = FindObjectOfType<QuestXrUiRig>();
            if (_rig != null && _rig.Actions != null && _trigger == null)
                _trigger = _rig.Actions.FindAction("RightHand UI Click", false);
        }

        void EnsureHint()
        {
            if (_hintRoot) return;
            var go = new GameObject("Door_InteractionHint");
            go.transform.SetParent(transform, false);
            _hintRoot = go.transform;
            _hintText = go.AddComponent<TextMesh>();
            _hintText.fontSize = 38;
            _hintText.characterSize = .014f;
            _hintText.anchor = TextAnchor.MiddleCenter;
            _hintText.color = new Color(.3f, .87f, 1f);
        }

        void Update()
        {
            EnsureHand();
            if (_movement == null || !_movement.CanExplore() ||
                _trigger == null || _rig == null || _rig.Origin == null)
            {
                InteractionHint = "";
                if (_hintRoot) _hintRoot.gameObject.SetActive(false);
                _pressed = false;
                return;
            }
            var controller = GameObject.Find("Right Controller");
            Door target = null;
            RaycastHit hit = default;
            if (controller && Physics.Raycast(new Ray(controller.transform.position, controller.transform.forward),
                out hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponentInParent<Door>();
                if (candidate && candidate.Accepts(hit.collider)) target = candidate;
            }
            if (target)
            {
                InteractionHint = (target.IsOpen ? "Trigger: Close " : "Trigger: Open ") + target.name;
                EnsureHint();
                _hintRoot.gameObject.SetActive(true);
                _hintRoot.position = hit.point + Vector3.up * .14f;
                var eye = _rig.Origin.Camera.transform;
                _hintRoot.rotation = Quaternion.LookRotation(_hintRoot.position - eye.position, Vector3.up);
                _hintText.text = InteractionHint;
            }
            else
            {
                InteractionHint = "";
                if (_hintRoot) _hintRoot.gameObject.SetActive(false);
            }

            bool pressed = _trigger.IsPressed();
            if (pressed && !_pressed && target)
            {
                if (!target.Toggle(_rig.Origin.Camera.transform))
                    Debug.Log("[Spaces] door close blocked by headset position");
                else Debug.Log("[Spaces] door toggled: " + target.name + " open=" + target.IsOpen);
            }
            _pressed = pressed;
        }
    }
}
