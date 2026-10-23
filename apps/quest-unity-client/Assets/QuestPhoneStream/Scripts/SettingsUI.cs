using System;
using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    public sealed class SettingsUI : MonoBehaviour
    {
        public QuestSignalingClient signalingClient { get; private set; }
        public Canvas canvas;
        public InputField signalingUrlInput, tokenInput, questDeviceIdInput, androidDeviceIdInput, sessionIdInput, mediaBaseUrlInput;
        public Button saveButton, connectButton, phoneScreenButton, videoLibraryButton;
        public Button developerToolsButton;
        public Button backButton;
        public Text statusText;
        public MediaLibraryUI mediaLibrary;
        public MediaPlaybackController mediaPlayback;
        public QuestWebRtcReceiver receiver;
        public MediaCatalogClient mediaCatalogClient;
        public WirelessAdbHelper wirelessAdbHelper;
        public DeveloperHud developerHud;
        public Action onBackToHome;
        public bool IsVisible => canvas != null && canvas.gameObject.activeInHierarchy;

        private Camera _xrCamera;
        private bool _initialized, _isConnecting, _reportedMissingCamera;
        private InputField[] Inputs => new[] { signalingUrlInput, tokenInput, questDeviceIdInput, androidDeviceIdInput, sessionIdInput, mediaBaseUrlInput };

        public void Initialize(QuestSignalingClient client, Camera xrCamera)
        {
            if (_initialized) throw new InvalidOperationException("Settings UI is already initialized");
            if (client == null || xrCamera == null || canvas == null)
                throw new ArgumentException("Settings UI requires signaling, camera and canvas");
            signalingClient = client;
            _xrCamera = xrCamera;
            canvas.worldCamera = xrCamera;
            _initialized = true;
            saveButton.onClick.AddListener(OnSave);
            connectButton.onClick.AddListener(OnConnect);
            backButton?.onClick.AddListener(OnBack);
            developerToolsButton?.onClick.AddListener(ShowDeveloperTools);
            phoneScreenButton?.onClick.AddListener(() => {
                mediaLibrary?.Close();
                if (receiver != null) receiver.SetPhoneScreenMode();
                else mediaPlayback?.SetPhoneScreenMode();
            });
            videoLibraryButton?.onClick.AddListener(() => {
                if (receiver != null && !receiver.SupportsMedia) return;
                SetAdvancedVisible(false);
                if (receiver != null) receiver.OpenVideoLibrary();
                else mediaLibrary?.Open();
            });
            client.StateChanged += OnStateChanged;
            LoadSettings();
            RegisterDraftSaving();
            Hide();
            OnStateChanged(client.State);
        }

        public void Show()
        {
            if (!_initialized) throw new InvalidOperationException("Initialize Settings UI before showing it");
            if (_xrCamera == null)
            {
                if (!_reportedMissingCamera) Debug.LogError("[QuestPhoneStream] Settings XR camera is missing");
                _reportedMissingCamera = true;
                return;
            }
            if (IsVisible) return;
            var forward = Vector3.ProjectOnPlane(_xrCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.Cross(_xrCamera.transform.right, Vector3.up);
            forward.Normalize();
            canvas.transform.position = _xrCamera.transform.position + forward * 2f + Vector3.down * 0.15f;
            canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            canvas.gameObject.SetActive(true);
        }

        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }
        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        public void ShowAdvanced()
        {
            developerHud?.Hide();
            wirelessAdbHelper?.Hide();
            SetAdvancedVisible(true);
            Show();
        }

        public void ShowDeveloperTools()
        {
            if (!_initialized) throw new InvalidOperationException("Initialize Settings UI before showing developer tools");
            SetAdvancedVisible(false);
            Show();
            wirelessAdbHelper?.Hide();
            developerHud?.Show();
        }

        public void HideDeveloperTools()
        {
            developerHud?.Hide();
            wirelessAdbHelper?.Hide();
            SetAdvancedVisible(true);
            Show();
        }

        public void SetAdvancedVisible(bool visible)
        {
            if (canvas == null) return;
            var panel = canvas.transform.Find("Panel");
            if (panel != null) panel.gameObject.SetActive(visible);
        }

        private void OnBack()
        {
            developerHud?.Hide();
            wirelessAdbHelper?.Hide();
            SetAdvancedVisible(true);
            Hide();
            onBackToHome?.Invoke();
        }

        public void BackToHome() => OnBack();

        public void SetMediaBaseUrl(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (mediaBaseUrlInput != null) mediaBaseUrlInput.text = normalized;
            if (mediaCatalogClient != null) mediaCatalogClient.baseUrl = normalized;
            PlayerPrefs.SetString("QuestPhoneStream_MediaBaseUrl", normalized);
            PlayerPrefs.Save();
        }

        public void ApplyDiscoveredSignaling(string endpoint, string streamId)
        {
            var changed = false;
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                var normalizedEndpoint = endpoint.Trim();
                changed |= !string.Equals(signalingClient.signalingUrl, normalizedEndpoint, StringComparison.Ordinal);
                signalingClient.signalingUrl = normalizedEndpoint;
                if (signalingUrlInput != null) signalingUrlInput.text = signalingClient.signalingUrl;
            }
            if (!string.IsNullOrWhiteSpace(streamId))
            {
                var normalizedStreamId = streamId.Trim();
                changed |= !string.Equals(signalingClient.androidDeviceId, normalizedStreamId, StringComparison.Ordinal);
                signalingClient.androidDeviceId = normalizedStreamId;
                if (androidDeviceIdInput != null) androidDeviceIdInput.text = signalingClient.androidDeviceId;
            }
            if (changed) signalingClient.NotifyTargetChanged();
        }

        private void LoadSettings()
        {
            var persistedEndpoint = PlayerPrefs.GetString("QuestPhoneStream_SignalingUrl_v2", string.Empty);
            signalingUrlInput.text = QuestSignalingClient.ResolveSignalingEndpoint(
                persistedEndpoint, string.Empty, signalingClient.signalingUrl);
            signalingClient.signalingUrl = signalingUrlInput.text.Trim();
            tokenInput.text = PlayerPrefs.GetString("QuestPhoneStream_Token", signalingClient.token);
            questDeviceIdInput.text = PlayerPrefs.GetString("QuestPhoneStream_QuestDeviceId", signalingClient.questDeviceId);
            androidDeviceIdInput.text = PlayerPrefs.GetString("QuestPhoneStream_AndroidDeviceId", signalingClient.androidDeviceId);
            sessionIdInput.text = PlayerPrefs.GetString("QuestPhoneStream_SessionId", signalingClient.sessionId);
            if (mediaBaseUrlInput != null) mediaBaseUrlInput.text = PlayerPrefs.GetString("QuestPhoneStream_MediaBaseUrl", mediaBaseUrlInput.text);
        }

        /// <summary>
        /// Save local input edits independently of network connectivity. In particular,
        /// users must be able to save controller/connection preferences before pairing.
        /// onEndEdit is triggered by the Quest keyboard when editing completes.
        /// </summary>
        private void RegisterDraftSaving()
        {
            BindDraftField(signalingUrlInput, "QuestPhoneStream_SignalingUrl_v2", true, true);
            BindDraftField(tokenInput, "QuestPhoneStream_Token");
            BindDraftField(questDeviceIdInput, "QuestPhoneStream_QuestDeviceId");
            BindDraftField(androidDeviceIdInput, "QuestPhoneStream_AndroidDeviceId");
            BindDraftField(sessionIdInput, "QuestPhoneStream_SessionId");
            BindDraftField(mediaBaseUrlInput, "QuestPhoneStream_MediaBaseUrl", true);
        }

        private void BindDraftField(InputField input, string key, bool allowEmpty = false,
            bool isSignalingEndpoint = false)
        {
            if (input == null) return;
            input.onEndEdit.AddListener(_ => PersistDraftField(input, key, allowEmpty, isSignalingEndpoint));
        }

        private bool PersistDraftField(InputField input, string key, bool allowEmpty,
            bool isSignalingEndpoint)
        {
            if (!_initialized || input == null) return false;
            string value = key == "QuestPhoneStream_Token" ? input.text : (input.text ?? "").Trim();
            if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) ||
                (isSignalingEndpoint && !CanSaveLocalSignalingUrl(value)))
            {
                if (statusText != null)
                    statusText.text = isSignalingEndpoint ?
                        "Invalid ws/wss URL. Previous saved endpoint kept." :
                        "Required field is empty. Previous saved value kept.";
                return false;
            }
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            return true;
        }

        public static bool CanSaveLocalSignalingUrl(string url) =>
            string.IsNullOrWhiteSpace(url) || QuestSignalingClient.IsValidSignalingEndpoint(url);

        private bool ValidateLocalSettings()
        {
            if (CanSaveLocalSignalingUrl(signalingUrlInput.text)) return true;
            statusText.text = "Enter a valid ws/wss URL, or leave it blank for automatic discovery.";
            return false;
        }

        private void SaveValidDrafts()
        {
            PersistDraftField(signalingUrlInput, "QuestPhoneStream_SignalingUrl_v2", true, true);
            PersistDraftField(tokenInput, "QuestPhoneStream_Token", false, false);
            PersistDraftField(questDeviceIdInput, "QuestPhoneStream_QuestDeviceId", false, false);
            PersistDraftField(androidDeviceIdInput, "QuestPhoneStream_AndroidDeviceId", false, false);
            PersistDraftField(sessionIdInput, "QuestPhoneStream_SessionId", false, false);
            PersistDraftField(mediaBaseUrlInput, "QuestPhoneStream_MediaBaseUrl", true, false);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _initialized) SaveValidDrafts();
        }

        private void OnApplicationQuit()
        {
            if (_initialized) SaveValidDrafts();
        }

        private bool ValidateSettings()
        {
            if (!Uri.TryCreate(signalingUrlInput.text.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss") || !string.IsNullOrEmpty(uri.UserInfo) ||
                string.IsNullOrWhiteSpace(tokenInput.text) || string.IsNullOrWhiteSpace(questDeviceIdInput.text) ||
                string.IsNullOrWhiteSpace(androidDeviceIdInput.text) || string.IsNullOrWhiteSpace(sessionIdInput.text))
            {
                statusText.text = "Enter a ws/wss URL, token and all device/session IDs.";
                return false;
            }
            return true;
        }

        private void OnSave()
        {
            if (_isConnecting || signalingClient.IsConnecting || !ValidateLocalSettings()) return;
            SaveValidDrafts();
            receiver?.ResetMediaProbeState();
            statusText.text = "Settings saved locally. Apply & Reconnect to connect.";
        }

        private async void OnConnect()
        {
            if (_isConnecting || signalingClient.IsConnecting || !ValidateSettings()) return;
            _isConnecting = true;
            SetBusy(true);
            try
            {
                SaveSettings();
                signalingClient.signalingUrl = signalingUrlInput.text.Trim();
                signalingClient.token = tokenInput.text;
                signalingClient.questDeviceId = questDeviceIdInput.text.Trim();
                signalingClient.androidDeviceId = androidDeviceIdInput.text.Trim();
                signalingClient.sessionId = sessionIdInput.text.Trim();
                signalingClient.NotifyTargetChanged();
                await signalingClient.ReconnectAsync();
            }
            catch (Exception)
            {
                if (this != null) statusText.text = "Connection failed. Check settings and retry.";
            }
            finally
            {
                _isConnecting = false;
                if (this != null) SetBusy(signalingClient != null && signalingClient.IsConnecting);
            }
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetString("QuestPhoneStream_SignalingUrl_v2", signalingUrlInput.text.Trim());
            PlayerPrefs.SetString("QuestPhoneStream_Token", tokenInput.text);
            PlayerPrefs.SetString("QuestPhoneStream_QuestDeviceId", questDeviceIdInput.text.Trim());
            PlayerPrefs.SetString("QuestPhoneStream_AndroidDeviceId", androidDeviceIdInput.text.Trim());
            PlayerPrefs.SetString("QuestPhoneStream_SessionId", sessionIdInput.text.Trim());
            if (mediaBaseUrlInput != null) PlayerPrefs.SetString("QuestPhoneStream_MediaBaseUrl", mediaBaseUrlInput.text.Trim());
            PlayerPrefs.Save();
        }

        private void OnStateChanged(ConnectionState state)
        {
            statusText.text = signalingClient.HasValidSignalingEndpoint
                ? ConnectionStatus.Text(state)
                : "Waiting for device. Select a discovered device or configure manually.";
            SetBusy(_isConnecting || signalingClient.IsConnecting);
        }

        private void SetBusy(bool busy)
        {
            connectButton.interactable = !busy;
            saveButton.interactable = !busy;
            foreach (var input in Inputs) input.interactable = !busy;
        }

        private void OnDestroy()
        {
            if (signalingClient != null) signalingClient.StateChanged -= OnStateChanged;
        }
    }
}
