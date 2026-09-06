using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public sealed class QuestVisionTests
    {
        [TestCase(false, false, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, true)]
        public void CameraActivationRequiresAvailabilityAndPermission(bool available, bool authorized, bool requested, bool expected)
        {
            Assert.AreEqual(expected, QuestVisionPermissionGate.CanActivate(available, authorized, requested));
        }

        [Test]
        public void CameraCapabilityStartsUnavailableAndInactive()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            var camera = System.Array.Find(registry.All(), item => item.name == "camera.rgb");
            Assert.NotNull(camera);
            Assert.IsFalse(camera.state.available);
            Assert.IsFalse(camera.state.authorized);
            Assert.IsFalse(camera.state.active);
            Assert.Contains("horizonos.permission.HEADSET_CAMERA", camera.permissions);
        }

        [Test]
        public void CameraCapabilityCannotActivateBeforeAuthorization()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            registry.UpdateState("camera.rgb", available: true, authorized: false, active: true);
            var camera = System.Array.Find(registry.All(), item => item.name == "camera.rgb");
            Assert.IsTrue(camera.state.available);
            Assert.IsFalse(camera.state.authorized);
            Assert.IsFalse(camera.state.active);
        }
    }
}
