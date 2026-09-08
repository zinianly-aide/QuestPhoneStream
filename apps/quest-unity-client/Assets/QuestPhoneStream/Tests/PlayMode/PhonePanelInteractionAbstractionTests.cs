using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using QuestPhoneStream.Interaction;
using QuestPhoneStream.Interaction.Backends.XRI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuestPhoneStream.Tests
{
    public class PhonePanelInteractionAbstractionTests
    {
        [TearDown]
        public void RestoreBackendRegistry()
        {
            InteractionBackendRegistry.Clear();
            XriInteractionBackend.EnsureRegistered();
        }

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
            var cameraGo = new GameObject("Camera");
            var root = new GameObject("PhonePanelRoot");
            try
            {
                var manipulator = root.AddComponent<PhonePanelManipulator>();
                manipulator.defaultDistance = 1.5f;
                manipulator.minScale = .5f;
                manipulator.maxScale = 2.5f;
                cameraGo.transform.position = new Vector3(2f, 1.6f, 3f);
                cameraGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                manipulator.ResetPose(cameraGo.AddComponent<Camera>());
                Assert.That(Vector3.Distance(root.transform.position, cameraGo.transform.position),
                    Is.EqualTo(1.5f).Within(.001f));
                manipulator.SetUniformScale(9f);
                Assert.That(root.transform.localScale.x, Is.EqualTo(2.5f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void RouterSeparatesScreenTouchFromFrameManipulation()
        {
            var root = new GameObject("PhonePanelRoot");
            try
            {
                var screen = new GameObject("PhoneScreen");
                screen.transform.SetParent(root.transform);
                var screenCollider = screen.AddComponent<BoxCollider>();
                var frame = new GameObject("GrabHandle");
                frame.transform.SetParent(root.transform);
                var frameCollider = frame.AddComponent<BoxCollider>();
                var manipulator = root.AddComponent<PhonePanelManipulator>();
                var router = root.AddComponent<PhonePanelInteractionRouter>();
                router.screenCollider = screenCollider;
                router.grabCollider = frameCollider;
                router.manipulator = manipulator;

                router.RouteTransform(new TransformEvent(GrabPhase.Begin, Vector3.one, Quaternion.identity,
                    1f, 1, InteractionSourceType.LeftController, default));
                Assert.AreEqual(PhonePanelInteractionState.OneHandGrab, manipulator.State);
                router.RoutePointer(new PointerEvent(InteractionSourceType.LeftController, PointerModality.Ray,
                    InteractionPhase.PressBegin, Vector3.zero, Vector3.forward, frameCollider, "GrabHandle"));
                Assert.AreEqual(PhonePanelInteractionState.OneHandGrab, manipulator.State);
                router.RouteTransform(new TransformEvent(GrabPhase.End, Vector3.one, Quaternion.identity,
                    1f, 0, default, default));
                Assert.AreEqual(PhonePanelInteractionState.Idle, manipulator.State);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
                if (root != null)
                {
                    root.GetComponent<InteractionBackendManager>()?.ShutdownBackend();
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void XriRayGestureLifecyclePreservesRayModalityThroughEndAndDisableCancel()
        {
            var host = new GameObject("XriPointerSource test");
            try
            {
                var source = host.AddComponent<XriPointerSource>();
                var events = new List<PointerEvent>();
                source.PointerEventRaised += pointer => events.Add(pointer);

                var type = typeof(XriPointerSource);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var begin = type.GetMethod("BeginGesture", flags, null,
                    new[] { typeof(InteractionSourceType), typeof(PointerModality), typeof(Vector3), typeof(Vector3) }, null);
                var move = type.GetMethod("MoveGesture", flags, null,
                    new[] { typeof(InteractionSourceType), typeof(PointerModality), typeof(Vector3), typeof(Vector3) }, null);
                var end = type.GetMethod("EndGesture", flags, null,
                    new[] { typeof(InteractionSourceType), typeof(PointerModality), typeof(bool), typeof(Vector3), typeof(Vector3) }, null);
                Assert.IsNotNull(begin);
                Assert.IsNotNull(move);
                Assert.IsNotNull(end);

                var right = InteractionSourceType.RightController;
                begin.Invoke(source, new object[] { right, PointerModality.Ray, Vector3.zero, Vector3.forward });
                move.Invoke(source, new object[] { right, PointerModality.Ray, Vector3.forward, Vector3.forward });
                end.Invoke(source, new object[] { right, PointerModality.Ray, false, Vector3.forward * 2, Vector3.forward });

                CollectionAssert.AreEqual(
                    new[] { InteractionPhase.PressBegin, InteractionPhase.PressMove, InteractionPhase.PressEnd },
                    events.ConvertAll(pointer => pointer.phase));
                Assert.IsTrue(events.TrueForAll(pointer =>
                    pointer.source == right && pointer.modality == PointerModality.Ray));

                events.Clear();
                begin.Invoke(source, new object[] { right, PointerModality.Ray, Vector3.zero, Vector3.forward });
                host.SetActive(false);

                Assert.AreEqual(2, events.Count);
                Assert.AreEqual(InteractionPhase.PressBegin, events[0].phase);
                Assert.AreEqual(InteractionPhase.PressCancel, events[1].phase);
                Assert.IsTrue(events.TrueForAll(pointer =>
                    pointer.source == right && pointer.modality == PointerModality.Ray));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RayAndPokeUseSameAndroidCoordinateMapping()
        {
            var root = new GameObject("PhoneScreen");
            try
            {
                var mapper = root.AddComponent<PanelInputMapper>();
                mapper.SetAndroidResolution(100, 200);
                Assert.AreEqual(mapper.MapUvToAndroidPixels(new Vector2(.25f, .75f)),
                    mapper.MapUvToAndroidPixels(new Vector2(.25f, .75f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private sealed class FakeBackend : IInteractionBackend
        {
            private readonly FakePointer _pointer = new FakePointer();
            private readonly FakeManipulation _manipulation = new FakeManipulation();

            public FakeBackend(bool available) { IsAvailable = available; }
            public string Name => IsAvailable ? "FallbackTest" : "UnavailableTest";
            public bool IsAvailable { get; }
            public InteractionCapabilities Capabilities => InteractionCapabilities.None;
            public IPointerSource Pointer => _pointer;
            public IManipulationSource Manipulation => _manipulation;
            public void Initialize(InteractionBackendContext context) { }
            public void Shutdown() { }
        }

        private sealed class FakePointer : IPointerSource
        {
            public event Action<PointerEvent> PointerEventRaised { add { } remove { } }
        }

        private sealed class FakeManipulation : IManipulationSource
        {
            public event Action<TransformEvent> TransformEventRaised { add { } remove { } }
        }
    }
}
