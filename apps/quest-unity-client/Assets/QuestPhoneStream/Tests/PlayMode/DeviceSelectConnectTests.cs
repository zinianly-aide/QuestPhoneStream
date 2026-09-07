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

        [Test]
        public void ActiveDeviceContext_StoresEveryEndpointForTheSelectedDevice()
        {
            var context = ActiveDeviceContext.FromDiscovered(new MediaDeviceInfo(
                deviceId: "pixel-10", name: "Pixel 10", host: "192.168.1.10", port: 8788,
                capabilities: "screen,media,control", streamId: "android-pixel",
                signalingUrl: "ws://192.168.1.10:8787", isReady: true));

            Assert.AreEqual("pixel-10", context.DeviceId);
            Assert.AreEqual("android-pixel", context.StreamId);
            Assert.AreEqual("ws://192.168.1.10:8787", context.SignalingUrl);
            Assert.AreEqual("http://192.168.1.10:8788", context.MediaBaseUrl);
            Assert.IsTrue(context.Capabilities.Supports("display.publish"));
            Assert.IsTrue(context.Capabilities.Supports("media.list"));
            Assert.IsTrue(context.Capabilities.Supports("display.control"));
        }

        [Test]
        public void ActiveDeviceContext_NsdCapsAreBootstrapHints()
        {
            var context = ActiveDeviceContext.FromDiscovered(new MediaDeviceInfo(capabilities: "screen,control", isReady: true));
            Assert.IsFalse(context.Capabilities.HasSpatialCapabilities);
            Assert.IsTrue(context.Capabilities.Supports("display.publish"));
            Assert.IsFalse(context.Capabilities.Supports("media.open"));
        }

        [Test]
        public void ActiveDeviceContext_SpatialCapabilitiesOverrideNsdBootstrap()
        {
            var context = ActiveDeviceContext.FromDiscovered(new MediaDeviceInfo(capabilities: "screen,media,control", isReady: true));
            context.Capabilities.ApplySpatial(new[] {
                Capability("display.publish", available: false, authorized: false),
                Capability("media.open", available: true, authorized: true)
            });

            Assert.IsTrue(context.Capabilities.HasSpatialCapabilities);
            Assert.IsFalse(context.Capabilities.Supports("display.publish"));
            Assert.IsTrue(context.Capabilities.Supports("media.open"));
            Assert.IsFalse(context.Capabilities.Supports("display.control"));
        }

        [Test]
        public void ActiveDeviceContext_RejectsOldPeerAfterQuickDeviceSwitch()
        {
            var deviceA = ActiveDeviceContext.FromDiscovered(new MediaDeviceInfo(deviceId: "a", streamId: "android-a", isReady: true));
            var deviceB = ActiveDeviceContext.FromDiscovered(new MediaDeviceInfo(deviceId: "b", streamId: "android-b", isReady: true));

            Assert.IsFalse(deviceB.MatchesPeer("android-a"));
            Assert.IsTrue(deviceB.MatchesPeer("android-b"));
            Assert.IsTrue(deviceA.MatchesPeer("a"));
        }

        [Test]
        public void ActiveDeviceContext_LostDeviceRetainsIdentityButBecomesUnavailable()
        {
            var context = ActiveDeviceContext.FromDiscovered(new MediaDeviceInfo(
                deviceId: "pixel", streamId: "android-pixel", capabilities: "media", isReady: true));
            context.MarkLost();

            Assert.AreEqual("pixel", context.DeviceId);
            Assert.IsTrue(context.IsLost);
        }

        [Test]
        public void ActiveDeviceContext_UpdatesSameDeviceWithoutReplacingSpatialCapabilities()
        {
            var context = ActiveDeviceContext.FromDiscovered(new MediaDeviceInfo(
                deviceId: "pixel", host: "192.168.1.2", port: 8788, capabilities: "screen", isReady: true));
            context.Capabilities.ApplySpatial(new[] { Capability("media.list", available: true, authorized: true) });
            context.UpdateFromDiscovery(new MediaDeviceInfo(
                deviceId: "pixel", host: "192.168.1.3", port: 8788, capabilities: "screen", isReady: true));

            Assert.AreEqual("http://192.168.1.3:8788", context.MediaBaseUrl);
            Assert.IsTrue(context.Capabilities.Supports("media.list"));
            Assert.IsFalse(context.Capabilities.Supports("display.publish"));
        }

        private static SpatialCapabilityDescriptor Capability(string name, bool available, bool authorized) =>
            new SpatialCapabilityDescriptor
            {
                name = name,
                state = new SpatialCapabilityState { available = available, authorized = authorized, active = false }
            };
    }
}
