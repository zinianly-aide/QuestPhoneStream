using System.Collections;
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
    }
}
