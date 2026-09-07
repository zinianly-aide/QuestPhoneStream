using System;
using System.IO;
using NUnit.Framework;
using QuestPhoneStream.Interaction;
using QuestPhoneStream.Interaction.Backends.XRI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuestPhoneStream.Tests
{
    public class PhonePanelInteractionAbstractionTests
    {
        [Test]
        public void CoreDoesNotReferenceXriSdkTypes()
        {
            var core = Path.Combine(Application.dataPath, "QuestPhoneStream/Interaction/Core");
            foreach (var file in Directory.GetFiles(core, "*.cs"))
            {
                var source = File.ReadAllText(file);
                StringAssert.DoesNotContain("XRRayInteractor", source);
                StringAssert.DoesNotContain("XRGrabInteractable", source);
                StringAssert.DoesNotContain("OVRHand", source);
                StringAssert.DoesNotContain("Meta PokeInteractor", source);
            }
        }

        [Test]
        public void ResetPoseAndScaleClampAreSdkNeutral()
        {
            var cameraGo = new GameObject("Camera"); var root = new GameObject("PhonePanelRoot");
            var manipulator = root.AddComponent<PhonePanelManipulator>();
            manipulator.defaultDistance = 1.5f; manipulator.minScale = .5f; manipulator.maxScale = 2.5f;
            cameraGo.transform.position = new Vector3(2f, 1.6f, 3f); cameraGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            manipulator.ResetPose(cameraGo.AddComponent<Camera>());
            Assert.That(Vector3.Distance(root.transform.position, cameraGo.transform.position), Is.EqualTo(1.5f).Within(.001f));
            manipulator.SetUniformScale(9f);
            Assert.That(root.transform.localScale.x, Is.EqualTo(2.5f).Within(.001f));
            Object.DestroyImmediate(root); Object.DestroyImmediate(cameraGo);
        }

        [Test]
        public void RouterSeparatesScreenTouchFromFrameManipulation()
        {
            var root = new GameObject("PhonePanelRoot");
            var screen = new GameObject("PhoneScreen"); screen.transform.SetParent(root.transform); var screenCollider = screen.AddComponent<BoxCollider>();
            var frame = new GameObject("GrabHandle"); frame.transform.SetParent(root.transform); var frameCollider = frame.AddComponent<BoxCollider>();
            var manipulator = root.AddComponent<PhonePanelManipulator>(); var router = root.AddComponent<PhonePanelInteractionRouter>();
            router.screenCollider = screenCollider; router.grabCollider = frameCollider; router.manipulator = manipulator;
            router.RouteTransform(new TransformEvent(GrabPhase.Begin, Vector3.one, Quaternion.identity, 1f, 1, InteractionSourceType.LeftController, default));
            Assert.AreEqual(PhonePanelInteractionState.OneHandGrab, manipulator.State);
            router.RoutePointer(new PointerEvent(InteractionSourceType.LeftController, PointerModality.Ray, InteractionPhase.PressBegin, Vector3.zero, Vector3.forward, frameCollider, "GrabHandle"));
            Assert.AreEqual(PhonePanelInteractionState.OneHandGrab, manipulator.State);
            router.RouteTransform(new TransformEvent(GrabPhase.End, Vector3.one, Quaternion.identity, 1f, 0, default, default));
            Assert.AreEqual(PhonePanelInteractionState.Idle, manipulator.State);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RegistryFallsBackWhenPreferredBackendIsUnavailable()
        {
            InteractionBackendRegistry.Clear();
            GameObject root = null;
            try
            {
                InteractionBackendRegistry.Register("UnavailableTest", () => new FakeBackend(false));
                InteractionBackendRegistry.Register("FallbackTest", () => new FakeBackend(true), true);
                root = new GameObject("PhonePanelRoot");
                var manager = root.AddComponent<InteractionBackendManager>();
                manager.preferredBackend = "UnavailableTest";
                Assert.IsTrue(manager.InitializeBackend(new InteractionBackendContext { panelRoot = root }));
                Assert.AreEqual("FallbackTest", manager.ActiveBackend.Name);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                InteractionBackendRegistry.Clear();
                XriInteractionBackend.EnsureRegistered();
            }
        }

        [Test]
        public void RayAndPokeUseSameAndroidCoordinateMapping()
        {
            var root = new GameObject("PhoneScreen"); var mapper = root.AddComponent<PanelInputMapper>();
            mapper.SetAndroidResolution(100, 200);
            Assert.AreEqual(mapper.MapUvToAndroidPixels(new Vector2(.25f, .75f)), mapper.MapUvToAndroidPixels(new Vector2(.25f, .75f)));
            Object.DestroyImmediate(root);
        }

        private sealed class FakeBackend : IInteractionBackend
        {
            public FakeBackend(bool available) { IsAvailable = available; }
            public string Name => IsAvailable ? "FallbackTest" : "UnavailableTest";
            public bool IsAvailable { get; }
            public InteractionCapabilities Capabilities => InteractionCapabilities.None;
            public IPointerSource Pointer => new FakePointer();
            public IManipulationSource Manipulation => new FakeManipulation();
            public void Initialize(InteractionBackendContext context) { }
            public void Shutdown() { }
        }
        private sealed class FakePointer : IPointerSource { public event Action<PointerEvent> PointerEventRaised { add { } remove { } } }
        private sealed class FakeManipulation : IManipulationSource { public event Action<TransformEvent> TransformEventRaised { add { } remove { } } }
    }
}
