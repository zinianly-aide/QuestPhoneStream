using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream.Tests
{
    public sealed class SettingsPersistenceTests
    {
        static readonly string[] Keys = {
            "QuestPhoneStream_SignalingUrl_v2", "QuestPhoneStream_Token",
            "QuestPhoneStream_QuestDeviceId", "QuestPhoneStream_AndroidDeviceId",
            "QuestPhoneStream_SessionId", "QuestPhoneStream_MediaBaseUrl"
        };
        readonly Dictionary<string, string> _previous = new Dictionary<string, string>();
        readonly HashSet<string> _had = new HashSet<string>();
        GameObject _host, _cameraObject;
        SettingsUI _settings;

        [SetUp]
        public void Setup()
        {
            foreach (var key in Keys)
            {
                if (PlayerPrefs.HasKey(key)) _had.Add(key);
                _previous[key] = PlayerPrefs.GetString(key, "");
            }
            _host = new GameObject("SettingsPersistenceTest");
            var signaling = _host.AddComponent<QuestSignalingClient>();
            _cameraObject = new GameObject("TestCamera");
            var camera = _cameraObject.AddComponent<Camera>();
            _settings = _host.AddComponent<SettingsUIFactory>().Initialize(signaling, camera);
        }

        [TearDown]
        public void Cleanup()
        {
            if (_host) UnityEngine.Object.DestroyImmediate(_host);
            if (_cameraObject) UnityEngine.Object.DestroyImmediate(_cameraObject);
            foreach (var key in Keys)
                if (_had.Contains(key)) PlayerPrefs.SetString(key, _previous[key]);
                else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Test]
        public void SaveLocallyWorksWithEmptySignalingEndpoint()
        {
            _settings.signalingUrlInput.text = "";
            _settings.androidDeviceIdInput.text = "android-after-restart-test";
            _settings.questDeviceIdInput.text = "quest-after-restart-test";
            _settings.saveButton.onClick.Invoke();
            Assert.AreEqual("", PlayerPrefs.GetString("QuestPhoneStream_SignalingUrl_v2", "not-saved"));
            Assert.AreEqual("android-after-restart-test",
                PlayerPrefs.GetString("QuestPhoneStream_AndroidDeviceId", "missing"));
            Assert.AreEqual("quest-after-restart-test",
                PlayerPrefs.GetString("QuestPhoneStream_QuestDeviceId", "missing"));
            StringAssert.Contains("saved locally", _settings.statusText.text);
        }

        [Test]
        public void EndEditPersistsValidChangeButDoesNotReplaceUrlWithInvalidText()
        {
            PlayerPrefs.SetString("QuestPhoneStream_SignalingUrl_v2", "ws://127.0.0.1:8765");
            _settings.questDeviceIdInput.text = "quest-persist-edited";
            _settings.questDeviceIdInput.onEndEdit.Invoke(_settings.questDeviceIdInput.text);
            Assert.AreEqual("quest-persist-edited",
                PlayerPrefs.GetString("QuestPhoneStream_QuestDeviceId"));
            _settings.signalingUrlInput.text = "not-a-websocket";
            _settings.signalingUrlInput.onEndEdit.Invoke(_settings.signalingUrlInput.text);
            Assert.AreEqual("ws://127.0.0.1:8765",
                PlayerPrefs.GetString("QuestPhoneStream_SignalingUrl_v2"));
            Assert.IsFalse(SettingsUI.CanSaveLocalSignalingUrl("http://127.0.0.1:8000"));
            Assert.IsTrue(SettingsUI.CanSaveLocalSignalingUrl(" "));
            Assert.IsTrue(SettingsUI.CanSaveLocalSignalingUrl("wss://example.invalid/ws"));
        }

        [Test]
        public void BackgroundingFlushesValidDraftWithoutConnecting()
        {
            _settings.androidDeviceIdInput.text = "android-suspended-test";
            _settings.signalingUrlInput.text = "";
            _settings.gameObject.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            Assert.AreEqual("android-suspended-test", PlayerPrefs.GetString("QuestPhoneStream_AndroidDeviceId"));
        }
    }
}
