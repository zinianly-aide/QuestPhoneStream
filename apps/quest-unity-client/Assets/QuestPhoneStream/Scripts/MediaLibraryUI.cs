using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    public sealed class MediaLibraryUI : MonoBehaviour
    {
        private Canvas _canvas;
        private MediaCatalogClient _catalog;
        private MediaPlaybackController _playback;
        private System.Func<string> _baseUrl;
        private GameObject _panel;
        private Transform _list;

        public void Initialize(Canvas canvas, MediaCatalogClient catalog, MediaPlaybackController playback, System.Func<string> baseUrl)
        {
            _canvas = canvas; _catalog = catalog; _playback = playback; _baseUrl = baseUrl;
            Build();
        }

        public void Open()
        {
            if (_panel == null) Build();
            _panel.SetActive(true);
            _catalog.baseUrl = _baseUrl?.Invoke() ?? _catalog.baseUrl;
            StartCoroutine(Refresh());
        }

        public void Close() { if (_panel != null) _panel.SetActive(false); }

        private void Build()
        {
            if (_canvas == null || _panel != null) return;
            _panel = new GameObject("VideoLibraryPanel");
            _panel.transform.SetParent(_canvas.transform, false);
            var image = _panel.AddComponent<Image>(); image.color = new Color(.06f, .07f, .1f, .98f);
            var root = _panel.GetComponent<RectTransform>(); root.anchorMin = new Vector2(.08f, .08f); root.anchorMax = new Vector2(.92f, .92f); root.sizeDelta = Vector2.zero;
            var title = MakeText(_panel.transform, "Video Library", 28); title.GetComponent<RectTransform>().anchorMin = new Vector2(.05f,.85f); title.GetComponent<RectTransform>().anchorMax = new Vector2(.95f,.98f);
            var close = MakeButton(_panel.transform, "Close", new Vector2(.78f,.02f), new Vector2(.95f,.12f)); close.onClick.AddListener(Close);
            var listGo = new GameObject("MediaList"); listGo.transform.SetParent(_panel.transform, false); _list = listGo.transform;
            var listRect = listGo.AddComponent<RectTransform>(); listRect.anchorMin = new Vector2(.05f,.15f); listRect.anchorMax = new Vector2(.95f,.82f); listRect.sizeDelta = Vector2.zero;
            var layout = listGo.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _panel.SetActive(false);
        }

        private IEnumerator Refresh()
        {
            foreach (Transform child in _list) Destroy(child.gameObject);
            yield return _catalog.GetMedia((items, error) => {
                if (!string.IsNullOrEmpty(error)) { AddText("Media request failed: " + error, 20); return; }
                if (items == null || items.Count == 0) { AddText("No shared videos", 22); return; }
                foreach (var item in items) AddItem(item);
            });
        }

        private void AddItem(MediaItemDto item)
        {
            var button = MakeListButton(_list, item.name + (item.seekable ? "" : " (no seek)"));
            button.onClick.AddListener(() => StartCoroutine(Play(item)));
        }

        private IEnumerator Play(MediaItemDto item)
        {
            yield return _catalog.RequestPlayToken(item.id, (token, error) => {
                if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(token))
                    _playback?.PlayUrl(_catalog.BuildContentUrl(item.id, token));
                if (!string.IsNullOrEmpty(error)) AddText("Play failed: " + error, 18);
            });
            Close();
        }

        private Text AddText(string value, int size)
        {
            var text = MakeText(_list, value, size); text.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40); return text;
        }

        private static Text MakeText(Transform parent, string value, int size)
        {
            var go = new GameObject("Text"); go.transform.SetParent(parent, false); var text = go.AddComponent<Text>(); text.text = value; text.fontSize = size; text.color = Color.white; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }

        private static Button MakeButton(Transform parent, string label, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Button_" + label); go.transform.SetParent(parent, false); var image = go.AddComponent<Image>(); image.color = new Color(.2f,.45f,.65f,1); var button = go.AddComponent<Button>(); button.targetGraphic = image; var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.sizeDelta = Vector2.zero; var text = MakeText(go.transform, label, 20); text.GetComponent<RectTransform>().anchorMin = Vector2.zero; text.GetComponent<RectTransform>().anchorMax = Vector2.one; text.GetComponent<RectTransform>().sizeDelta = Vector2.zero; return button;
        }

        private static Button MakeListButton(Transform parent, string label)
        {
            var button = MakeButton(parent, label, Vector2.zero, Vector2.one);
            var rect = button.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(0, 55);
            return button;
        }
    }
}
