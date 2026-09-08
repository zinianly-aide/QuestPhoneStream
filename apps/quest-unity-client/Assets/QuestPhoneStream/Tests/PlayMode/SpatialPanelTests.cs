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
        { uv = default; if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return false; uv = new Vector2(point.x, point.y); return true; }
        public Vector2Int MapUvToAndroidPixels(Vector2 uv) => new Vector2Int(Mathf.RoundToInt(uv.x * 100), Mathf.RoundToInt(uv.y * 100));
        public void SendClick(Vector2Int point) { clicks++; end = point; }
        public void SendSwipe(Vector2Int start, Vector2Int finish, int duration) { swipes++; end = finish; }
    }

    public class SpatialPanelTests
    {
        private GameObject _root, _surface;
        private SpatialPanelTestMapper _mapper;
        private SpatialPanelShell _shell;
        [SetUp] public void Setup()
        {
            _root = new GameObject("SpatialPanelRoot");
            _surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _surface.transform.SetParent(_root.transform, false);
            _mapper = _surface.AddComponent<SpatialPanelTestMapper>();
            _shell = _root.AddComponent<SpatialPanelShell>();
            _shell.Initialize(_surface.transform, _surface.GetComponent<Collider>(), null, new AndroidScreenSurfaceInput(_mapper));
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(_root);
        private PointerEvent Pointer(InteractionPhase phase, float x, float y, Collider target = null) =>
            new PointerEvent(InteractionSourceType.LeftController, PointerModality.Ray, phase, new Vector3(x,y,0), Vector3.forward,
                target != null ? target : _surface.GetComponent<Collider>(), "PhoneScreen");
        private void Touch(InteractionPhase phase, float x, float y) => _shell.Router.RoutePointer(Pointer(phase,x,y));

        [Test] public void ReleaseOutsidePreservesLastValidUvAndClearsGesture()
        {
            Touch(InteractionPhase.PressBegin,.1f,.2f); Touch(InteractionPhase.PressMove,.8f,.9f);
            Touch(InteractionPhase.PressEnd,2,2);
            Assert.AreEqual(new Vector2Int(80,90),_mapper.end); Assert.AreEqual(1,_mapper.swipes);
            Assert.IsFalse(_shell.Input.IsActive);
            Touch(InteractionPhase.PressEnd,0,0); Assert.AreEqual(1,_mapper.swipes);
        }
        [Test] public void LegacyPhoneTouchReleaseAlsoPreservesUv()
        {
            var touch = _root.AddComponent<PhonePanelTouchController>(); touch.mappingProvider = _mapper;
            touch.Process(Pointer(InteractionPhase.PressBegin,.1f,.1f));
            touch.Process(Pointer(InteractionPhase.PressMove,.8f,.8f));
            touch.Process(Pointer(InteractionPhase.PressEnd,2,2));
            Assert.AreEqual(new Vector2Int(80,80),_mapper.end); Assert.IsFalse(touch.IsActive);
        }
        [TestCase(.23f,1,0)] [TestCase(.24f,0,1)]
        public void ThresholdDistinguishesClickAndSwipe(float delta,int clicks,int swipes)
        {
            Touch(InteractionPhase.PressBegin,0,.5f); Touch(InteractionPhase.PressEnd,delta,.5f);
            Assert.AreEqual(clicks,_mapper.clicks); Assert.AreEqual(swipes,_mapper.swipes);
        }
        [Test] public void SurfaceCannotStartManipulation()
        {
            Touch(InteractionPhase.PressBegin,.1f,.1f);
            Assert.IsFalse(_shell.Manipulator.IsGrabActive);
            Assert.AreEqual(PhonePanelInteractionState.ScreenTouch,_shell.Manipulator.State);
        }
        [Test] public void FrameCannotSendAndroidEvenWithSpoofedSurfaceId()
        {
            _shell.Router.RoutePointer(Pointer(InteractionPhase.PressBegin,.1f,.1f,_shell.Router.grabCollider));
            _shell.Router.RoutePointer(Pointer(InteractionPhase.PressEnd,.1f,.1f,_shell.Router.grabCollider));
            Assert.AreEqual(0,_mapper.clicks); Assert.IsFalse(_shell.Input.IsActive);
        }
        [Test] public void GrabCancelsTouchWithoutClickOrSwipe()
        {
            Touch(InteractionPhase.PressBegin,.1f,.1f);
            _shell.Router.RouteTransform(new TransformEvent(GrabPhase.Begin,Vector3.zero,Quaternion.identity,1,1,default,default));
            Touch(InteractionPhase.PressEnd,.8f,.8f);
            Assert.AreEqual(0,_mapper.clicks+_mapper.swipes); Assert.IsFalse(_shell.Input.IsActive);
        }
        [TestCase("android",typeof(AndroidScreenSurfaceInput))]
        [TestCase("macos",typeof(ViewOnlySurfaceInput))]
        [TestCase("unknown",typeof(ViewOnlySurfaceInput))]
        public void PlatformSelectsHandler(string platform,System.Type expected)
        { Assert.AreEqual(expected,PanelSurfaceInputFactory.Screen(platform,_mapper,()=>true).GetType()); }
        [Test] public void MacAndMediaNeverSendAndroidCommands()
        {
            foreach (var handler in new IPanelSurfaceInputHandler[] { new ViewOnlySurfaceInput(),new MediaSurfaceInput() })
            {
                _shell.Input.SetHandler(handler); Touch(InteractionPhase.PressBegin,0,0); Touch(InteractionPhase.PressEnd,1,1);
            }
            Assert.AreEqual(0,_mapper.clicks+_mapper.swipes);
        }
        [Test] public void HandlerChangeCancelsOldDeviceGesture()
        {
            Touch(InteractionPhase.PressBegin,.1f,.1f); _shell.Input.SetHandler(new ViewOnlySurfaceInput());
            Touch(InteractionPhase.PressEnd,.8f,.8f); Assert.AreEqual(0,_mapper.clicks+_mapper.swipes);
        }
        [Test] public void ShutdownClearsGestureAndGrab()
        {
            Touch(InteractionPhase.PressBegin,.1f,.1f);
            _root.GetComponent<InteractionBackendManager>().ShutdownBackend();
            Assert.IsFalse(_shell.Input.IsActive); Assert.AreEqual(PhonePanelInteractionState.Idle,_shell.Manipulator.State);
            Touch(InteractionPhase.PressEnd,.8f,.8f); Assert.AreEqual(0,_mapper.clicks+_mapper.swipes);
        }
        [Test] public void FlatMediaUsesSameShellAndManipulator()
        {
            var media = _surface.AddComponent<FlatMediaPanelController>(); media.Initialize(null,_surface.GetComponent<Renderer>());
            Assert.IsInstanceOf<SpatialPanelManipulator>(media.Shell.Manipulator);
            Assert.IsInstanceOf<MediaSurfaceInput>(media.Shell.Input.Handler);
            media.SetProjection(ProjectionMode.Flat); Assert.IsTrue(media.Shell.Router.grabCollider.enabled);
            media.SetProjection(ProjectionMode.Equirectangular);
            Assert.IsFalse(media.Shell.Router.grabCollider.gameObject.activeInHierarchy);
        }
        [Test] public void ExpandedHandleDoesNotOverlapSurface()
        {
            Physics.SyncTransforms();
            Assert.IsFalse(_shell.Router.grabCollider.bounds.Intersects(_shell.Router.screenCollider.bounds));
            Assert.Greater(_shell.Router.grabCollider.bounds.size.y,.06f);
        }
        [Test] public void DistanceGrabKeepsRotatedRayAttachPoint()
        {
            _root.transform.position = new Vector3(0,0,3);
            var solver = new PanelGrabSolver();
            solver.Begin(InteractionSourceType.LeftController,new Pose(Vector3.zero,Quaternion.identity),new Vector3(0,0,3),_root.transform);
            var rotation = Quaternion.Euler(0,20,0);
            solver.SetOrigin(InteractionSourceType.LeftController,new Pose(Vector3.forward,rotation));
            var result = solver.Evaluate(_root.transform,GrabPhase.Update,.5f,2.5f);
            Assert.That(Vector3.Distance(result.position,Vector3.forward+rotation*Vector3.forward*3),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(result.rotation,rotation),Is.LessThan(.001f));
        }
        [Test] public void OneTwoOneTransitionsStayContinuousAndClampScale()
        {
            var solver = new PanelGrabSolver();
            solver.Begin(InteractionSourceType.LeftController,new Pose(Vector3.left,Quaternion.identity),Vector3.left,_root.transform);
            solver.Begin(InteractionSourceType.RightController,new Pose(Vector3.right,Quaternion.identity),Vector3.right,_root.transform);
            var joined = solver.Evaluate(_root.transform,GrabPhase.Begin,.5f,2.5f);
            Assert.That(joined.position.magnitude,Is.LessThan(.001f)); Assert.AreEqual(1,joined.uniformScale);
            solver.SetOrigin(InteractionSourceType.RightController,new Pose(Vector3.right*20,Quaternion.identity));
            _shell.Manipulator.Apply(solver.Evaluate(_root.transform,GrabPhase.Update,.5f,2.5f));
            Assert.AreEqual(2.5f,_root.transform.localScale.x);
            var before = _root.transform.position;
            solver.End(InteractionSourceType.RightController,_root.transform);
            var single = solver.Evaluate(_root.transform,GrabPhase.End,.5f,2.5f);
            Assert.That(Vector3.Distance(before,single.position),Is.LessThan(.001f));
            solver.End(InteractionSourceType.LeftController,_root.transform); Assert.AreEqual(0,solver.Count);
        }
        [Test] public void PokeHysteresisAvoidsThresholdChatter()
        {
            Assert.IsTrue(PokeHysteresis.IsPressed(false,.007f,.008f,.025f));
            Assert.IsTrue(PokeHysteresis.IsPressed(true,.015f,.008f,.025f));
            Assert.IsFalse(PokeHysteresis.IsPressed(false,.015f,.008f,.025f));
            Assert.IsFalse(PokeHysteresis.IsPressed(true,.03f,.008f,.025f));
        }
    }
}
