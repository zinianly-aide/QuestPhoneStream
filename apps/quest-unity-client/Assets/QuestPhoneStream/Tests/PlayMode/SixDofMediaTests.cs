using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public sealed class SixDofMediaTests
    {
        [TestCase("6dof")]
        [TestCase("volumetric")]
        [TestCase("mpeg-vv")]
        [TestCase("v3c")]
        public void DescriptorRecognizesSupportedSpatialFormats(string format)
        {
            Assert.IsTrue(SixDofMediaDescriptor.IsSixDofFormat(format));
        }

        [Test]
        public void FlatMediaIsNotMisclassifiedAsSixDof()
        {
            var item = new MediaItemDto { id = "flat", projection = "flat", spatialFormat = null };
            Assert.IsFalse(item.IsSixDof);
            Assert.IsFalse(SixDofMediaDescriptor.From(item).IsSixDof);
        }

        [Test]
        public void RegistryKeepsSixDofUnavailableUntilRendererProviderExists()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            var capability = System.Array.Find(registry.All(), item => item.name == "media.6dof.render");
            Assert.NotNull(capability);
            Assert.IsFalse(capability.state.available);
            Assert.IsFalse(capability.state.authorized);
            Assert.IsFalse(capability.state.active);
            CollectionAssert.Contains(capability.features, "provider-adapter");
        }
    }
}
