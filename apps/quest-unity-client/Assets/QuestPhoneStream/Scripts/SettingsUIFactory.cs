using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream
{
    public sealed class SettingsUIFactory : MonoBehaviour
    {
        private Canvas _canvas;
        private SettingsUI _settingsUI;
        private MediaLibraryUI _mediaLibrary;

        public SettingsUI Initialize(QuestSignalingClient signalingClient, Camera xrCamera) =>
            Initialize(signalingClient, xrCamera, null, null);

        public SettingsUI Initialize(QuestSignalingClient signalingClient, Camera xrCamera, MediaPlaybackController playback = null, QuestWebRtcReceiver receiver = null)
        {
            if (signalingClient == null || xrCamera == null)
                throw new System.ArgumentException("Settings UI requires signaling and XR camera dependencies");
            if (_settingsUI != null) return _settingsUI;
            CreateUI(signalingClient, xrCamera, playback, receiver);
            return _settingsUI;
        }

        private void CreateUI(QuestSignalingClient signalingClient, Camera xrCamera, MediaPlaybackController playback, QuestWebRtcReceiver receiver)
        {
            var canvasGo = new GameObject("SettingsCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.SetActive(false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;
            _canvas.worldCamera = xrCamera;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1;

            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000, 750);
            rt.localScale = Vector3.one * 0.002f;
            rt.localRotation = Quaternion.identity;

            var panel = CreatePanel(canvasGo.transform);
            CreatePanelTitle(panel, "Advanced Settings");

            _settingsUI = gameObject.AddComponent<SettingsUI>();
            _settingsUI.canvas = _canvas;
            CreateBackButton(panel, out var backButton);
            _settingsUI.backButton = backButton;

            CreateInputField(panel, "Signaling URL:", "QuestPhoneStream_SignalingUrl_v2", "ws://192.168.1.9:8787", 0, out var urlInput);
            _settingsUI.signalingUrlInput = urlInput;

            CreateInputField(panel, "Token:", "QuestPhoneStream_Token", "dev-token", 1, out var tokenInput);
            _settingsUI.tokenInput = tokenInput;
            tokenInput.contentType = InputField.ContentType.Password;

            CreateInputField(panel, "Quest Device ID:", "QuestPhoneStream_QuestDeviceId", "quest-3s-001", 2, out var questIdInput);
            _settingsUI.questDeviceIdInput = questIdInput;

            CreateInputField(panel, "Android Device ID:", "QuestPhoneStream_AndroidDeviceId", "android-phone-001", 3, out var androidIdInput);
            _settingsUI.androidDeviceIdInput = androidIdInput;

            CreateInputField(panel, "Session ID:", "QuestPhoneStream_SessionId", "local-session-001", 4, out var sessionIdInput);
            _settingsUI.sessionIdInput = sessionIdInput;

            CreateInputField(panel, "Media HTTP URL:", "QuestPhoneStream_MediaBaseUrl", "", 5, out var mediaUrlInput);
            _settingsUI.mediaBaseUrlInput = mediaUrlInput;

            CreateButton(panel, "Save", 6, 0.05f, out var saveBtn);
            _settingsUI.saveButton = saveBtn;

            CreateButton(panel, "Connect / Reconnect", 6, 0.52f, out var connectBtn);
            _settingsUI.connectButton = connectBtn;

            CreateButton(panel, "Phone Screen", 7, 0.05f, out var phoneBtn);
            CreateButton(panel, "Video Library", 7, 0.52f, out var videoBtn);
            _settingsUI.phoneScreenButton = phoneBtn;
            _settingsUI.videoLibraryButton = videoBtn;

            var statusRight = 0.95f;
#if QPS_DEV_TOOLS || DEVELOPMENT_BUILD || UNITY_EDITOR
            CreateButton(panel, "Developer Tools", 8, 0.52f, out var developerToolsButton);
            _settingsUI.developerToolsButton = developerToolsButton;

            var wirelessAdbHelper = gameObject.AddComponent<WirelessAdbHelper>();
            _settingsUI.wirelessAdbHelper = wirelessAdbHelper;
            wirelessAdbHelper.Initialize(_canvas, _settingsUI.HideDeveloperTools);

            var diagnostics = gameObject.GetComponent<QuestDiagnostics>() ?? gameObject.AddComponent<QuestDiagnostics>();
            diagnostics.Initialize(receiver);
            var p2Diagnostics = gameObject.GetComponent<QuestDeveloperHud>() ?? gameObject.AddComponent<QuestDeveloperHud>();
            p2Diagnostics.Initialize(receiver, signalingClient);
            var developerHud = gameObject.GetComponent<DeveloperHud>() ?? gameObject.AddComponent<DeveloperHud>();
            developerHud.Initialize(_canvas, diagnostics, p2Diagnostics, wirelessAdbHelper, _settingsUI.HideDeveloperTools);
            _settingsUI.developerHud = developerHud;
            statusRight = 0.48f;
#endif

            var catalogClient = gameObject.AddComponent<MediaCatalogClient>();
            catalogClient.SetPairingTokenProvider(() => _settingsUI.tokenInput.text);
            _mediaLibrary = gameObject.AddComponent<MediaLibraryUI>();
            _mediaLibrary.Initialize(_canvas, catalogClient, playback, () => _settingsUI.mediaBaseUrlInput.text);
            _settingsUI.mediaCatalogClient = catalogClient;
            _mediaLibrary.SetOnClose(() => {
                _settingsUI.SetAdvancedVisible(true);
                _settingsUI.BackToHome();
            });
            _settingsUI.mediaLibrary = _mediaLibrary;
            _settingsUI.mediaPlayback = playback;

            CreateStatusText(panel, 8, 0.05f, statusRight, out var statusText);
            _settingsUI.statusText = statusText;

            _settingsUI.Initialize(signalingClient, xrCamera);
        }

        private Transform CreatePanel(Transform parent)
        {
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(parent, false);

            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.sizeDelta = Vector2.zero;

            return panelGo.transform;
        }

        private void CreatePanelTitle(Transform parent, string value)
        {
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(parent, false);
            var title = titleGo.AddComponent<Text>();
            title.text = value;
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = titleGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.28f, 0.91f);
            rt.anchorMax = new Vector2(0.95f, 0.99f);
            rt.sizeDelta = Vector2.zero;
        }

        private void CreateBackButton(Transform parent, out Button button)
        {
            var btnGo = new GameObject("Button_Back");
            btnGo.transform.SetParent(parent, false);
            var image = btnGo.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.3f, 1f);
            button = btnGo.AddComponent<Button>();
            button.targetGraphic = image;
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.94f);
            rt.anchorMax = new Vector2(0.25f, 0.995f);
            rt.sizeDelta = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = "← Back";
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
        }

        private void CreateInputField(Transform parent, string label, string prefKey, string defaultValue, int index, out InputField inputField)
        {
            var row = new GameObject($"Row_{index}");
            row.transform.SetParent(parent, false);

            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.05f, 0.85f - index * 0.1f);
            rowRt.anchorMax = new Vector2(0.95f, 0.85f - index * 0.1f + 0.08f);
            rowRt.sizeDelta = Vector2.zero;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.fontSize = 24;
            labelText.raycastTarget = false;
            labelText.color = Color.white;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0, 0.2f);
            labelRt.anchorMax = new Vector2(0.35f, 0.8f);
            labelRt.sizeDelta = Vector2.zero;

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(row.transform, false);
            var inputImage = inputGo.AddComponent<Image>();
            inputImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.37f, 0.1f);
            inputRt.anchorMax = new Vector2(1f, 0.9f);
            inputRt.sizeDelta = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.fontSize = 22;
            text.raycastTarget = false;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.offsetMin = new Vector2(10, 5);
            textRt.offsetMax = new Vector2(-10, -5);

            inputField = inputGo.AddComponent<QuestKeyboardInputField>();
            inputField.textComponent = text;
            inputField.targetGraphic = inputImage;
            inputField.shouldHideMobileInput = false;
            inputField.keyboardType = TouchScreenKeyboardType.Default;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.text = PlayerPrefs.GetString(prefKey, defaultValue);
        }

        private void CreateButton(Transform parent, string label, int index, out Button button) => CreateButton(parent, label, index, label == "Save" ? 0.05f : 0.52f, out button);

        private void CreateButton(Transform parent, string label, int index, float xPos, out Button button)
        {
            var btnGo = new GameObject($"Button_{label}");
            btnGo.transform.SetParent(parent, false);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = label == "Save" ? new Color(0.3f, 0.3f, 0.4f, 1f) : new Color(0.2f, 0.6f, 0.2f, 1f);

            button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImage;

            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xPos, 0.85f - index * 0.1f);
            rt.anchorMax = new Vector2(xPos + 0.43f, 0.85f - index * 0.1f + 0.08f);
            rt.sizeDelta = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.fontSize = 24;
            text.raycastTarget = false;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
        }

        private void CreateStatusText(Transform parent, int index, float minX, float maxX, out Text statusText)
        {
            var textGo = new GameObject("StatusText");
            textGo.transform.SetParent(parent, false);

            statusText = textGo.AddComponent<Text>();
            statusText.fontSize = 22;
            statusText.raycastTarget = false;
            statusText.color = Color.yellow;
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.text = "";

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(minX, 0.85f - index * 0.1f);
            rt.anchorMax = new Vector2(maxX, 0.85f - index * 0.1f + 0.08f);
            rt.sizeDelta = Vector2.zero;
        }
    }
}
