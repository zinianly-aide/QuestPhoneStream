using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public class DeviceSelectConnectTests
    {
        [Test]
        public void SpatialPeerIsolation_RejectsStaleSourceAfterDeviceSwitch()
        {
            var oldAndroid = "android-old";
            var newAndroid = "android-new";
            var session = "sess-1";
            var stale = new SpatialEnvelope { type = "hello", source = oldAndroid, sessionId = session };
            Assert.IsFalse(SpatialPeerIsolation.Accept(stale, newAndroid, newAndroid, session));
            var fresh = new SpatialEnvelope { type = "hello", source = newAndroid, sessionId = session };
            Assert.IsTrue(SpatialPeerIsolation.Accept(fresh, newAndroid, newAndroid, session));
        }

        [Test]
        public void SpatialPeerIsolation_RejectsStaleSession()
        {
            var android = "android-1";
            var msg = new SpatialEnvelope { type = "capabilities", source = android, sessionId = "old-session" };
            Assert.IsFalse(SpatialPeerIsolation.Accept(msg, android, android, "new-session"));
        }

        [Test]
        public void SpatialPeerIsolation_AcceptsWhenSessionEmpty()
        {
            var android = "android-1";
            var msg = new SpatialEnvelope { type = "hello", source = android, sessionId = "" };
            Assert.IsTrue(SpatialPeerIsolation.Accept(msg, android, android, ""));
        }

        [Test]
        public void BuildBaseUrl_NormalisesIpv4()
        {
            Assert.AreEqual("http://192.168.1.10:8788", MediaDeviceDiscovery.BuildBaseUrl("192.168.1.10", 8788));
        }

        [Test]
        public void BuildBaseUrl_BracketsIpv6()
        {
            Assert.AreEqual("http://[fe80::1234]:8788", MediaDeviceDiscovery.BuildBaseUrl("fe80::1234", 8788));
            Assert.AreEqual("http://[fe80::1234]:8788", MediaDeviceDiscovery.BuildBaseUrl("[fe80::1234]", 8788));
        }

        [Test]
        public void BuildBaseUrl_ReturnsEmptyForInvalidInput()
        {
            Assert.AreEqual("", MediaDeviceDiscovery.BuildBaseUrl("", 0));
            Assert.AreEqual("", MediaDeviceDiscovery.BuildBaseUrl("host", 0));
        }

        [Test]
        public void ShouldAcceptResolvedCallback_RequiresBothCurrentAndActive()
        {
            Assert.IsTrue(MediaDeviceDiscovery.ShouldAcceptResolvedCallback(true, true));
            Assert.IsFalse(MediaDeviceDiscovery.ShouldAcceptResolvedCallback(false, true));
            Assert.IsFalse(MediaDeviceDiscovery.ShouldAcceptResolvedCallback(true, false));
            Assert.IsFalse(MediaDeviceDiscovery.ShouldAcceptResolvedCallback(false, false));
        }

        [Test]
        public void HasCapability_ParsesCommaSeparatedCaseInsensitiveValues()
        {
            var device = new MediaDeviceInfo(capabilities: "media,Screen,control");
            Assert.IsTrue(device.HasCapability("media"));
            Assert.IsTrue(device.HasCapability("screen"));
            Assert.IsTrue(device.HasCapability("CONTROL"));
            Assert.IsFalse(device.HasCapability("spatial"));
        }

        [Test]
        public void DiscoveryServiceTypes_IncludesUnifiedAndLegacy()
        {
            Assert.AreEqual("_qps-device._tcp.", MediaDeviceDiscovery.UnifiedServiceType);
            Assert.AreEqual("_qps-media._tcp.", MediaDeviceDiscovery.LegacyServiceType);
            CollectionAssert.Contains(MediaDeviceDiscovery.DiscoveryServiceTypes, MediaDeviceDiscovery.UnifiedServiceType);
            CollectionAssert.Contains(MediaDeviceDiscovery.DiscoveryServiceTypes, MediaDeviceDiscovery.LegacyServiceType);
        }

        [Test]
        public void SignalingEndpointResolution_PrefersPersistedThenDiscoveredThenManual()
        {
            Assert.AreEqual("ws://persisted:8787", QuestSignalingClient.ResolveSignalingEndpoint(
                " ws://persisted:8787 ", "ws://discovered:8787", "ws://manual:8787"));
            Assert.AreEqual("ws://discovered:8787", QuestSignalingClient.ResolveSignalingEndpoint(
                "", " ws://discovered:8787 ", "ws://manual:8787"));
            Assert.AreEqual("ws://manual:8787", QuestSignalingClient.ResolveSignalingEndpoint(
                "not-an-endpoint", "", " ws://manual:8787 "));
            Assert.AreEqual("", QuestSignalingClient.ResolveSignalingEndpoint("", "", ""));
        }

        [Test]
        public void TargetChanged_NotifiesWithoutRequiringStateTransition()
        {
            var root = new UnityEngine.GameObject("target change test");
            try
            {
                var client = root.AddComponent<QuestSignalingClient>();
                var notifications = 0;
                client.TargetChanged += () => ++notifications;
                client.NotifyTargetChanged();
                Assert.AreEqual(1, notifications);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
