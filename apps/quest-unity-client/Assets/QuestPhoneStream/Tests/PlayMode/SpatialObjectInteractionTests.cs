using System.Linq;
using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public sealed class SpatialObjectInteractionTests
    {
        [Test]
        public void TrackerEmitsHoverAndSelectionLifecycle()
        {
            var tracker = new SpatialInteractionTracker();
            CollectionAssert.AreEqual(new[] { "hover.enter" }, tracker.Update("right", "cube", false).ToArray());
            CollectionAssert.AreEqual(new[] { "select.start" }, tracker.Update("right", "cube", true).ToArray());
            CollectionAssert.AreEqual(new[] { "select.update" }, tracker.Update("right", "cube", true).ToArray());
            CollectionAssert.AreEqual(new[] { "select.end" }, tracker.Update("right", "cube", false).ToArray());
            CollectionAssert.AreEqual(new[] { "hover.exit" }, tracker.Update("right", null, false).ToArray());
        }

        [Test]
        public void RegistryExposesInteractionAsSeparateSpatialCapability()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            var capability = System.Array.Find(registry.All(), item => item.name == "spatial.object.interaction");
            Assert.NotNull(capability);
            Assert.IsTrue(capability.state.available);
            Assert.IsTrue(capability.state.authorized);
            Assert.IsFalse(capability.state.active);
            CollectionAssert.Contains(capability.features, "raycast");
            CollectionAssert.Contains(capability.features, "select");
        }
    }
}
