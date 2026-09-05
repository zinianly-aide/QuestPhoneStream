using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream
{
    /// <summary>
    /// Compact end-user control bar. Engineering fields remain behind Advanced Settings;
    /// this bar is the normal Phone/Videos entry point.
    /// </summary>
    public sealed class QuestHomeUI : MonoBehaviour
    {
        private Canvas _canvas;
        private QuestSignalingClient _signaling;
        private QuestWebRtcReceiver _receiver;
        private Camera _camera;
        private Text _phoneStatus, _screenStatus, _controlStatus, _mediaStatus;
        private Button _phoneTab, _videosTab;
        private bool _initialized;
        private bool _videosSelected;
        private Coroutine _keyboardRoutine;

        public bool IsVisible => _canvas != null && _canvas.gameObject.activeInHierarchy;

        public void Initialize(QuestSignalingClient signaling, Camera xrCamera, QuestWebRtcReceiver receiver)
        {
            if (_initialized) return;
            if (signaling == null || xrCamera == null || receiver == null)
                throw new System.ArgumentException("Quest home UI requires signaling, camera and receiver");
            _signaling = signaling;
            _camera = xrCamera;
            _receiver = receiver;
            Build();
            _signaling.StateChanged += OnStateChanged;
            UpdateStatus(_signaling.State);
            Show();
            _initialized = true;
        }

        public void Show()
        {
            if (_canvas == null || _camera == null) return;
            var forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            _canvas.transform.position = _camera.transform.position + forward * 1.35f + Vector3.down * 0.62f;
            _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _canvas.gameObject.SetActive(true);
        }

        public void Hide() { if (_canvas != null) _canvas.gameObject.SetActive(false); }
        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        private void Build()
        {
            var canvasGo = new GameObject("QuestHomeCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _camera;
            _canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000, 300);
            canvasRect.localScale = Vector3.one * 0.002f;

            var panelGo = new GameObject("HomePanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = panelGo.AddComponent<Image>();
            panel.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var title = MakeText(panelGo.transform, "QuestPhoneStream", 28, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, 0.05f, 0.78f, 0.5f, 0.96f);
            var phone = MakeText(panelGo.transform, "Phone  ·  Disconnected", 20, TextAnchor.MiddleLeft);
            Anchor(phone.rectTransform, 0.52f, 0.78f, 0.95f, 0.96f);
            _phoneStatus = phone.textComponent;

            _screenStatus = AddStatus(panelGo.transform, "Screen", 0.05f, 0.61f);
            _controlStatus = AddStatus(panelGo.transform, "Control", 0.30f, 0.61f);
            _mediaStatus = AddStatus(panelGo.transform, "Media", 0.55f, 0.61f);

            _phoneTab = MakeButton(panelGo.transform, "Phone", 0.05f, 0.39f, 0.27f, 0.56f, OnPhone);
            _videosTab = MakeButton(panelGo.transform, "Videos", 0.29f, 0.39f, 0.51f, 0.56f, OnVideos);
            MakeButton(panelGo.transform, "Keyboard", 0.55f, 0.39f, 0.76f, 0.56f, OpenKeyboard);
            MakeButton(panelGo.transform, "Settings", 0.78f, 0.39f, 0.95f, 0.56f, OpenSettings);

            var hint = MakeText(panelGo.transform, "Use the controller ray to select Phone or Videos", 16, TextAnchor.MiddleLeft);
            hint.textComponent.color = new Color(0.72f, 0.78f, 0.9f, 1f);
            Anchor(hint.rectTransform, 0.05f, 0.08f, 0.95f, 0.28f);
        }

        private Text AddStatus(Transform parent, string label, float x, float y)
        {
            var text = MakeText(parent, label + "  ·  —", 17, TextAnchor.MiddleLeft);
            Anchor(text.rectTransform, x, y, x + 0.25f, y + 0.16f);
            return text.textComponent;
        }

        private void OnPhone()
        {
            _videosSelected = false;
            _receiver?.SetPhoneScreenMode();
            SetTab(_phoneTab, true);
            SetTab(_videosTab, false);
        }

        private void OnVideos()
        {
            _videosSelected = true;
            _receiver?.OpenVideoLibrary();
            Hide();
        }

        private void OpenSettings()
        {
            _receiver?.ToggleSettings();
            Hide();
        }

        private void OpenKeyboard()
        {
            if (_keyboardRoutine != null) StopCoroutine(_keyboardRoutine);
            _keyboardRoutine = StartCoroutine(ReadKeyboard());
        }

        private IEnumerator ReadKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported)
            {
                Debug.LogWarning("[QuestPhoneStream] Native keyboard is unavailable on this platform");
                yield break;
            }
            var keyboard = TouchScreenKeyboard.Open(string.Empty, TouchScreenKeyboardType.Default, false, false, false);
            while (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Visible)
                yield return null;
            if (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Done && !string.IsNullOrEmpty(keyboard.text))
                _receiver?.controlChannel?.SendText(keyboard.text);
            _keyboardRoutine = null;
        }

        private void OnStateChanged(ConnectionState state) => UpdateStatus(state);

        private void Update()
        {
            if (!_initialized || !IsVisible) return;
            UpdateStatus(_signaling.State);
        }

        private void UpdateStatus(ConnectionState state)
        {
            if (_phoneStatus == null || _receiver == null) return;
            var failed = ConnectionStatus.IsFailure(state);
            _phoneStatus.text = failed ? "Phone  ·  Needs attention" :
                (int)state >= (int)ConnectionState.Registered ? "Phone  ·  Connected" : "Phone  ·  Searching...";
            _screenStatus.text = "Screen  ·  " + (_receiver.HasVideoFrame ? "Ready" : _receiver.IsPeerConnected ? "Waiting" : "—");
            _controlStatus.text = "Control  ·  " + (_receiver.IsControlConnected ? "Ready" : _receiver.IsPeerConnected ? "Waiting" : "—");
            _mediaStatus.text = "Media  ·  " + (_receiver.HasMediaUrl ? "Ready" : "Set in Settings");
            SetTab(_phoneTab, !_videosSelected);
            SetTab(_videosTab, _videosSelected);
        }

        private static void SetTab(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.targetGraphic as Graphic;
            if (image != null) image.color = selected ? new Color(0.16f, 0.55f, 0.34f, 1f) : new Color(0.14f, 0.18f, 0.26f, 1f);
        }

        private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.sizeDelta = Vector2.zero;
        }

        private static Button MakeButton(Transform parent, string label, float minX, float minY, float maxX, float maxY, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.14f, 0.18f, 0.26f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
            var text = MakeText(go.transform, label, 19, TextAnchor.MiddleCenter);
            Anchor(text.rectTransform, 0, 0, 1, 1);
            button.onClick.AddListener(action);
            return button;
        }

        private static UiText MakeText(Transform parent, string value, int size, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return new UiText(text, go.GetComponent<RectTransform>());
        }

        private readonly struct UiText
        {
            public readonly Text textComponent;
            public readonly RectTransform rectTransform;
            public UiText(Text text, RectTransform rect) { textComponent = text; rectTransform = rect; }
            public static implicit operator Text(UiText value) => value.textComponent;
        }

        private void OnDestroy()
        {
            if (_signaling != null) _signaling.StateChanged -= OnStateChanged;
            if (_keyboardRoutine != null) StopCoroutine(_keyboardRoutine);
        }
    }
}
