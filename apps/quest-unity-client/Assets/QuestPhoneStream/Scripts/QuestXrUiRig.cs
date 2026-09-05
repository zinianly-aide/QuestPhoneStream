using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream
{
    // Explicit, idempotent scene bootstrap using the installed XRI 3.0 components.
    public sealed class QuestXrUiRig : MonoBehaviour
    {
        public InputActionAsset actionAsset;
        public XROrigin Origin { get; private set; }
        public EventSystem UiEvents { get; private set; }
        public InputActionMap Actions { get; private set; }
        private QuestWebRtcReceiver _receiver;
        private GameObject _root, _events;
        private readonly List<InputActionReference> _references = new List<InputActionReference>();

        public void Initialize(Camera camera, QuestWebRtcReceiver receiver)
        {
            if (Origin != null) return;
            if (camera == null || receiver == null || actionAsset == null)
                throw new ArgumentException("XR rig requires camera, receiver and the scene Input Action Asset");
            _receiver = receiver;
            // The serialized asset is loaded before OpenXR attaches its bindings at startup.
            Actions = actionAsset.FindActionMap("Quest UI", true);
            var open = Actions.FindAction("Open Settings", true);
            open.performed += OpenSettings;

            _root = new GameObject("XR Origin");
            _root.SetActive(false);
            _root.transform.position = new Vector3(camera.transform.position.x, 0, camera.transform.position.z);
            Origin = _root.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(_root.transform, false);
            camera.transform.SetParent(offset.transform, false);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            Origin.Camera = camera;
            Origin.CameraFloorOffsetObject = offset;
            Origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            Origin.CameraYOffset = 1.6f;
            ConfigurePose(camera.gameObject, "Head");

            var manager = _root.AddComponent<XRInteractionManager>();
            _events = new GameObject("EventSystem");
            _events.SetActive(false);
            UiEvents = _events.AddComponent<EventSystem>();
            var module = _events.AddComponent<XRUIInputModule>();
            module.uiCamera = camera;
            module.enableXRInput = true;
            module.enableMouseInput = false;
            module.enableTouchInput = false;
            module.enableGamepadInput = false;
            module.enableJoystickInput = false;
            module.enableBuiltinActionsAsFallback = false;
            CreateController(offset.transform, manager, "LeftHand");
            CreateController(offset.transform, manager, "RightHand");
            Actions.Enable();
            _events.SetActive(true);
            _root.SetActive(true);
            Debug.Log($"[QuestPhoneStream] XR rig initialized. Camera world pos={camera.transform.position} rot={camera.transform.eulerAngles}");
            PinPanelToCamera(camera);
            WirePanelInput();
        }

        /// <summary>Wire the right-hand controller ray and trigger action to PanelInputMapper at runtime.</summary>
        private void WirePanelInput()
        {
            var panelInput = FindFirstObjectByType<PanelInputMapper>();
            if (panelInput == null)
            {
                Debug.LogWarning("[QuestPhoneStream] WirePanelInput: PanelInputMapper not found in scene");
                return;
            }
            var rightController = GameObject.Find("Right Controller");
            if (rightController != null)
            {
                var interactor = rightController.GetComponent<XRRayInteractor>();
                if (interactor != null)
                {
                    panelInput.controllerInteractor = interactor;
                    Debug.Log("[QuestPhoneStream] PanelInputMapper wired to Right Controller ray");
                }
            }
            var triggerAction = Actions.FindAction("RightHand UI Click", true);
            if (triggerAction != null)
            {
                panelInput.clickAction = triggerAction;
                Debug.Log("[QuestPhoneStream] PanelInputMapper clickAction bound to RightHand UI Click (trigger)");
            }
        }

        // The phone mirror panel lives at a fixed scene position by default, which
        // is not where the real head is after the XR origin is rebuilt from the
        // guardian space. Reparent it under the camera so it is always in front.
        private void PinPanelToCamera(Camera camera)
        {
            var panel = GameObject.Find("PhonePanel");
            if (panel == null)
            {
                Debug.LogError("[QuestPhoneStream] PinPanelToCamera: PhonePanel NOT FOUND");
                return;
            }
            panel.transform.SetParent(camera.transform, false);
            panel.transform.localPosition = new Vector3(0, 0.05f, 2.2f);
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = new Vector3(0.9f, 1.6f, 1f); // 9:16 portrait mirror

            // Ensure the receiver writes video to the SAME material the renderer uses.
            // Use sharedMaterial to avoid creating a per-renderer instance that would
            // diverge from the serialized targetMaterial reference.
            var r = panel.GetComponent<Renderer>();
            if (r != null)
            {
                _receiver.phoneScreenRenderer = r;
                if (_receiver.mediaPlayback != null) _receiver.mediaPlayback.phoneScreenRenderer = r;
                var shared = r.sharedMaterial;
                if (shared != null)
                {
                    _receiver.targetMaterial = shared;
                    if (shared.HasProperty("_Cull")) shared.SetFloat("_Cull", 0f);
                }
                Debug.Log($"[QuestPhoneStream] Panel material: renderer.shared={shared?.name} " +
                          $"receiver.target={_receiver.targetMaterial?.name} " +
                          $"same={shared == _receiver.targetMaterial}");
            }

            var vp = camera.WorldToViewportPoint(panel.transform.position);
            Debug.Log($"[QuestPhoneStream] Panel pinned. localPos={panel.transform.localPosition} " +
                      $"viewport=({vp.x:F2},{vp.y:F2},{vp.z:F2}) active={panel.activeInHierarchy}");
        }

        private InputActionReference Reference(string name)
        {
            var reference = InputActionReference.Create(Actions.FindAction(name, true));
            _references.Add(reference);
            return reference;
        }

        private void ConfigurePose(GameObject target, string name)
        {
            var pose = target.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(Reference(name + " UI Point Position"));
            pose.rotationInput = new InputActionProperty(Reference(name + " UI Point Rotation"));
            pose.ignoreTrackingState = true;
        }

        private void CreateController(Transform parent, XRInteractionManager manager, string hand)
        {
            var controller = new GameObject(hand == "LeftHand" ? "Left Controller" : "Right Controller");
            controller.transform.SetParent(parent, false);
            ConfigurePose(controller, hand);
            var ray = controller.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            ray.enableUIInteraction = true;
            ray.maxRaycastDistance = 5f;
            ray.uiPressInput = new XRInputButtonReader {
                inputSourceMode = XRInputButtonReader.InputSourceMode.InputActionReference,
                inputActionReferencePerformed = Reference(hand + " UI Click"),
                inputActionReferenceValue = Reference(hand + " UI Click Value")
            };
            // UI selection is handled by XRI; no custom raycast/input dispatch implementation.
            ray.selectInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.Unused };
            controller.AddComponent<XRInteractorLineVisual>();
        }

        private void OpenSettings(InputAction.CallbackContext _) { _receiver.ToggleHome(); }
        private void OnDisable() { Actions?.Disable(); }
        private void OnEnable() { Actions?.Enable(); }
        private void OnDestroy()
        {
            if (Actions != null)
            {
                Actions.Disable();
                Actions.FindAction("Open Settings").performed -= OpenSettings;
            }
            if (_events != null) Destroy(_events);
            if (_root != null) { _root.SetActive(false); Destroy(_root); }
            foreach (var reference in _references) Destroy(reference);
        }
    }
}
