using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void EncodeJpgProducesJpegFileBytes()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
                texture.Apply(false, false);
                var frame = new QuestVisionFrame { texture = texture, width = 2, height = 2 };
                var bytes = frame.EncodeJpg(85);

                Assert.That(bytes, Is.Not.Null.And.Length.GreaterThan(3));
                Assert.AreEqual(0xFF, bytes[0]);
                Assert.AreEqual(0xD8, bytes[1]);
                Assert.AreEqual(0xFF, bytes[2]);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
