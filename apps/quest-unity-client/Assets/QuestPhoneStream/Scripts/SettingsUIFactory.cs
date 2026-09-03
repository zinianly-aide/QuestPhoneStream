using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    public sealed class SettingsUIFactory : MonoBehaviour
    {
        public QuestSignalingClient signalingClient;

        private Canvas _canvas;
        private SettingsUI _settingsUI;

        private void Awake()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            var canvasGo = new GameObject("SettingsCanvas");
            canvasGo.transform.SetParent(null, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            canvasGo.AddComponent<GraphicRaycaster>();

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2, 1.5f);
            rt.localPosition = new Vector3(0, 1.5f, 2);
            rt.localRotation = Quaternion.identity;

            var panel = CreatePanel(canvasGo.transform);

            _settingsUI = gameObject.AddComponent<SettingsUI>();
            _settingsUI.canvas = _canvas;
            _settingsUI.signalingClient = signalingClient;

            CreateInputField(panel, "Signaling URL:", "QuestPhoneStream_SignalingUrl", "ws://192.168.1.11:8787", 0, out var urlInput);
            _settingsUI.signalingUrlInput = urlInput;

            CreateInputField(panel, "Token:", "QuestPhoneStream_Token", "dev-token", 1, out var tokenInput);
            _settingsUI.tokenInput = tokenInput;

            CreateInputField(panel, "Quest Device ID:", "QuestPhoneStream_QuestDeviceId", "quest-3s-001", 2, out var questIdInput);
            _settingsUI.questDeviceIdInput = questIdInput;

            CreateInputField(panel, "Android Device ID:", "QuestPhoneStream_AndroidDeviceId", "android-phone-001", 3, out var androidIdInput);
            _settingsUI.androidDeviceIdInput = androidIdInput;

            CreateInputField(panel, "Session ID:", "QuestPhoneStream_SessionId", "local-session-001", 4, out var sessionIdInput);
            _settingsUI.sessionIdInput = sessionIdInput;

            CreateButton(panel, "Save", 5, out var saveBtn);
            _settingsUI.saveButton = saveBtn;

            CreateButton(panel, "Connect", 6, out var connectBtn);
            _settingsUI.connectButton = connectBtn;

            CreateStatusText(panel, 7, out var statusText);
            _settingsUI.statusText = statusText;

            canvasGo.SetActive(false);
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

        private void CreateInputField(Transform parent, string label, string prefKey, string defaultValue, int index, out InputField inputField)
        {
            var row = new GameObject($"Row_{index}");
            row.transform.SetParent(parent, false);

            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.05f, 0.85f - index * 0.12f);
            rowRt.anchorMax = new Vector2(0.95f, 0.85f - index * 0.12f + 0.1f);
            rowRt.sizeDelta = Vector2.zero;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.fontSize = 18;
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
            text.fontSize = 16;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.offsetMin = new Vector2(10, 5);
            textRt.offsetMax = new Vector2(-10, -5);

            inputField = inputGo.AddComponent<InputField>();
            inputField.textComponent = text;
            inputField.text = PlayerPrefs.GetString(prefKey, defaultValue);
        }

        private void CreateButton(Transform parent, string label, int index, out Button button)
        {
            var btnGo = new GameObject($"Button_{label}");
            btnGo.transform.SetParent(parent, false);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = label == "Connect" ? new Color(0.2f, 0.6f, 0.2f, 1f) : new Color(0.3f, 0.3f, 0.4f, 1f);

            button = btnGo.AddComponent<Button>();

            var rt = btnGo.GetComponent<RectTransform>();
            float xPos = label == "Save" ? 0.05f : 0.52f;
            rt.anchorMin = new Vector2(xPos, 0.85f - index * 0.12f);
            rt.anchorMax = new Vector2(xPos + 0.43f, 0.85f - index * 0.12f + 0.1f);
            rt.sizeDelta = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
        }

        private void CreateStatusText(Transform parent, int index, out Text statusText)
        {
            var textGo = new GameObject("StatusText");
            textGo.transform.SetParent(parent, false);

            statusText = textGo.AddComponent<Text>();
            statusText.fontSize = 14;
            statusText.color = Color.yellow;
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.text = "";

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.85f - index * 0.12f);
            rt.anchorMax = new Vector2(0.95f, 0.85f - index * 0.12f + 0.1f);
            rt.sizeDelta = Vector2.zero;
        }
    }
}
