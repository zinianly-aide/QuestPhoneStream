using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Interaction;
using Object = UnityEngine.Object;

namespace QuestPhoneStream.Tests
{
    public sealed class SpatialPanelTestMapper : MonoBehaviour, IPhonePanelTouchMapper
    {
        public int clicks, swipes;
        public Vector2Int end;
        public bool blocked;
        public bool IsInputBlocked => blocked;
        public int SwipeThresholdPixels => 24;
        public bool TryMapWorldPointToUv(Vector3 point, Vector3 normal, out Vector2 uv)
        {
            uv = default;
            if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return false;
            uv = new Vector2(point.x, point.y);
            return true;
        }
        public Vector2Int MapUvToAndroidPixels(Vector2 uv) =>
            new Vector2Int(Mathf.RoundToInt(uv.x * 100), Mathf.RoundToInt(uv.y * 100));
        public void SendClick(Vector2Int point) { clicks++; end = point; }
        public void SendSwipe(Vector2Int start, Vector2Int finish, int duration) { swipes++; end = finish; }
    }

    public class SpatialPanelTests
    {
        private GameObject _root, _surface;
        private SpatialPanelTestMapper _mapper;
        private SpatialPanelShell _shell;

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("SpatialPanelRoot");
            _surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _surface.transform.SetParent(_root.transform, false);
            _mapper = _surface.AddComponent<SpatialPanelTestMapper>();
            _shell = _root.AddComponent<SpatialPanelShell>();
            _shell.Initialize(_surface.transform, _surface.GetComponent<Collider>(), null,
                new AndroidScreenSurfaceInput(_mapper));
        }

        [TearDown]
        public void Cleanup()
        {
            if (_root != null)
            {
                _root.GetComponent<InteractionBackendManager>()?.ShutdownBackend();
                Object.DestroyImmediate(_root);
            }
        }

        private PointerEvent Pointer(InteractionPhase phase, float x, float y, Collider target = null,
            InteractionSourceType source = InteractionSourceType.LeftController,
            PointerModality modality = PointerModality.Ray) =>
            new PointerEvent(source, modality, phase, new Vector3(x, y, 0), Vector3.forward,
                target != null ? target : _surface.GetComponent<Collider>(), "PhoneScreen");

        private void Touch(InteractionPhase phase, float x, float y,
            InteractionSourceType source = InteractionSourceType.LeftController,
            PointerModality modality = PointerModality.Ray) =>
            _shell.Router.RoutePointer(Pointer(phase, x, y, null, source, modality));

        [Test]
        public void AndroidScreenReleaseOutsidePreservesLastValidUvAndClearsGesture()
        {
            Touch(InteractionPhase.PressBegin, .1f, .2f);
            Touch(InteractionPhase.PressMove, .8f, .9f);
            Touch(InteractionPhase.PressEnd, 2, 2);
            Assert.AreEqual(new Vector2Int(80, 90), _mapper.end);
            Assert.AreNotEqual(Vector2Int.zero, _mapper.end);
            Assert.AreEqual(1, _mapper.swipes);
            Assert.IsFalse(_shell.Input.IsActive);
            Touch(InteractionPhase.PressEnd, 0, 0);
            Assert.AreEqual(1, _mapper.swipes);
        }

        [Test]
        public void UnrelatedReleaseCannotEndOwnedSurfaceGesture()
        {
            Touch(InteractionPhase.PressBegin, .1f, .2f, InteractionSourceType.RightController);
            Touch(InteractionPhase.PressMove, .8f, .9f, InteractionSourceType.RightController);

            Touch(InteractionPhase.PressEnd, .8f, .9f, InteractionSourceType.LeftController);

            Assert.IsTrue(_shell.Input.IsActive);
            Assert.AreEqual(PhonePanelInteractionState.ScreenTouch, _shell.Manipulator.State);
            Assert.AreEqual(0, _mapper.clicks + _mapper.swipes);

            Touch(InteractionPhase.PressEnd, .8f, .9f, InteractionSourceType.RightController);

            Assert.IsFalse(_shell.Input.IsActive);
            Assert.AreEqual(PhonePanelInteractionState.Idle, _shell.Manipulator.State);
            Assert.AreEqual(1, _mapper.swipes);
        }

