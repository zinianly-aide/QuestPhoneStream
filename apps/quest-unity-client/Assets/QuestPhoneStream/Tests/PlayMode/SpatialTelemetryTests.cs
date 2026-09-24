using NUnit.Framework;
using UnityEngine;

namespace QuestPhoneStream.Tests
{
    public sealed class SpatialTelemetryTests
    {
        [Test]
        public void SubscriptionBookSupportsLifecycle()
        {
            var book = new SpatialSubscriptionBook();
            var item = new SpatialTelemetrySubscription { id = "sub-1", capability = "xr.head.pose", rateHz = 60f };
            Assert.IsTrue(book.Add(item));
            Assert.IsFalse(book.Add(item));
            Assert.AreEqual(1, book.Count);
            Assert.IsTrue(book.ContainsCapability("xr.head.pose"));
            Assert.AreEqual(60f, book.HighestRate());
            Assert.IsTrue(book.Remove("sub-1", out var removed));
            Assert.AreSame(item, removed);
            Assert.AreEqual(0, book.Count);
            Assert.IsFalse(book.Remove("sub-1", out _));
        }

        [Test]
        public void SequenceGateDropsDuplicateAndStaleFrames()
        {
            var gate = new SpatialSequenceGate();
            Assert.IsTrue(gate.Accept("pose", 1));
            Assert.IsFalse(gate.Accept("pose", 1));
            Assert.IsFalse(gate.Accept("pose", 0));
            Assert.IsTrue(gate.Accept("pose", 2));
            Assert.IsTrue(gate.Accept("other", 0));
        }

        [Test]
        public void TelemetryPacketRoundTripsRequiredPoseFields()
        {
            var packet = new SpatialTelemetryPacket
            {
                capability = "xr.head.pose",
                streamId = "stream-1",
                sequence = 42,
                timestamp = 123456,
                space = "local",
                position = new SpatialVector3 { x = 1, y = 2, z = 3 },
                orientation = new SpatialQuaternion { x = 0, y = 0, z = 0, w = 1 }
            };
            var decoded = SpatialTelemetryPacket.FromJson(packet.ToJson());
            Assert.AreEqual("stream-1", decoded.streamId);
            Assert.AreEqual(42, decoded.sequence);
            Assert.AreEqual("local", decoded.space);
            Assert.AreEqual(3f, decoded.position.z);
            Assert.AreEqual(1f, decoded.orientation.w);
        }

        [Test]
        public void CoordinateConverterUsesSpatialV1Handedness()
        {
            var position = SpatialCoordinateConverter.ToCanonicalPosition(new Vector3(1, 2, 3));
            Assert.AreEqual(1f, position.x);
            Assert.AreEqual(2f, position.y);
            Assert.AreEqual(-3f, position.z);

            var input = Quaternion.Euler(10, 20, 30).normalized;
            var rotation = SpatialCoordinateConverter.ToCanonicalRotation(input);
            Assert.That(rotation.x, Is.EqualTo(-input.x).Within(0.0001f));
            Assert.That(rotation.y, Is.EqualTo(-input.y).Within(0.0001f));
            Assert.That(rotation.z, Is.EqualTo(input.z).Within(0.0001f));
            Assert.That(rotation.w, Is.EqualTo(input.w).Within(0.0001f));
        }

        [Test]
        public void RequestedRateNegotiatesToSupportedDisplayRates()
        {
            Assert.AreEqual(60f, SpatialTelemetryService.NormalizeRate(60f));
            Assert.AreEqual(60f, SpatialTelemetryService.NormalizeRate(65f));
            Assert.AreEqual(72f, SpatialTelemetryService.NormalizeRate(67f));
            Assert.AreEqual(72f, SpatialTelemetryService.NormalizeRate(72f));
        }

        [Test]
        public void CapabilityStateCannotStayActiveWhenUnavailable()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            Assert.IsTrue(registry.UpdateState("xr.head.pose", available: false, authorized: true, active: true));
            var capability = System.Array.Find(registry.All(), item => item.name == "xr.head.pose");
            Assert.IsFalse(capability.state.available);
            Assert.IsTrue(capability.state.authorized);
            Assert.IsFalse(capability.state.active);
        }
    }
}
