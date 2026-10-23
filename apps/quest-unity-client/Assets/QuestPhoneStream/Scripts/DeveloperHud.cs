using System;
using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    /// <summary>
    /// Developer diagnostics and explicit foreground-only power preference.
    /// Sampling never modifies transport/provider state.
    /// </summary>
    public sealed class DeveloperHud : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _page;
        private QuestDiagnostics _diagnostics;
        private WirelessAdbHelper _wirelessAdb;
        private Action _onBack;
        private Text _body;
        private Toggle _autoRefresh;
        private QuestForegroundConnectionGuard _foregroundGuard;
        private Button _keepAwakeButton;
        private Text _keepAwakeText;
        private const string ViewPref = "QuestPhoneStream_DiagnosticsView_v1";
        private QuestDiagnosticsView _selectedView = QuestDiagnosticsView.All;
        private readonly Button[] _viewButtons = new Button[5];
        private static readonly string[] ViewLabels = { "All", "Discovery", "Streaming", "XR / Spatial", "Caps" };
        private float _nextRefresh;
        private bool _initialized;

        public bool IsVisible => _page != null && _page.activeInHierarchy;

        public void Initialize(Canvas canvas, QuestDiagnostics diagnostics, WirelessAdbHelper wirelessAdb, Action onBack)
        {
            if (_initialized) return;
            if (canvas == null) throw new ArgumentException("Developer HUD requires a canvas");
            _canvas = canvas;
            _diagnostics = diagnostics;
            _wirelessAdb = wirelessAdb;
            _onBack = onBack;
            _selectedView = SafeView(PlayerPrefs.GetInt(ViewPref, 0));
            _foregroundGuard = GetComponent<QuestForegroundConnectionGuard>();
            if (!_foregroundGuard) _foregroundGuard = gameObject.AddComponent<QuestForegroundConnectionGuard>();
            Build();
            _initialized = true;
        }

        public void Show()
        {
            if (!_initialized) throw new InvalidOperationException("Initialize Developer HUD before showing it");
            _wirelessAdb?.Hide();
            _page.SetActive(true);
            UpdateKeepAwakeLabel();
            Refresh();
        }

        public void Hide()
        {
            if (_page != null) _page.SetActive(false);
        }

        public void Refresh()
        {
            if (!IsVisible) return;
            _body.text = _diagnostics != null ? _diagnostics.CaptureSnapshot().ToDisplayText(_selectedView) : "Diagnostics unavailable";
            LayoutRebuilder.ForceRebuildLayoutImmediate(_body.rectTransform);
            _nextRefresh = Time.unscaledTime + 1f;
        }

        private void Update()
        {
            if (IsVisible && _autoRefresh != null && _autoRefresh.isOn && Time.unscaledTime >= _nextRefresh)
                Refresh();
        }

        public QuestDiagnosticsView SelectedView => _selectedView;

        public static QuestDiagnosticsView SafeView(int value) =>
            value >= (int)QuestDiagnosticsView.All && value <= (int)QuestDiagnosticsView.Capabilities
                ? (QuestDiagnosticsView)value : QuestDiagnosticsView.All;

        public void SelectView(QuestDiagnosticsView view)
        {
            _selectedView = SafeView((int)view);
            PlayerPrefs.SetInt(ViewPref, (int)_selectedView);
            PlayerPrefs.Save();
            for (int i = 0; i < _viewButtons.Length; i++)
            {
                if (!_viewButtons[i]) continue;
                var image = _viewButtons[i].GetComponent<Image>();
                if (image) image.color = i == (int)_selectedView
                    ? new Color(0.17f, 0.51f, 0.77f, 1f)
                    : new Color(0.18f, 0.24f, 0.32f, 1f);
            }
            if (IsVisible) Refresh();
        }

        private void ToggleKeepAwake()
        {
            if (!_foregroundGuard) return;
            _foregroundGuard.SetEnabled(!_foregroundGuard.Enabled);
            UpdateKeepAwakeLabel();
        }

        private void UpdateKeepAwakeLabel()
        {
            if (_keepAwakeText != null && _foregroundGuard != null)
                _keepAwakeText.text = _foregroundGuard.Enabled ? "Keep Wi-Fi: ON" : "Keep Wi-Fi: OFF";
        }

        private void OpenWirelessAdb()
        {
            Hide();
            _wirelessAdb?.Show();
        }

        private void Build()
        {
            _page = new GameObject("DeveloperHudPanel");
            _page.transform.SetParent(_canvas.transform, false);
            var image = _page.AddComponent<Image>();
            image.color = new Color(0.05f, 0.07f, 0.11f, 0.98f);
            var pageRect = _page.GetComponent<RectTransform>();
            pageRect.anchorMin = new Vector2(0.05f, 0.04f);
            pageRect.anchorMax = new Vector2(0.95f, 0.96f);
            pageRect.sizeDelta = Vector2.zero;

            CreateText(_page.transform, "Keep Wi-Fi works only while this app is active; OS sleep / ADB is separate",
                11, TextAnchor.MiddleCenter, 0.04f, 0.12f, 0.96f, 0.15f, out _);
            CreateText(_page.transform, "Developer Diagnostics", 28, TextAnchor.MiddleLeft, 0.04f, 0.93f, 0.76f, 0.99f, out _);
            CreateText(_page.transform, "read-only · 1 Hz max", 14, TextAnchor.MiddleRight, 0.70f, 0.93f, 0.96f, 0.99f, out var subtitle);
            subtitle.color = new Color(0.65f, 0.72f, 0.82f, 1f);
            // Large world-space category buttons remain ray/poke selectable with XR.
            const float left = 0.04f, right = 0.96f, spacing = 0.009f;
            float cellWidth = (right - left - spacing * 4f) / 5f;
            for (int i = 0; i < ViewLabels.Length; i++)
            {
                int index = i;
                float x = left + (cellWidth + spacing) * i;
                _viewButtons[i] = CreateButton(_page.transform, ViewLabels[i], x, 0.83f,
                    x + cellWidth, 0.90f, () => SelectView((QuestDiagnosticsView)index));
            }
            BuildScrollableBody();
            SelectView(_selectedView);

            CreateButton(_page.transform, "Refresh", 0.04f, 0.05f, 0.18f, 0.12f, Refresh);
            CreateButton(_page.transform, "Wireless ADB", 0.19f, 0.05f, 0.35f, 0.12f, OpenWirelessAdb);
            _keepAwakeButton = CreateButton(_page.transform, "Keep Wi-Fi: OFF",
                0.36f, 0.05f, 0.62f, 0.12f, ToggleKeepAwake);
            _keepAwakeText = _keepAwakeButton.GetComponentInChildren<Text>();
            CreateButton(_page.transform, "Back", 0.82f, 0.05f, 0.96f, 0.12f, () => { Hide(); _onBack?.Invoke(); });

            var toggleGo = new GameObject("AutoRefresh");
            toggleGo.transform.SetParent(_page.transform, false);
            var toggleRect = toggleGo.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.63f, 0.05f);
            toggleRect.anchorMax = new Vector2(0.81f, 0.12f);
            toggleRect.sizeDelta = Vector2.zero;
            _autoRefresh = toggleGo.AddComponent<Toggle>();
            var background = toggleGo.AddComponent<Image>();
            background.color = new Color(0.16f, 0.2f, 0.28f, 1f);
            _autoRefresh.targetGraphic = background;
            _autoRefresh.isOn = true;
            CreateText(toggleGo.transform, "Auto (1 Hz)", 12, TextAnchor.MiddleCenter, 0, 0, 1, 1, out _);
            _page.SetActive(false);
        }

        private void BuildScrollableBody()
        {
            var scrollGo = new GameObject("DiagnosticsScroll");
            scrollGo.transform.SetParent(_page.transform, false);
            var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.04f, 0.15f);
            scrollRectTransform.anchorMax = new Vector2(0.96f, 0.81f);
            scrollRectTransform.sizeDelta = Vector2.zero;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewportGo.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(0.08f, 0.10f, 0.15f, 0.7f);
            scroll.viewport = viewportRect;

            var bodyGo = new GameObject("DiagnosticsBody");
            bodyGo.transform.SetParent(viewportGo.transform, false);
            var bodyRect = bodyGo.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0, 1);
            bodyRect.anchorMax = new Vector2(1, 1);
            bodyRect.pivot = new Vector2(0.5f, 1);
            bodyRect.sizeDelta = Vector2.zero;
            _body = bodyGo.AddComponent<Text>();
            _body.fontSize = 15;
            _body.alignment = TextAnchor.UpperLeft;
            _body.color = Color.white;
            _body.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            _body.raycastTarget = false;
            var fitter = bodyGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = bodyRect;
        }

        private static void CreateText(Transform parent, string value, int size, TextAnchor alignment,
            float minX, float minY, float maxX, float maxY, out Text text)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.sizeDelta = Vector2.zero;
        }

        private static Button CreateButton(Transform parent, string label, float minX, float minY,
            float maxX, float maxY, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.3f, 0.48f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.sizeDelta = Vector2.zero;
            CreateText(go.transform, label, 15, TextAnchor.MiddleCenter, 0, 0, 1, 1, out _);
            return button;
        }
    }
}