        [Test]
        public void ActiveSurfaceSwipeSuppressesPanelManipulationUntilGripRelease()
        {
            Touch(InteractionPhase.PressBegin, .1f, .1f, InteractionSourceType.RightController);
            Touch(InteractionPhase.PressMove, .7f, .7f, InteractionSourceType.RightController);
            var originalPosition = _root.transform.position;
            var originalScale = _root.transform.localScale;

            _shell.Router.RouteTransform(new TransformEvent(GrabPhase.Begin, Vector3.one,
                Quaternion.Euler(0, 30, 0), 2f, 1, InteractionSourceType.LeftController, default));

            Assert.IsTrue(_shell.Input.IsActive);
            Assert.AreEqual(PhonePanelInteractionState.ScreenTouch, _shell.Manipulator.State);
            Assert.AreEqual(originalPosition, _root.transform.position);
            Assert.AreEqual(originalScale, _root.transform.localScale);

            Touch(InteractionPhase.PressEnd, .7f, .7f, InteractionSourceType.RightController);
            _shell.Router.RouteTransform(new TransformEvent(GrabPhase.Update, Vector3.one,
                Quaternion.Euler(0, 30, 0), 2f, 1, InteractionSourceType.LeftController, default));

            Assert.AreEqual(PhonePanelInteractionState.Idle, _shell.Manipulator.State);
            Assert.AreEqual(originalPosition, _root.transform.position);
            Assert.AreEqual(originalScale, _root.transform.localScale);

            _shell.Router.RouteTransform(new TransformEvent(GrabPhase.End, originalPosition,
                Quaternion.identity, 1f, 0, default, default));
            _shell.Router.RouteTransform(new TransformEvent(GrabPhase.Begin, Vector3.one,
                Quaternion.identity, 2f, 1, InteractionSourceType.LeftController, default));

            Assert.AreEqual(PhonePanelInteractionState.OneHandGrab, _shell.Manipulator.State);
            Assert.AreEqual(Vector3.one, _root.transform.position);
            Assert.AreEqual(Vector3.one * 2f, _root.transform.localScale);
        }

        [Test]
        public void LegacyPhoneTouchReleaseAlsoPreservesUv()
        {
            var touch = _root.AddComponent<PhonePanelTouchController>();
            touch.mappingProvider = _mapper;
            touch.Process(Pointer(InteractionPhase.PressBegin, .1f, .1f));
            touch.Process(Pointer(InteractionPhase.PressMove, .8f, .8f));
            touch.Process(Pointer(InteractionPhase.PressEnd, 2, 2));
            Assert.AreEqual(new Vector2Int(80, 80), _mapper.end);
            Assert.IsFalse(touch.IsActive);
        }

        [TestCase(.23f, 1, 0)]
        [TestCase(.24f, 0, 1)]
        public void ThresholdDistinguishesClickAndSwipe(float delta, int clicks, int swipes)
        {
            Touch(InteractionPhase.PressBegin, 0, .5f);
            Touch(InteractionPhase.PressEnd, delta, .5f);
            Assert.AreEqual(clicks, _mapper.clicks);
            Assert.AreEqual(swipes, _mapper.swipes);
        }

        [Test]
        public void SurfaceCannotStartManipulation()
        {
            Touch(InteractionPhase.PressBegin, .1f, .1f);
            Assert.IsFalse(_shell.Manipulator.IsGrabActive);
            Assert.AreEqual(PhonePanelInteractionState.ScreenTouch, _shell.Manipulator.State);
        }

        [Test]
        public void FrameCannotSendAndroidEvenWithSpoofedSurfaceId()
        {
            _shell.Router.RoutePointer(Pointer(InteractionPhase.PressBegin, .1f, .1f, _shell.Router.grabCollider));
            _shell.Router.RoutePointer(Pointer(InteractionPhase.PressEnd, .1f, .1f, _shell.Router.grabCollider));
            Assert.AreEqual(0, _mapper.clicks);
            Assert.IsFalse(_shell.Input.IsActive);
        }

        [TestCase("android", typeof(AndroidScreenSurfaceInput))]
        [TestCase("macos", typeof(ViewOnlySurfaceInput))]
        [TestCase("unknown", typeof(ViewOnlySurfaceInput))]
        public void PlatformSelectsHandler(string platform, System.Type expected)
        {
            Assert.AreEqual(expected, PanelSurfaceInputFactory.Screen(platform, _mapper, () => true).GetType());
        }

