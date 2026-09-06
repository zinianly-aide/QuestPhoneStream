using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream.Tests
{
    public class SettingsUiTests
    {
        private GameObject _root, _cameraObject;
        private QuestSignalingClient _client;
        private Camera _camera;
        private SettingsUI _ui;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Settings test");
            _client = _root.AddComponent<QuestSignalingClient>();
            _cameraObject = new GameObject("Explicit untagged camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.transform.position = new Vector3(0, 1.6f, 0);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            Object.Destroy(_cameraObject);
            yield return null;
        }

        private SettingsUI CreateUi() =>
            _ui = _root.AddComponent<SettingsUIFactory>().Initialize(_client, _camera);

        [UnityTest]
        public IEnumerator VisibilitySurvivesStartAndRepeatedOpen()
        {
            var ui = CreateUi();
            Assert.IsFalse(ui.IsVisible);
            ui.Show();
            Assert.IsTrue(ui.IsVisible);
            yield return null;
            Assert.IsTrue(ui.IsVisible, "Start must not hide a requested menu");
            for (var i = 0; i < 10; ++i)
            {
                ui.Hide(); Assert.IsFalse(ui.IsVisible);
                ui.Show(); Assert.IsTrue(ui.IsVisible);
                ui.Toggle(); Assert.IsFalse(ui.IsVisible);
                ui.Toggle(); Assert.IsTrue(ui.IsVisible);
            }
            ui.canvas.gameObject.SetActive(false);
            Assert.IsFalse(ui.IsVisible, "Visibility must reflect the actual canvas");
        }

        [Test]
        public void FactoryDoesNotCreateUiBeforeDependenciesArrive()
        {
            var factory = _root.AddComponent<SettingsUIFactory>();
            Assert.IsNull(_root.GetComponent<SettingsUI>());
            var ui = factory.Initialize(_client, _camera);
            Assert.AreSame(_client, ui.signalingClient);
            Assert.AreSame(_camera, ui.canvas.worldCamera);
            Assert.AreSame(ui, factory.Initialize(_client, _camera));
            Assert.AreEqual(1, _root.GetComponents<SettingsUI>().Length);
            Assert.Throws<System.ArgumentException>(() => factory.Initialize(_client, null));
        }

        [UnityTest]
        public IEnumerator LayoutHasPositiveTextAreasAndReadableDefaultText()
        {
            var ui = CreateUi();
            // Keep this geometry test independent of a developer's saved device settings.
            ui.signalingUrlInput.text = "ws://192.168.1.11:8787";
            ui.tokenInput.text = "dev-token";
            ui.questDeviceIdInput.text = "quest-3s-001";
            ui.androidDeviceIdInput.text = "android-phone-001";
            ui.sessionIdInput.text = "local-session-001";
            ui.Show();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)ui.canvas.transform;
            Assert.AreEqual(new Vector2(1000, 750), rect.sizeDelta);
            Assert.AreEqual(2f, rect.rect.width * rect.lossyScale.x, 0.01f);
            Assert.AreEqual(1.5f, rect.rect.height * rect.lossyScale.y, 0.01f);
            Assert.IsNotNull(ui.canvas.GetComponent<TrackedDeviceGraphicRaycaster>());
            foreach (var input in ui.GetComponentsInChildren<InputField>(true))
            {
                Assert.IsNotNull(input.GetComponent<QuestKeyboardInputField>());
                Assert.IsFalse(input.shouldHideMobileInput);
                var textRect = input.textComponent.rectTransform.rect;
                Assert.Greater(textRect.width, 0);
                Assert.Greater(textRect.height, input.textComponent.fontSize);
                Assert.Greater(textRect.width, input.textComponent.preferredWidth);
            }
            foreach (var button in ui.GetComponentsInChildren<Button>(true))
            {
                var text = button.GetComponentInChildren<Text>();
                Assert.GreaterOrEqual(text.rectTransform.rect.width, text.preferredWidth);
            }
            foreach (var text in ui.GetComponentsInChildren<Text>(true))
                if (text.gameObject.name == "Label")
                    Assert.GreaterOrEqual(text.rectTransform.rect.width, text.preferredWidth);
        }

        [UnityTest]
        public IEnumerator MissingCameraReportsOnceWithoutOpening()
        {
            var ui = CreateUi();
            Object.Destroy(_cameraObject);
            yield return null;
            LogAssert.Expect(LogType.Error, "[QuestPhoneStream] Settings XR camera is missing");
            ui.Show();
            ui.Show();
            Assert.IsFalse(ui.IsVisible);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MenuStaysInWorldAndRepositionsOnNextShow()
        {
            var ui = CreateUi();
            ui.Show();
            var initial = ui.canvas.transform.position;
            _camera.transform.position += Vector3.right * 3;
            _camera.transform.rotation = Quaternion.Euler(25, 90, 20);
            yield return null;
            Assert.AreEqual(initial, ui.canvas.transform.position);
            ui.Hide(); ui.Show();
            Assert.AreNotEqual(initial, ui.canvas.transform.position);
            Assert.Less(Vector3.Angle(ui.canvas.transform.up, Vector3.up), 0.01f);
        }

        [UnityTest]
        public IEnumerator HomeKeepsNavigationAndAdvancedSettingsInViewport()
        {
            var receiver = _root.AddComponent<QuestWebRtcReceiver>();
            receiver.enabled = false;
            receiver.signaling = _client;
            receiver.xrCamera = _camera;
            var home = _root.AddComponent<QuestHomeUI>();
            home.Initialize(_client, _camera, receiver);
            Canvas.ForceUpdateCanvases();

            Button advanced = null, phone = null, videos = null, keyboard = null;
            foreach (var button in home.GetComponentsInChildren<Button>(true))
            {
                var text = button.GetComponentInChildren<Text>();
                if (text == null) continue;
                if (text.text == "Advanced Settings") advanced = button;
                if (text.text == "Phone") phone = button;
                if (text.text == "Videos") videos = button;
                if (text.text == "Keyboard") keyboard = button;
            }

            Assert.IsNotNull(advanced);
            Assert.IsNotNull(phone);
            Assert.IsNotNull(videos);
            Assert.IsNotNull(keyboard);
            var viewport = _camera.WorldToViewportPoint(advanced.transform.position);
            Assert.Greater(viewport.z, 0f);
            Assert.That(viewport.x, Is.InRange(0.1f, 0.9f));
            Assert.That(viewport.y, Is.InRange(0.15f, 0.85f));

            var canvas = home.GetComponentInChildren<Canvas>(true);
            Assert.AreEqual(new Vector2(900f, 500f), ((RectTransform)canvas.transform).sizeDelta);
            Assert.That(((RectTransform)canvas.transform).lossyScale.x, Is.EqualTo(0.0015f).Within(0.00001f));
            var panel = canvas.transform.Find("HomePanel");
            var list = panel.Find("MediaDeviceList") as RectTransform;
            Assert.IsNotNull(list);
            Assert.Less(list.anchorMax.y, advanced.GetComponent<RectTransform>().anchorMin.y);
            Assert.Less(advanced.GetComponent<RectTransform>().anchorMax.y, phone.GetComponent<RectTransform>().anchorMin.y);

            advanced.onClick.Invoke();
            var settingsObject = GameObject.Find("SettingsUI");
            var settings = settingsObject == null ? null : settingsObject.GetComponent<SettingsUI>();
            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.IsVisible);
            Assert.IsNotNull(settings.signalingUrlInput);
            Assert.IsNotNull(settings.tokenInput);
            Assert.IsNotNull(settings.questDeviceIdInput);
            Assert.IsNotNull(settings.androidDeviceIdInput);
            Assert.IsNotNull(settings.sessionIdInput);
            Assert.IsNotNull(settings.mediaBaseUrlInput);
            Assert.IsNotNull(settings.connectButton);
            yield return null;
        }

        [Test]
        public void PeerConnectedDoesNotHideVisibleAdvancedSettings()
        {
            var ui = CreateUi();
            ui.ShowAdvanced();
            Assert.IsTrue(ui.IsVisible);
            var method = typeof(SettingsUI).GetMethod("OnStateChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(ui, new object[] { ConnectionState.PeerConnected });
            Assert.IsTrue(ui.IsVisible);
        }

        [Test]
        public void WirelessAdbHelperSelectsIpv4AndBuildsSafeCommand()
        {
            Assert.AreEqual("192.168.1.20", WirelessAdbHelper.SelectIpv4(new[] {
                "127.0.0.1", "invalid", "8.8.8.8", "192.168.1.20"
            }));
            Assert.AreEqual(string.Empty, WirelessAdbHelper.SelectIpv4(new[] { "127.0.0.1", "not-an-ip" }));
            Assert.AreEqual("adb connect 192.168.1.20:5555", WirelessAdbHelper.BuildConnectCommand("192.168.1.20"));
            Assert.AreEqual(string.Empty, WirelessAdbHelper.BuildConnectCommand("not-an-ip"));
        }

        [Test]
        public void WirelessAdbHelperMapsProbeStatesAndSettingsFallback()
        {
            Assert.AreEqual(WirelessAdbStatus.Unknown, WirelessAdbHelper.ProbePort(string.Empty));
            Assert.AreEqual("Listening", WirelessAdbHelper.StatusLabel(WirelessAdbStatus.Listening));
            Assert.AreEqual("Not listening", WirelessAdbHelper.StatusLabel(WirelessAdbStatus.NotListening));
            Assert.AreEqual("Unknown", WirelessAdbHelper.StatusLabel(WirelessAdbStatus.Unknown));

            var attempts = 0;
            Assert.IsFalse(WirelessAdbHelper.TryOpenSettings(action => {
                attempts++;
                throw new System.InvalidOperationException(action);
            }));
            Assert.AreEqual(2, attempts);
        }

        [Test]
        public void NsdLateResolveCannotPromoteLostService()
        {
            Assert.IsTrue(MediaDeviceDiscovery.ShouldAcceptResolvedCallback(true, true));
            Assert.IsFalse(MediaDeviceDiscovery.ShouldAcceptResolvedCallback(true, false));
            Assert.IsFalse(MediaDeviceDiscovery.ShouldAcceptResolvedCallback(false, true));
        }

        [Test]
        public void UnifiedDeviceCapabilitiesRemainDiscoverableWithoutSecrets()
        {
            var device = new MediaDeviceInfo { capabilities = "media,screen,control" };
            Assert.IsTrue(device.HasCapability("media"));
            Assert.IsTrue(device.HasCapability("screen"));
            Assert.IsTrue(device.HasCapability("control"));
            Assert.IsFalse(device.HasCapability("token"));
        }

        [UnityTest]
        public IEnumerator DeveloperToolsIsAnAdvancedSettingsChildPage()
        {
            var ui = CreateUi();
            Assert.IsTrue(WirelessAdbHelper.IsDeveloperToolsAvailable);
            Assert.IsNotNull(ui.developerToolsButton);
            Assert.IsNotNull(ui.wirelessAdbHelper);
            ui.developerToolsButton.onClick.Invoke();
            Assert.IsTrue(ui.IsVisible);
            Assert.IsTrue(ui.wirelessAdbHelper.IsVisible);
            ui.wirelessAdbHelper.Hide();
            ui.HideDeveloperTools();
            yield return null;
        }
    }
}
