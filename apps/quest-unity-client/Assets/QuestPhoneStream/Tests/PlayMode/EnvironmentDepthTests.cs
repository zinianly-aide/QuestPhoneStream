using NUnit.Framework;

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
    }
}
