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
            ConfigureSpatialPhonePanel(camera);
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
            var leftController = GameObject.Find("Left Controller");
            XRRayInteractor rightRay = null;
            XRRayInteractor leftRay = null;
            if (rightController != null)
            {
                rightRay = rightController.GetComponent<XRRayInteractor>();
                if (rightRay != null)
                {
                    panelInput.controllerInteractor = rightRay;
                    Debug.Log("[QuestPhoneStream] PanelInputMapper wired to Right Controller ray");
                }
            }
            if (leftController != null)
            {
                leftRay = leftController.GetComponent<XRRayInteractor>();
                if (leftRay != null) panelInput.secondaryControllerInteractor = leftRay;
            }
            var triggerAction = Actions.FindAction("RightHand UI Click", true);
            if (triggerAction != null)
            {
                panelInput.clickAction = triggerAction;
                Debug.Log("[QuestPhoneStream] PanelInputMapper clickAction bound to RightHand UI Click (trigger)");
            }
            panelInput.secondaryClickAction = Actions.FindAction("LeftHand UI Click", true);

            var spatial = panelInput.GetComponentInParent<PhonePanelSpatialInteraction>();
            if (spatial != null)
            {
                spatial.leftRay = leftRay;
                spatial.rightRay = rightRay;
                spatial.leftGrabAction = Actions.FindAction("LeftHand Grab", true);
                spatial.rightGrabAction = Actions.FindAction("RightHand Grab", true);
                panelInput.spatialInteraction = spatial;
            }
        }

        private void ConfigureSpatialPhonePanel(Camera camera)
        {
            var panel = GameObject.Find("PhonePanelRoot") ?? GameObject.Find("PhonePanel");
            if (panel == null)
            {
                Debug.LogError("[QuestPhoneStream] ConfigureSpatialPhonePanel: PhonePanel NOT FOUND");
                return;
            }

            var spatialPanels = _root.transform.Find("SpatialPanels");
            if (spatialPanels == null)
            {
                spatialPanels = new GameObject("SpatialPanels").transform;
                spatialPanels.SetParent(_root.transform, false);
            }

            var root = panel.name == "PhonePanelRoot" ? panel : CreatePhonePanelRoot(panel, spatialPanels);
            root.transform.SetParent(spatialPanels, true);
            var screen = root.transform.Find("PhoneScreen")?.gameObject ?? panel;
            var screenCollider = screen.GetComponent<Collider>();
            var handle = EnsureGrabHandle(root.transform);
            var spatial = root.GetComponent<PhonePanelSpatialInteraction>() ?? root.AddComponent<PhonePanelSpatialInteraction>();
            spatial.screenCollider = screenCollider;
            spatial.grabCollider = handle.GetComponent<Collider>();
            spatial.frameRenderer = handle.GetComponent<Renderer>();
            spatial.defaultDistance = 1.5f;
            spatial.minScale = 0.5f;
            spatial.maxScale = 2.5f;
            spatial.ResetPose(camera);

            var mapper = screen.GetComponent<PanelInputMapper>();
            if (mapper != null)
            {
                mapper.panelCollider = screenCollider;
                mapper.spatialInteraction = spatial;
            }
            var handInteraction = root.GetComponent<PhonePanelHandInteraction>() ?? root.AddComponent<PhonePanelHandInteraction>();
            handInteraction.panel = spatial;
            handInteraction.inputMapper = mapper;

            // Ensure the receiver writes video to the SAME material the renderer uses.
            // Use sharedMaterial to avoid creating a per-renderer instance that would
            // diverge from the serialized targetMaterial reference.
            var r = screen.GetComponent<Renderer>();
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

            var vp = camera.WorldToViewportPoint(root.transform.position);
            Debug.Log($"[QuestPhoneStream] Spatial PhonePanel ready. worldPos={root.transform.position} " +
                      $"viewport=({vp.x:F2},{vp.y:F2},{vp.z:F2}) active={panel.activeInHierarchy}");
        }

        private static GameObject CreatePhonePanelRoot(GameObject screen, Transform parent)
        {
            var root = new GameObject("PhonePanelRoot");
            root.transform.SetPositionAndRotation(screen.transform.position, screen.transform.rotation);
            root.transform.localScale = Vector3.one;
            root.transform.SetParent(parent, true);
            screen.name = "PhoneScreen";
            screen.transform.SetParent(root.transform, true);
            if (screen.GetComponent<PhonePanelController>() != null) Destroy(screen.GetComponent<PhonePanelController>());
            if (root.GetComponent<PhonePanelController>() == null) root.AddComponent<PhonePanelController>();
            return root;
        }

        private static GameObject EnsureGrabHandle(Transform root)
        {
            var existing = root.Find("Frame/GrabHandle");
            if (existing != null) return existing.gameObject;
            var frame = new GameObject("Frame");
            frame.transform.SetParent(root, false);
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "GrabHandle";
            handle.transform.SetParent(frame.transform, false);
            handle.transform.localPosition = new Vector3(0f, -0.88f, 0.025f);
            handle.transform.localScale = new Vector3(0.78f, 0.06f, 0.04f);
            return handle;
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
            // Trigger remains screen/UI-only. No XRI select input is configured;
            // PhonePanelSpatialInteraction owns frame grabbing from the separate grip action.
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
