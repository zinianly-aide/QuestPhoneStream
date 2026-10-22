using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceLocomotionTests
    {
        const string Prefix = "qps.space.locomotion.s2.";
        GameObject _host;
        [SetUp]
        public void Setup()
        {
            foreach (var key in new [] { "left", "right", "speed", "snap" })
                PlayerPrefs.DeleteKey(Prefix + key);
            _host = new GameObject("S2 Locomotion Tests");
        }
        [TearDown]
        public void Cleanup()
        {
            if (_host) Object.DestroyImmediate(_host);
            foreach (var key in new [] { "left", "right", "speed", "snap" })
                PlayerPrefs.DeleteKey(Prefix + key);
        }
        [Test]
        public void StickSettingsPersistAcrossInstances()
        {
            var config = _host.AddComponent<SceneWorkspaceLocomotion>();
            Assert.AreEqual(SceneWorkspaceLocomotion.StickMode.Move, config.LeftMode);
            Assert.AreEqual(SceneWorkspaceLocomotion.StickMode.SnapTurn, config.RightMode);
            config.Configure(true, SceneWorkspaceLocomotion.StickMode.SmoothTurn);
            config.Configure(false, SceneWorkspaceLocomotion.StickMode.Off);
            config.ConfigureSpeed(1.8f);
            config.ConfigureSnap(45);
            Object.DestroyImmediate(config);
            var restored = _host.AddComponent<SceneWorkspaceLocomotion>();
            Assert.AreEqual(SceneWorkspaceLocomotion.StickMode.SmoothTurn, restored.LeftMode);
            Assert.AreEqual(SceneWorkspaceLocomotion.StickMode.Off, restored.RightMode);
            Assert.AreEqual(1.8f, restored.MoveSpeed, .001f);
            Assert.AreEqual(45, restored.SnapDegrees);
        }
        [Test]
        public void DeadzoneAndSpeedClampsAvoidAccidentalMovement()
        {
            Assert.AreEqual(Vector2.zero, SceneWorkspaceLocomotion.Deadzone(new Vector2(.1f, .1f)));
            Assert.AreEqual(Vector2.zero, SceneWorkspaceLocomotion.Deadzone(Vector2.zero));
            Assert.AreEqual(1f, SceneWorkspaceLocomotion.Deadzone(new Vector2(1f, 0f)).x, .001f);
            var config = _host.AddComponent<SceneWorkspaceLocomotion>();
            config.ConfigureSpeed(30f);
            Assert.AreEqual(2f, config.MoveSpeed);
            config.ConfigureSpeed(-4f);
            Assert.AreEqual(.5f, config.MoveSpeed);
            config.ConfigureSnap(90);
            Assert.AreEqual(30, config.SnapDegrees);
        }
    }
}