        [Test]
        public void MacViewOnlySurfaceCannotSendAndroidControl()
        {
            _shell.Input.SetHandler(PanelSurfaceInputFactory.Screen("macos", _mapper, () => true));
            Touch(InteractionPhase.PressBegin, .1f, .1f);
            Touch(InteractionPhase.PressEnd, .8f, .8f);

            Assert.IsInstanceOf<ViewOnlySurfaceInput>(_shell.Input.Handler);
            Assert.AreEqual(0, _mapper.clicks + _mapper.swipes);
            Assert.IsFalse(_shell.Input.IsActive);
        }

        [Test]
        public void MediaSurfaceNeverSendsAndroidCommands()
        {
            _shell.Input.SetHandler(new MediaSurfaceInput());
            Touch(InteractionPhase.PressBegin, 0, 0);
            Touch(InteractionPhase.PressEnd, 1, 1);
            Assert.AreEqual(0, _mapper.clicks + _mapper.swipes);
        }

        [Test]
        public void HandlerChangeCancelsOldDeviceGesture()
        {
            Touch(InteractionPhase.PressBegin, .1f, .1f);
            _shell.Input.SetHandler(new ViewOnlySurfaceInput());
            Touch(InteractionPhase.PressEnd, .8f, .8f);
            Assert.AreEqual(0, _mapper.clicks + _mapper.swipes);
            Assert.IsFalse(_shell.Input.IsActive);
            Assert.AreEqual(PhonePanelInteractionState.Idle, _shell.Manipulator.State);
        }

        [Test]
        public void ShutdownClearsGestureAndGrab()
        {
            Touch(InteractionPhase.PressBegin, .1f, .1f);
            _root.GetComponent<InteractionBackendManager>().ShutdownBackend();
            Assert.IsFalse(_shell.Input.IsActive);
            Assert.AreEqual(PhonePanelInteractionState.Idle, _shell.Manipulator.State);
            Touch(InteractionPhase.PressEnd, .8f, .8f);
            Assert.AreEqual(0, _mapper.clicks + _mapper.swipes);
        }

        [Test]
        public void FlatMediaUsesSameShellAndManipulator()
        {
            var media = _surface.AddComponent<FlatMediaPanelController>();
            media.Initialize(null, _surface.GetComponent<Renderer>());
            Assert.IsInstanceOf<SpatialPanelManipulator>(media.Shell.Manipulator);
            Assert.IsInstanceOf<MediaSurfaceInput>(media.Shell.Input.Handler);
            media.SetProjection(ProjectionMode.Flat);
            Assert.IsTrue(media.Shell.Router.grabCollider.enabled);
            media.SetProjection(ProjectionMode.Equirectangular);
            Assert.IsFalse(media.Shell.Router.grabCollider.gameObject.activeInHierarchy);
        }

        [Test]
        public void ExpandedHandleDoesNotOverlapSurface()
        {
            Physics.SyncTransforms();
            Assert.Greater(_shell.Router.grabCollider.bounds.size.y, .06f);
            // Handle must sit on the user-facing (-Z) side so a forward ray can hit it.
            var handle = _shell.Router.grabCollider.transform;
            var surface = _shell.Router.screenCollider.transform;
            Assert.Less(handle.localPosition.z, surface.localPosition.z);
        }

        [Test]
        public void DistanceGrabKeepsRotatedRayAttachPoint()
        {
            _root.transform.position = new Vector3(0, 0, 3);
            var solver = new PanelGrabSolver();
            solver.Begin(InteractionSourceType.LeftController, new Pose(Vector3.zero, Quaternion.identity),
                new Vector3(0, 0, 3), _root.transform);
            var rotation = Quaternion.Euler(0, 20, 0);
            solver.SetOrigin(InteractionSourceType.LeftController, new Pose(Vector3.forward, rotation));
            var result = solver.Evaluate(_root.transform, GrabPhase.Update, .5f, 2.5f);
            Assert.That(Vector3.Distance(result.position, Vector3.forward + rotation * Vector3.forward * 3),
                Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(result.rotation, rotation), Is.LessThan(.001f));
        }

