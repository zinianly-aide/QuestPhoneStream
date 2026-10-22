using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceAffordanceTests
    {
        [Test]
        public void HingeAcceptsOnlyLeafOrHandleAndTracksOpenClose()
        {
            var root = new GameObject("Door_Master");
            try
            {
                var hinge = new GameObject("DoorHinge").transform;
                hinge.SetParent(root.transform);
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = "DoorLeaf";
                leaf.transform.SetParent(hinge);
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "Jamb";
                frame.transform.SetParent(root.transform);
                var door = root.AddComponent<SceneWorkspaceDoors.Door>();
                door.Configure(hinge, -90, Vector3.zero, false);
                Assert.IsTrue(door.Accepts(leaf.GetComponent<Collider>()));
                Assert.IsFalse(door.Accepts(frame.GetComponent<Collider>()));
                Assert.IsTrue(door.Toggle(null));
                Assert.IsTrue(door.IsOpen);
                Assert.IsTrue(door.Toggle(null));
                Assert.IsFalse(door.IsOpen);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SliderRespectsDesignatedMovableLeaf()
        {
            var root = new GameObject("StudySouth_BalconySlidingDoor");
            try
            {
                var sliding = new GameObject("EastSlidingPanel").transform;
                sliding.SetParent(root.transform);
                var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glass.transform.SetParent(sliding);
                var door = root.AddComponent<SceneWorkspaceDoors.Door>();
                door.Configure(sliding, 0, new Vector3(-.79f, 0, 0), true);
                Assert.IsTrue(door.sliding);
                Assert.AreEqual(-.79f, door.slideOffset.x, .001f);
                Assert.IsTrue(door.Accepts(glass.GetComponent<Collider>()));
                Assert.IsTrue(door.Toggle(null));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EyeHeightTrimIsBoundedAndPersists()
        {
            const string key = "qps.space.eyeTrimMeters";
            PlayerPrefs.DeleteKey(key);
            var go = new GameObject("HeightRigTest");
            try
            {
                var rig = go.AddComponent<QuestXrUiRig>();
                rig.SetEyeTrim(-.08f);
                Assert.AreEqual(-.08f, rig.EyeTrimMeters, .001f);
                Assert.AreEqual(-.08f, PlayerPrefs.GetFloat(key), .001f);
                rig.SetEyeTrim(-.8f);
                Assert.AreEqual(-.20f, rig.EyeTrimMeters, .001f);
                rig.SetEyeTrim(.8f);
                Assert.AreEqual(.20f, rig.EyeTrimMeters, .001f);
            }
            finally { Object.DestroyImmediate(go); PlayerPrefs.DeleteKey(key); }
        }
    }
}
