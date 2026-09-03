using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    public sealed class SettingsUI : MonoBehaviour
    {
        [Header("References")]
        public QuestSignalingClient signalingClient;

        [Header("UI")]
        public Canvas canvas;
        public InputField signalingUrlInput;
        public InputField tokenInput;
        public InputField questDeviceIdInput;
        public InputField androidDeviceIdInput;
        public InputField sessionIdInput;
        public Button saveButton;
        public Button connectButton;
        public Text statusText;

        private const string UrlPrefKey = "QuestPhoneStream_SignalingUrl";
        private const string TokenPrefKey = "QuestPhoneStream_Token";
        private const string QuestIdPrefKey = "QuestPhoneStream_QuestDeviceId";
        private const string AndroidIdPrefKey = "QuestPhoneStream_AndroidDeviceId";
        private const string SessionIdPrefKey = "QuestPhoneStream_SessionId";

        private bool _isVisible;

        private void Start()
        {
            if (canvas == null) canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
                UpdateCanvasPosition();
            }

            LoadSettings();
            SetupUI();
        }

        private void Update()
        {
            if (_isVisible)
            {
                UpdateCanvasPosition();
            }
        }

        private void UpdateCanvasPosition()
        {
            var cam = Camera.main;
            if (cam != null && canvas != null)
            {
                canvas.transform.position = cam.transform.position + cam.transform.forward * 2f;
                canvas.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
                Debug.Log($"[QuestPhoneStream] Canvas position: {canvas.transform.position}, camera: {cam.transform.position}");
            }
            else
            {
                Debug.LogWarning($"[QuestPhoneStream] Canvas or Camera is null. cam={cam != null}, canvas={canvas != null}");
            }
        }

        private void SetupUI()
        {
            if (saveButton != null) saveButton.onClick.AddListener(OnSave);
            if (connectButton != null) connectButton.onClick.AddListener(OnConnect);
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
            if (canvas != null) canvas.gameObject.SetActive(_isVisible);
            if (_isVisible) LoadSettings();
        }

        public void Show()
        {
            _isVisible = true;
            if (canvas != null) canvas.gameObject.SetActive(true);
            LoadSettings();
        }

        public void Hide()
        {
            _isVisible = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void LoadSettings()
        {
            if (signalingUrlInput != null)
                signalingUrlInput.text = PlayerPrefs.GetString(UrlPrefKey, "ws://192.168.1.11:8787");
            if (tokenInput != null)
                tokenInput.text = PlayerPrefs.GetString(TokenPrefKey, "dev-token");
            if (questDeviceIdInput != null)
                questDeviceIdInput.text = PlayerPrefs.GetString(QuestIdPrefKey, "quest-3s-001");
            if (androidDeviceIdInput != null)
                androidDeviceIdInput.text = PlayerPrefs.GetString(AndroidIdPrefKey, "android-phone-001");
            if (sessionIdInput != null)
                sessionIdInput.text = PlayerPrefs.GetString(SessionIdPrefKey, "local-session-001");
        }

        private void OnSave()
        {
            SaveSettings();
            SetStatus("Settings saved!");
        }

        private void OnConnect()
        {
            SaveSettings();
            ApplySettings();
            if (signalingClient != null)
            {
                signalingClient.Reconnect();
                SetStatus("Connecting...");
            }
        }

        private void SaveSettings()
        {
            if (signalingUrlInput != null)
                PlayerPrefs.SetString(UrlPrefKey, signalingUrlInput.text);
            if (tokenInput != null)
                PlayerPrefs.SetString(TokenPrefKey, tokenInput.text);
            if (questDeviceIdInput != null)
                PlayerPrefs.SetString(QuestIdPrefKey, questDeviceIdInput.text);
            if (androidDeviceIdInput != null)
                PlayerPrefs.SetString(AndroidIdPrefKey, androidDeviceIdInput.text);
            if (sessionIdInput != null)
                PlayerPrefs.SetString(SessionIdPrefKey, sessionIdInput.text);
            PlayerPrefs.Save();
        }

        public void ApplySettings()
        {
            if (signalingClient == null) return;

            signalingClient.signalingUrl = PlayerPrefs.GetString(UrlPrefKey, "ws://192.168.1.11:8787");
            signalingClient.token = PlayerPrefs.GetString(TokenPrefKey, "dev-token");
            signalingClient.questDeviceId = PlayerPrefs.GetString(QuestIdPrefKey, "quest-3s-001");
            signalingClient.androidDeviceId = PlayerPrefs.GetString(AndroidIdPrefKey, "android-phone-001");
            signalingClient.sessionId = PlayerPrefs.GetString(SessionIdPrefKey, "local-session-001");
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[QuestPhoneStream] Settings: {message}");
        }
    }
}