        [Test]
        public void OneHandToTwoHandToOneHandRebasesWithoutPoseOrScaleJump()
        {
            _root.transform.position = new Vector3(0, 0, 2);
            _root.transform.localScale = Vector3.one * 1.2f;
            var solver = new PanelGrabSolver();

            solver.Begin(InteractionSourceType.LeftController,
                new Pose(new Vector3(-.3f, 0, 0), Quaternion.identity),
                new Vector3(-.3f, 0, 2), _root.transform);
            solver.SetOrigin(InteractionSourceType.LeftController,
                new Pose(new Vector3(-.1f, 0, 0), Quaternion.identity));
            _shell.Manipulator.Apply(solver.Evaluate(_root.transform, GrabPhase.Update, .5f, 2.5f));

            var oneHandPosition = _root.transform.position;
            var oneHandScale = _root.transform.localScale.x;
            solver.Begin(InteractionSourceType.RightController,
                new Pose(new Vector3(.5f, 0, 0), Quaternion.identity),
                new Vector3(.5f, 0, 2), _root.transform);
            var joined = solver.Evaluate(_root.transform, GrabPhase.Begin, .5f, 2.5f);

            Assert.That(Vector3.Distance(oneHandPosition, joined.position), Is.LessThan(.001f));
            Assert.That(Mathf.Abs(oneHandScale - joined.uniformScale), Is.LessThan(.001f));
            _shell.Manipulator.Apply(joined);

            solver.SetOrigin(InteractionSourceType.RightController,
                new Pose(new Vector3(.8f, 0, 0), Quaternion.identity));
            _shell.Manipulator.Apply(solver.Evaluate(_root.transform, GrabPhase.Update, .5f, 2.5f));
            var twoHandPosition = _root.transform.position;
            var twoHandScale = _root.transform.localScale.x;

            solver.End(InteractionSourceType.RightController, _root.transform);
            var single = solver.Evaluate(_root.transform, GrabPhase.End, .5f, 2.5f);

            Assert.That(Vector3.Distance(twoHandPosition, single.position), Is.LessThan(.001f));
            Assert.That(Mathf.Abs(twoHandScale - single.uniformScale), Is.LessThan(.001f));
            _shell.Manipulator.Apply(single);

            solver.SetOrigin(InteractionSourceType.LeftController,
                new Pose(Vector3.zero, Quaternion.identity));
            var resumed = solver.Evaluate(_root.transform, GrabPhase.Update, .5f, 2.5f);
            Assert.That(Mathf.Abs(twoHandScale - resumed.uniformScale), Is.LessThan(.001f));

            solver.End(InteractionSourceType.LeftController, _root.transform);
            Assert.AreEqual(0, solver.Count);
        }

        [Test]
        public void TwoHandGrabSurvivesDegeneratePanelScale()
        {
            _root.transform.position = new Vector3(0, 0, 2);
            _root.transform.localScale = Vector3.zero;
            var solver = new PanelGrabSolver();
            solver.Begin(InteractionSourceType.LeftController,
                new Pose(new Vector3(-.3f, 0, 0), Quaternion.identity),
                new Vector3(-.3f, 0, 2), _root.transform);
            solver.Begin(InteractionSourceType.RightController,
                new Pose(new Vector3(.3f, 0, 0), Quaternion.identity),
                new Vector3(.3f, 0, 2), _root.transform);
            solver.SetOrigin(InteractionSourceType.RightController,
                new Pose(new Vector3(.6f, 0, 0), Quaternion.identity));
            var pose = solver.Evaluate(_root.transform, GrabPhase.Update, .5f, 2.5f);
            Assert.IsFalse(float.IsNaN(pose.position.x));
            Assert.IsFalse(float.IsInfinity(pose.position.x));
            Assert.IsFalse(float.IsNaN(pose.uniformScale));
        }

        [Test]
        public void PokeHysteresisAvoidsThresholdChatter()
        {
            Assert.IsTrue(PokeHysteresis.IsPressed(false, .007f, .008f, .025f));
            Assert.IsTrue(PokeHysteresis.IsPressed(true, .015f, .008f, .025f));
            Assert.IsFalse(PokeHysteresis.IsPressed(false, .015f, .008f, .025f));
            Assert.IsFalse(PokeHysteresis.IsPressed(true, .03f, .008f, .025f));
        }
    }
}
