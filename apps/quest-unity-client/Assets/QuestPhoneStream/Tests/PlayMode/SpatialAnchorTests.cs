using NUnit.Framework;
using UnityEngine;

namespace QuestPhoneStream.Tests
{
    public sealed class SpatialAnchorTests
    {
        [Test]
        public void StoreCreatesUpdatesAndRemovesSessionLocalAnchor()
        {
            var store = new SpatialAnchorStore();
            var created = store.Create(new Vector3(1, 2, 3), Quaternion.identity, "anchor-1");
            Assert.AreEqual("anchor-1", created.id);
            Assert.IsFalse(created.persistent);
            Assert.AreEqual(-3f, created.position.z);
            Assert.AreEqual(1, store.Count);

            Assert.IsTrue(store.Update("anchor-1", new Vector3(4, 5, 6), Quaternion.Euler(0, 45, 0), out var updated));
            Assert.AreEqual(4f, updated.position.x);
            Assert.AreEqual(-6f, updated.position.z);
            Assert.GreaterOrEqual(updated.updatedAt, updated.createdAt);

            Assert.IsTrue(store.Remove("anchor-1", out var removed));
            Assert.AreEqual("anchor-1", removed.id);
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void QuestRegistryAdvertisesSessionLocalAnchorHonestly()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            var capability = System.Array.Find(registry.All(), item => item.name == "spatial.anchor");
            Assert.NotNull(capability);
            Assert.IsTrue(capability.state.available);
            Assert.IsTrue(capability.state.authorized);
            Assert.IsFalse(capability.state.active);
            CollectionAssert.Contains(capability.features, "session-local");
            CollectionAssert.DoesNotContain(capability.features, "persistent");
        }
    }
}
