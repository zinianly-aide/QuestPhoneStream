using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream;

namespace QuestPhoneStream.Tests
{
    public sealed class QuestForegroundConnectionGuardTests
    {
        GameObject _host;
        int _oldPref;
        bool _hadPref;
        int _oldTimeout;
        [SetUp]
        public void Setup()
        {
            _hadPref = PlayerPrefs.HasKey(QuestForegroundConnectionGuard.PreferenceKey);
            _oldPref = PlayerPrefs.GetInt(QuestForegroundConnectionGuard.PreferenceKey, 0);
            PlayerPrefs.DeleteKey(QuestForegroundConnectionGuard.PreferenceKey);
            PlayerPrefs.Save();
            _oldTimeout = Screen.sleepTimeout;
            _host = new GameObject("GuardTestHost");
        }
        [TearDown]
        public void Teardown()
        {
            if (_host) Object.DestroyImmediate(_host);
            Screen.sleepTimeout = _oldTimeout;
            if (_hadPref) PlayerPrefs.SetInt(QuestForegroundConnectionGuard.PreferenceKey, _oldPref);
            else PlayerPrefs.DeleteKey(QuestForegroundConnectionGuard.PreferenceKey);
            PlayerPrefs.Save();
        }
        [Test]
        public void OptInOnlyWhileActiveAndReleaseWhenPaused()
        {
            var guard = _host.AddComponent<QuestForegroundConnectionGuard>();
            Assert.IsFalse(guard.Enabled);
            Assert.IsFalse(guard.IsActive);
            guard.SetEnabled(true);
            Assert.IsTrue(guard.Enabled);
            Assert.IsTrue(guard.IsActive);
            Assert.AreEqual(1, PlayerPrefs.GetInt(QuestForegroundConnectionGuard.PreferenceKey));
            guard.SendMessage("OnApplicationPause", true);
            Assert.IsFalse(guard.IsActive);
            StringAssert.Contains("Paused",guard.State);
            guard.SendMessage("OnApplicationPause", false);
            Assert.IsTrue(guard.IsActive);
            guard.SetEnabled(false);
            Assert.IsFalse(guard.Enabled);
            Assert.IsFalse(guard.IsActive);
            Assert.AreEqual(0,PlayerPrefs.GetInt(QuestForegroundConnectionGuard.PreferenceKey));
        }
        [Test]
        public void DisablingComponentDoesNotLeaveGuardActive()
        {
            var guard = _host.AddComponent<QuestForegroundConnectionGuard>();
            guard.SetEnabled(true);
            guard.enabled=false;
            Assert.IsFalse(guard.IsActive);
            guard.enabled=true;
            Assert.IsTrue(guard.IsActive);
            guard.SetEnabled(false);
        }
    }
}
