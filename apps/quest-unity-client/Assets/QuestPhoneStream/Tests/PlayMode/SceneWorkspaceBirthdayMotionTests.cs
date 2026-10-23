using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceBirthdayMotionTests
    {
        GameObject _room;
        static readonly string[] Furniture = {
            "EntranceShoeCabinet", "DiningSideboard", "Move_Dining_Table",
            "Move_Sofa", "Move_Coffee_Table"
        };

        [SetUp]
        public void Setup()
        {
            _room = new GameObject("BirthdayMotionQA");
            for (int i = 0; i < Furniture.Length; i++)
            {
                var anchor = new GameObject(Furniture[i]);
                anchor.transform.SetParent(_room.transform, false);
                anchor.transform.position = new Vector3(i * 1.3f, 0f, -i * .4f);
                var workspace = anchor.AddComponent<SceneWorkspaceObject>();
                workspace.persistentId = "hefu-" + Furniture[i];
                workspace.editable = true;
                var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                proxy.transform.SetParent(anchor.transform, false);
                proxy.transform.localScale = new Vector3(1f, .8f, .8f);
            }
        }
        [TearDown]
        public void TearDown()
        {
            if (_room) Object.DestroyImmediate(_room);
        }

        [Test]
        public void GentleMotionChangesOnlyDecorationAndRestoresInitialPose()
        {
            var original = new Vector3[Furniture.Length];
            for (int i = 0; i < Furniture.Length; i++)
                original[i] = _room.transform.GetChild(i).position;
            var scene = SceneWorkspaceStore.Create("Birthday", "cad-bundle", "hefu/home.bundle");
            scene.decorationPreset = "birthday-v1";
            var decor = SceneWorkspaceBirthday.Build(_room.transform, scene);
            var motion = decor.GetComponent<BirthdayAmbientMotion>();
            Assert.IsNotNull(motion);
            Assert.AreEqual(3, motion.ClusterCount);
            Assert.AreEqual(2, motion.GlowCount);
            Assert.AreEqual(121, decor.DecorationCount);
            Transform cluster = null;
            foreach (var tr in decor.transform.parent.GetComponentsInChildren<Transform>(true))
                if (tr.name == "Birthday_AnimatedBalloonCluster_0") { cluster = tr; break; }
            Assert.IsNotNull(cluster);
            Vector3 initial = cluster.localPosition;
            Quaternion initialRot = cluster.localRotation;
            var strings = cluster.GetComponentInChildren<MeshFilter>();
            Assert.IsNotNull(strings);
            bool containsBalloons = false, containsStrings = false;
            foreach (var tr in cluster.GetComponentsInChildren<Transform>())
            {
                if (tr.name.StartsWith("Balloons_")) containsBalloons = true;
                if (tr.name.Contains("BalloonSatinStrings")) containsStrings = true;
            }
            Assert.IsTrue(containsBalloons && containsStrings, "Balloons and strings should sway together");
            motion.Tick(1.25f);
            Assert.Greater(Vector3.Distance(initial, cluster.localPosition), .002f);
            Assert.Less(Vector3.Distance(initial, cluster.localPosition), .020f,
                "Motion must remain subtle, no collision or clipping changes");
            motion.SetMotionEnabled(false);
            Assert.That(cluster.localPosition, Is.EqualTo(initial));
            Assert.That(cluster.localRotation, Is.EqualTo(initialRot));
            motion.Tick(5.4f);
            Assert.That(cluster.localPosition, Is.EqualTo(initial));
            motion.SetMotionEnabled(true);
            motion.Tick(4.35f);
            Assert.Greater(Vector3.Distance(initial, cluster.localPosition), .002f);
            foreach (var light in decor.GetComponentsInChildren<Light>(true))
                Assert.IsFalse(light.shadows != LightShadows.None,
                    "Added ambience must not use shadow-casting lights");
            decor.ToggleLights();
            foreach (var light in decor.GetComponentsInChildren<Light>(true))
                Assert.IsFalse(light.enabled, "User light-off preference takes priority over animation");
            for (int i = 0; i < Furniture.Length; i++)
                Assert.AreEqual(original[i], _room.transform.GetChild(i).position);
        }
    }
}
