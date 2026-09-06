using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    /// <summary>
    /// User-facing AI Vision panel. Camera access and network requests are always
    /// explicit user actions; opening this panel never starts capture or inference.
    /// </summary>
    public sealed class AiVisionUI : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _panel;
        private QuestVisionService _vision;
        private QuestAiClient _ai;
        private Action _onBack;
        private InputField _endpointInput, _modelInput, _apiKeyInput;
        private Text _statusText, _resultText;
        private RawImage _preview;
        private float _nextRefresh;
        private bool _initialized;

        public bool IsVisible => _panel != null && _panel.activeInHierarchy;

        public void Initialize(Canvas canvas, QuestVisionService vision, QuestAiClient ai, Action onBack)
        {
            if (_initialized) return;
            if (canvas == null || vision == null || ai == null)
                throw new ArgumentException("AI Vision UI requires canvas, vision service and AI client");
            _canvas = canvas;
            _vision = vision;
            _ai = ai;
            _onBack = onBack;
            Build();
            _ai.ResultReceived += OnResultReceived;
            _vision.FrameSampled += OnFrameSampled;
            LoadConfiguration();
            Hide();
            _initialized = true;
        }

        public void Show()
        {
            if (!_initialized || _panel == null) return;
            LoadConfiguration();
            _panel.SetActive(true);
            _nextRefresh = 0f;
            RefreshStatus();
            RefreshPreview();
            RefreshResult();
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void Build()
        {
            _panel = new GameObject("AiVisionPanel");
            _panel.transform.SetParent(_canvas.transform, false);
            var background = _panel.AddComponent<Image>();
            background.color = new Color(0.045f, 0.055f, 0.085f, 0.985f);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.07f);
            rect.anchorMax = new Vector2(0.92f, 0.93f);
            rect.sizeDelta = Vector2.zero;

            var title = MakeText(_panel.transform, "AI Vision", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, 0.25f, 0.91f, 0.75f, 0.99f);
            MakeButton(_panel.transform, "← Back", 0.03f, 0.91f, 0.20f, 0.985f, Back);

            MakeLabel(_panel.transform, "OpenAI-compatible endpoint", 0.04f, 0.82f, 0.30f, 0.88f);
            _endpointInput = MakeInput(_panel.transform, false, 0.31f, 0.815f, 0.96f, 0.885f);
            MakeLabel(_panel.transform, "Model", 0.04f, 0.73f, 0.30f, 0.79f);
            _modelInput = MakeInput(_panel.transform, false, 0.31f, 0.725f, 0.62f, 0.795f);
            MakeLabel(_panel.transform, "API key", 0.64f, 0.73f, 0.75f, 0.79f);
            _apiKeyInput = MakeInput(_panel.transform, true, 0.75f, 0.725f, 0.96f, 0.795f);

            MakeButton(_panel.transform, "Save AI", 0.04f, 0.635f, 0.19f, 0.70f, SaveConfiguration);
            MakeButton(_panel.transform, "Permission", 0.205f, 0.635f, 0.37f, 0.70f, RequestPermission);
            MakeButton(_panel.transform, "Start Camera", 0.385f, 0.635f, 0.55f, 0.70f, StartCamera);
            MakeButton(_panel.transform, "Capture", 0.565f, 0.635f, 0.70f, 0.70f, Capture);
            MakeButton(_panel.transform, "Analyze", 0.715f, 0.635f, 0.84f, 0.70f, Analyze);
            MakeButton(_panel.transform, "Stop", 0.855f, 0.635f, 0.96f, 0.70f, StopCamera);

            _statusText = MakeText(_panel.transform, "", 17, FontStyle.Normal, TextAnchor.MiddleLeft).textComponent;
            _statusText.color = new Color(0.82f, 0.88f, 1f, 1f);
            Anchor(_statusText.rectTransform, 0.04f, 0.55f, 0.96f, 0.62f);

            var previewFrame = new GameObject("PreviewFrame");
            previewFrame.transform.SetParent(_panel.transform, false);
            var previewBackground = previewFrame.AddComponent<Image>();
            previewBackground.color = new Color(0.08f, 0.09f, 0.13f, 1f);
            Anchor(previewFrame.GetComponent<RectTransform>(), 0.04f, 0.08f, 0.49f, 0.53f);

            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(previewFrame.transform, false);
            _preview = previewGo.AddComponent<RawImage>();
            _preview.color = Color.white;
            Anchor(previewGo.GetComponent<RectTransform>(), 0.02f, 0.02f, 0.98f, 0.98f);

            var resultBackgroundGo = new GameObject("ResultFrame");
            resultBackgroundGo.transform.SetParent(_panel.transform, false);
            var resultBackground = resultBackgroundGo.AddComponent<Image>();
            resultBackground.color = new Color(0.08f, 0.09f, 0.13f, 1f);
            Anchor(resultBackgroundGo.GetComponent<RectTransform>(), 0.51f, 0.08f, 0.96f, 0.53f);

            _resultText = MakeText(resultBackgroundGo.transform, "No AI result yet.", 17, FontStyle.Normal, TextAnchor.UpperLeft).textComponent;
            _resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _resultText.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(_resultText.rectTransform, 0.04f, 0.05f, 0.96f, 0.95f);
        }

        private void LoadConfiguration()
        {
            if (_endpointInput != null) _endpointInput.text = _ai.endpointUrl ?? string.Empty;
            if (_modelInput != null) _modelInput.text = _ai.model ?? string.Empty;
            if (_apiKeyInput != null) _apiKeyInput.text = _ai.apiKey ?? string.Empty;
        }

        private void SaveConfiguration()
        {
            _ai.Configure(_endpointInput?.text, _modelInput?.text, _apiKeyInput?.text, true);
            RefreshStatus("AI settings saved locally.");
        }

        private void RequestPermission()
        {
            RefreshStatus("Requesting headset camera permission…");
            _vision.RequestPermission(granted => RefreshStatus(granted
                ? "Camera permission granted. Press Start Camera."
                : "Camera permission was not granted."));
        }

        private void StartCamera()
        {
            _vision.RefreshProvider();
            var started = _vision.StartCamera();
            RefreshStatus(started ? "Camera started." : "Camera unavailable or permission required.");
        }

        private void Capture()
        {
            var frame = _vision.CaptureSingleFrame();
            if (frame == null)
            {
                RefreshStatus("Capture failed. Start Camera and verify PCA availability.");
                return;
            }
            RefreshPreview();
            RefreshStatus($"Captured {frame.width}x{frame.height} frame.");
        }

        private void Analyze()
        {
            SaveConfiguration();
            if (!_ai.CanRequest)
            {
                RefreshStatus("Configure a valid http/https endpoint and model first.");
                return;
            }
            if (_vision.LastFrame == null && _vision.CaptureSingleFrame() == null)
            {
                RefreshStatus("No camera frame available. Start Camera and Capture first.");
                return;
            }
            RefreshPreview();
            if (_ai.AnalyzeLastFrame() == null)
            {
                RefreshStatus(_ai.IsRequestActive ? "AI request already in progress." : "Unable to start AI analysis.");
                return;
            }
            RefreshStatus("Analyzing captured frame…");
        }

        private void StopCamera()
        {
            _vision.StopCamera();
            RefreshStatus("Camera stopped.");
        }

        private void Back()
        {
            Hide();
            _onBack?.Invoke();
        }

        private void OnFrameSampled(QuestVisionFrame frame)
        {
            if (!IsVisible || frame == null) return;
            RefreshPreview();
        }

        private void OnResultReceived(AiVisionResult result)
        {
            if (!IsVisible) return;
            RefreshResult();
            RefreshStatus("AI analysis complete.");
        }

        private void Update()
        {
            if (!IsVisible || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            RefreshStatus();
        }

        private void RefreshStatus(string notice = null)
        {
            if (_statusText == null) return;
            var aiState = _ai.IsRequestActive ? "Requesting" : _ai.CanRequest ? "Ready" : "Not configured";
            var message = $"Camera: {_vision.CameraState}   ·   AI: {aiState}   ·   Last latency: {_ai.LastLatencyMs} ms";
            if (!string.IsNullOrWhiteSpace(_ai.LastError)) message += "   ·   " + _ai.LastError;
            if (!string.IsNullOrWhiteSpace(notice)) message = notice + "\n" + message;
            _statusText.text = message;
        }

        private void RefreshPreview()
        {
            if (_preview != null) _preview.texture = _vision.LastFrame?.texture;
        }

        private void RefreshResult()
        {
            if (_resultText == null) return;
            var result = _ai.LastResult;
            if (result == null)
            {
                _resultText.text = "No AI result yet.";
                return;
            }
            var text = new StringBuilder(1024);
            text.AppendLine(string.IsNullOrWhiteSpace(result.text) ? "(no summary)" : result.text);
            if (result.objects != null && result.objects.Length > 0)
            {
                text.AppendLine();
                text.AppendLine("Objects");
                for (var i = 0; i < Mathf.Min(result.objects.Length, 10); i++)
                {
                    var item = result.objects[i];
                    if (item == null) continue;
                    text.AppendLine($"• {item.label}  {item.confidence:0.00}");
                }
            }
            if (result.actions != null && result.actions.Length > 0)
            {
                text.AppendLine();
                text.AppendLine($"Actions: {result.actions.Length}");
            }
            _resultText.text = text.ToString();
        }

        private static Text MakeLabel(Transform parent, string value, float minX, float minY, float maxX, float maxY)
        {
            var label = MakeText(parent, value, 18, FontStyle.Normal, TextAnchor.MiddleLeft).textComponent;
            Anchor(label.rectTransform, minX, minY, maxX, maxY);
            return label;
        }

        private static InputField MakeInput(Transform parent, bool password, float minX, float minY, float maxX, float maxY)
        {
            var go = new GameObject("Input");
            go.transform.SetParent(parent, false);
            var background = go.AddComponent<Image>();
            background.color = new Color(0.15f, 0.17f, 0.23f, 1f);
            Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            Anchor(text.rectTransform, 0.03f, 0.05f, 0.97f, 0.95f);

            var input = go.AddComponent<QuestKeyboardInputField>();
            input.textComponent = text;
            input.targetGraphic = background;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.shouldHideMobileInput = false;
            return input;
        }

        private static Button MakeButton(Transform parent, string label, float minX, float minY, float maxX, float maxY, Action action)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.16f, 0.30f, 0.46f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(() => action());
            Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);

            var text = MakeText(go.transform, label, 17, FontStyle.Normal, TextAnchor.MiddleCenter).textComponent;
            Anchor(text.rectTransform, 0f, 0f, 1f, 1f);
            return button;
        }

        private static (Text textComponent, RectTransform rectTransform) MakeText(Transform parent, string value, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = anchor;
            text.raycastTarget = false;
            return (text, go.GetComponent<RectTransform>());
        }

        private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (_ai != null) _ai.ResultReceived -= OnResultReceived;
            if (_vision != null) _vision.FrameSampled -= OnFrameSampled;
        }
    }
}
