using System;
using NUnit.Framework;
using QuestPhoneStream;

namespace QuestPhoneStream.Tests
{
    public sealed class QuestPresencePublisherTests
    {
        [Test]
        public void PresenceAdvertisesDedicatedQuestRoleNotSystemAdb()
        {
            Assert.AreEqual("_qps-quest._tcp.", QuestPresencePublisher.ServiceType);
            Assert.AreEqual("quest", QuestPresencePublisher.Platform);
            Assert.AreEqual("scene", QuestPresencePublisher.Capabilities);
            Assert.IsFalse(QuestPresencePublisher.ServiceType.Contains("adb"));
        }
        [Test]
        public void PresenceIdRemainsStableAndDoesNotExposeHardwareIdentifiers()
        {
            var one=QuestPresencePublisher.StablePresenceId("");
            Assert.AreEqual(32, one.Length);
            Assert.AreEqual(one, QuestPresencePublisher.StablePresenceId(one));
            Assert.AreEqual(32, QuestPresencePublisher.StablePresenceId("invalid").Length);
            Assert.AreNotEqual(one, QuestPresencePublisher.StablePresenceId("invalid"));
        }
    }
}
