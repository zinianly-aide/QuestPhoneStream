using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using QuestPhoneStream.Interaction;
using QuestPhoneStream.Interaction.Backends.XRI;

namespace QuestPhoneStream.Tests
{
    public class XrUiRigTests
    {
        [SetUp]
        public void Setup()
        {
            InteractionBackendRegistry.Clear();
            XriInteractionBackend.EnsureRegistered();
            DestroyExistingPhonePanels();
        }

        [TearDown]
        public void CleanupRegistry()
        {
            InteractionBackendRegistry.Clear();
            XriInteractionBackend.EnsureRegistered();
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesOneInputChainWithTwoRays()
        {
            var root = new GameObject("XR test");
            var cameraObject = new GameObject("Camera without tag");
            var camera = cameraObject.AddComponent<Camera>();
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "PhonePanel";
            var videoShader = Shader.Find("QuestPhoneStream/UnlitVideo");
            Assert.IsNotNull(videoShader);
            var panelMaterial = new Material(videoShader);
            panel.GetComponent<Renderer>().sharedMaterial = panelMaterial;
            var receiver = root.AddComponent<QuestWebRtcReceiver>();
            receiver.enabled = false; // This test exercises the rig, not native WebRTC initialization.
            var rig = root.AddComponent<QuestXrUiRig>();
            rig.actionAsset = Resources.Load<InputActionAsset>("QuestUi");
            try
            {
                rig.Initialize(camera, receiver);
                var origin = rig.Origin;
                rig.Initialize(camera, receiver);
                Assert.AreSame(origin, rig.Origin);
                Assert.AreSame(camera, origin.Camera);
                Assert.IsNotNull(rig.UiEvents.GetComponent<XRUIInputModule>());
                var rays = origin.GetComponentsInChildren<XRRayInteractor>();
                Assert.AreEqual(2, rays.Length);
                foreach (var ray in rays) Assert.IsTrue(ray.enableUIInteraction);
                Assert.IsTrue(rig.Actions.FindAction("Open Settings").enabled);
                Assert.Greater(rig.Actions.FindAction("LeftHand UI Click").bindings.Count, 0);
                Assert.Greater(rig.Actions.FindAction("RightHand UI Point Position").bindings.Count, 0);

                var panelRoot = GameObject.Find("PhonePanelRoot");
                Assert.IsNotNull(panelRoot);
                Assert.AreSame(origin.transform.Find("SpatialPanels"), panelRoot.transform.parent);
                Assert.AreNotSame(camera.transform, panelRoot.transform.parent);
                var screen = panelRoot.transform.Find("PhoneScreen");
                Assert.IsNotNull(screen);
                Assert.AreSame(panel, screen.gameObject);
                var renderer = screen.GetComponent<Renderer>();
                Assert.IsNotNull(renderer);
                Assert.AreSame(renderer, receiver.phoneScreenRenderer);
                Assert.AreSame(renderer.sharedMaterial, receiver.targetMaterial);
                Assert.AreEqual("QuestPhoneStream/UnlitVideo", renderer.sharedMaterial.shader.name);
                Assert.AreEqual(0f, renderer.sharedMaterial.GetFloat("_Cull"));
                Assert.AreEqual(Quaternion.identity, screen.localRotation);
                var router = panelRoot.GetComponent<SpatialPanelInteractionRouter>();
                Assert.IsNotNull(router);
                Assert.AreNotSame(screen.GetComponent<Collider>(), router.grabCollider);
                Assert.AreSame(router, panelRoot.GetComponent<SpatialPanelShell>().Router);
                yield return null;
            }
            finally
            {
                var panelRoot = GameObject.Find("PhonePanelRoot");
                panelRoot?.GetComponent<InteractionBackendManager>()?.ShutdownBackend();
                Object.DestroyImmediate(root);
                DestroyExistingPhonePanels();
                Object.DestroyImmediate(panelMaterial);
            }
            yield return null;
        }

        private static void DestroyExistingPhonePanels()
        {
            var transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var transform in transforms)
            {
                if (transform == null) continue;
                if (transform.name != "PhonePanelRoot" && transform.name != "PhonePanel") continue;
                transform.GetComponent<InteractionBackendManager>()?.ShutdownBackend();
                Object.DestroyImmediate(transform.gameObject);
            }
        }
    }
}
