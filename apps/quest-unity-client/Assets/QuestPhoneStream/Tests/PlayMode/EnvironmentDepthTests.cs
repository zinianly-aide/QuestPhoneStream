using NUnit.Framework;
using UnityEngine;

namespace QuestPhoneStream.Tests
{
    public sealed class EnvironmentDepthTests
    {
        [TestCase(false, false, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, true)]
        public void CapabilityGateRequiresAvailabilityAuthorizationAndRequest(bool available, bool authorized, bool requested, bool expected)
        {
            Assert.AreEqual(expected, EnvironmentDepthCapabilityGate.CanActivate(available, authorized, requested));
        }

        [Test]
        public void QuestRegistryDoesNotPretendDepthExistsWithoutProvider()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            var capability = System.Array.Find(registry.All(), item => item.name == "spatial.environment.depth");
            Assert.NotNull(capability);
            Assert.IsFalse(capability.state.available);
            Assert.IsFalse(capability.state.authorized);
            Assert.IsFalse(capability.state.active);
            CollectionAssert.Contains(capability.features, "local-texture");
            CollectionAssert.Contains(capability.features, "metadata-sample");
        }

        [Test]
        public void DepthServiceCannotBeDiscoveredAsItsOwnProvider()
        {
            Assert.IsFalse(MetaEnvironmentDepthProvider.IsProviderTypeForDiscovery(typeof(QuestEnvironmentDepthService)));
        }

        [Test]
        public void AddingDepthServiceDoesNotProbeProviderDuringAwake()
        {
            OptionalProviderDiscovery.ResetAll();
            var go = new GameObject("EnvironmentDepthLazyDiscoveryTest");
            try
            {
                var service = go.AddComponent<QuestEnvironmentDepthService>();
                Assert.IsFalse(service.IsAvailable);
                Assert.AreEqual(0, OptionalProviderDiscovery.AssemblyScanCount,
                    "Quest startup/Awake must not scan optional provider assemblies");
            }
            finally
            {
                Object.DestroyImmediate(go);
                OptionalProviderDiscovery.ResetAll();
            }
        }
    }
}
