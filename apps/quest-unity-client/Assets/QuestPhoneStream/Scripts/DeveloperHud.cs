using System;
using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    /// <summary>
    /// Read-only developer diagnostics page. It is created only from Developer Tools,
    /// samples at most once per second when auto-refresh is enabled, and never changes
    /// transport state.
    /// </summary>
    public sealed class DeveloperHud : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _page;
        private QuestDiagnostics _diagnostics;
        private QuestDeveloperHud _p2Diagnostics;
        private WirelessAdbHelper _wirelessAdb;
        private Action _onBack;
        private Text _body;
        private Toggle _autoRefresh;
        private float _nextRefresh;
        private bool _initialized;

        public bool IsVisible => _page != null && _page.activeInHierarchy;

        public void Initialize(Canvas canvas, QuestDiagnostics diagnostics, WirelessAdbHelper wirelessAdb, Action onBack) =>
            Initialize(canvas, diagnostics, null, wirelessAdb, onBack);

        public void Initialize(Canvas canvas, QuestDiagnostics diagnostics, QuestDeveloperHud p2Diagnostics,
            WirelessAdbHelper wirelessAdb, Action onBack)
        {
            if (_initialized) return;
            if (canvas == null) throw new ArgumentException("Developer HUD requires a canvas");
            _canvas = canvas;
            _diagnostics = diagnostics;
            _p2Diagnostics = p2Diagnostics;
            _wirelessAdb = wirelessAdb;
            _onBack = onBack;
            Build();
            _initialized = true;
        }

        public void Show()
        {
            if (!_initialized) throw new InvalidOperationException("Initialize Developer HUD before showing it");
            _wirelessAdb?.Hide();
            _page.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_page != null) _page.SetActive(false);
        }

        public void Refresh()
        {
            if (!IsVisible) return;
            var core = _diagnostics != null ? _diagnostics.CaptureSnapshot().ToDisplayText() : "Diagnostics unavailable";
            var p2 = _p2Diagnostics != null ? _p2Diagnostics.CaptureText() : string.Empty;
            _body.text = core + p2;
            _nextRefresh = Time.unscaledTime + 1f;
        }

        private void Update()
        {
            if (IsVisible && _autoRefresh != null && _autoRefresh.isOn && Time.unscaledTime >= _nextRefresh)
                Refresh();
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

            CreateText(_page.transform, "Developer HUD", 28, TextAnchor.MiddleCenter, 0.25f, 0.94f, 0.75f, 0.99f, out _);
            CreateText(_page.transform, "", 14, TextAnchor.UpperLeft, 0.04f, 0.17f, 0.96f, 0.92f, out _body);
            CreateButton(_page.transform, "Refresh", 0.04f, 0.06f, 0.18f, 0.13f, Refresh);
            CreateButton(_page.transform, "Wireless ADB", 0.20f, 0.06f, 0.40f, 0.13f, OpenWirelessAdb);
            CreateButton(_page.transform, "Back", 0.82f, 0.06f, 0.96f, 0.13f, () => { Hide(); _onBack?.Invoke(); });

            var toggleGo = new GameObject("AutoRefresh");
            toggleGo.transform.SetParent(_page.transform, false);
            var toggleRect = toggleGo.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.44f, 0.06f);
            toggleRect.anchorMax = new Vector2(0.76f, 0.13f);
            toggleRect.sizeDelta = Vector2.zero;
            _autoRefresh = toggleGo.AddComponent<Toggle>();
            var background = toggleGo.AddComponent<Image>();
            background.color = new Color(0.16f, 0.2f, 0.28f, 1f);
            _autoRefresh.targetGraphic = background;
            _autoRefresh.isOn = true;
            CreateText(toggleGo.transform, "Auto Refresh (1 Hz)", 15, TextAnchor.MiddleCenter, 0, 0, 1, 1, out _);
            _page.SetActive(false);
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
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.sizeDelta = Vector2.zero;
        }

        private static void CreateButton(Transform parent, string label, float minX, float minY,
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
        }
    }
}
