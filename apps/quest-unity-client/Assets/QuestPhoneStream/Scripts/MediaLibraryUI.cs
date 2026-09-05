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
        private Text _statusText;

        public void Initialize(Canvas canvas, MediaCatalogClient catalog, MediaPlaybackController playback, System.Func<string> baseUrl)
        {
            _canvas = canvas; _catalog = catalog; _playback = playback; _baseUrl = baseUrl;
            Build();
        }

        public void Open()
        {
            if (_panel == null || _list == null) Build();
            bool listNullAfterBuild = _list == null;
            bool listRefNullAfterBuild = ReferenceEquals(_list, null);
            int listInstanceIdAfterBuild = -1;
            try { if (!ReferenceEquals(_list, null)) listInstanceIdAfterBuild = _list.GetInstanceID(); } catch { }
            Debug.Log($"[MediaLibraryUI] Open step1 after Build: listEqNull={listNullAfterBuild} listRefNull={listRefNullAfterBuild} listInstId={listInstanceIdAfterBuild} panelEqNull={_panel == null}");

            if (_panel == null)
            {
                Debug.LogWarning("[MediaLibraryUI] Open skipped: panel was not built (Initialize may not have been called)");
                return;
            }
            _panel.SetActive(true);
            bool listNullAfterActivate = _list == null;
            Debug.Log($"[MediaLibraryUI] Open step2 after SetActive(true): listEqNull={listNullAfterActivate}");

            var urlFromSettings = _baseUrl?.Invoke();
            Debug.Log($"[MediaLibraryUI] Open: instance={GetInstanceID()} settingsUrl={urlFromSettings} catalogNull={_catalog == null} listNull={_list == null}");
            if (_catalog != null)
            {
                if (!string.IsNullOrEmpty(urlFromSettings))
                    _catalog.baseUrl = urlFromSettings;
                Debug.Log($"[MediaLibraryUI] Using catalog.baseUrl={_catalog.baseUrl}");
            }
            StartCoroutine(Refresh());
        }

        public void Close() { if (_panel != null) _panel.SetActive(false); }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
            Debug.Log($"[MediaLibraryUI] Status: {message}");
        }

        private void Build()
        {
            if (_canvas == null)
            {
                Debug.LogWarning("[MediaLibraryUI] Build skipped: _canvas is null (Initialize not called?)");
                return;
            }
            if (_panel != null && _list != null) return;

            // If panel exists but list is null, just rebuild the list on the existing panel.
            // Do NOT Destroy() the panel — Unity's deferred destruction queue was destroying
            // the newly-created MediaList in the same frame.
            if (_panel != null)
            {
                Debug.LogWarning("[MediaLibraryUI] Rebuilding list only (panel exists, list was destroyed)");
                BuildList();
                return;
            }

            try
            {
                _panel = new GameObject("VideoLibraryPanel");
                _panel.transform.SetParent(_canvas.transform, false);
                var image = _panel.AddComponent<Image>(); image.color = new Color(.06f, .07f, .1f, .98f);
                var root = _panel.GetComponent<RectTransform>(); root.anchorMin = new Vector2(.08f, .08f); root.anchorMax = new Vector2(.92f, .92f); root.sizeDelta = Vector2.zero;
                var title = MakeText(_panel.transform, "Video Library", 28); title.GetComponent<RectTransform>().anchorMin = new Vector2(.05f,.85f); title.GetComponent<RectTransform>().anchorMax = new Vector2(.95f,.98f);
                _statusText = MakeText(_panel.transform, "Ready", 16); _statusText.color = new Color(.7f,.8f,1f,1); _statusText.alignment = TextAnchor.UpperLeft; var stRect = _statusText.GetComponent<RectTransform>(); stRect.anchorMin = new Vector2(.05f,.78f); stRect.anchorMax = new Vector2(.95f,.85f); stRect.sizeDelta = Vector2.zero;
                var close = MakeButton(_panel.transform, "Close", new Vector2(.78f,.02f), new Vector2(.95f,.12f)); close.onClick.AddListener(Close);
                BuildList();
                _panel.SetActive(false);
                Debug.Log("[MediaLibraryUI] Build completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MediaLibraryUI] Build failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                _list = null;
            }
        }

        private void BuildList()
        {
            var listGo = new GameObject("MediaList");
            listGo.transform.SetParent(_panel.transform, false);
            // IMPORTANT: AddComponent<RectTransform>() destroys the existing Transform component
            // and replaces it with a RectTransform. So we must set _list AFTER adding components,
            // otherwise _list references a destroyed Transform (== null returns true, instId=0).
            var listRect = listGo.AddComponent<RectTransform>(); listRect.anchorMin = new Vector2(.05f,.15f); listRect.anchorMax = new Vector2(.95f,.82f); listRect.sizeDelta = Vector2.zero;
            var layout = listGo.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.padding = new RectOffset(8, 8, 8, 8);
            // NOTE: ContentSizeFitter removed — it was calculating preferred height as 0
            // because children hadn't been laid out yet, making the entire list invisible.
            // The list now fills its parent via anchors (0.15-0.82 vertical).
            _list = listGo.transform;
            Debug.Log($"[MediaLibraryUI] BuildList: instance={GetInstanceID()} _list set, refNull={ReferenceEquals(_list, null)} instId={_list.GetInstanceID()}");
        }

        private IEnumerator Refresh()
        {
            if (_list == null || _catalog == null)
            {
                SetStatus($"Error: listNull={_list == null} catalogNull={_catalog == null}");
                Debug.LogWarning("[MediaLibraryUI] Refresh skipped: _list or _catalog is null");
                yield break;
            }
            foreach (Transform child in _list) Destroy(child.gameObject);
            SetStatus($"Loading from {_catalog.baseUrl} ...");
            Debug.Log($"[MediaLibraryUI] Fetching media from {_catalog.baseUrl}/v1/media");
            yield return _catalog.GetMedia((items, error) => {
                if (!string.IsNullOrEmpty(error)) { SetStatus("Request failed: " + error); AddText("Media request failed: " + error, 20); return; }
                if (items == null || items.Count == 0) { SetStatus("No shared videos found"); AddText("No shared videos", 22); return; }
                SetStatus($"Found {items.Count} video(s)");
                foreach (var item in items) AddItem(item);
                var listRect = _list as RectTransform;
                // Force layout group to position children immediately (otherwise they stay at default pos)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
                Debug.Log($"[MediaLibraryUI] Added {items.Count} items. List childCount={_list.childCount}, listSize={listRect.rect.size}, listPos={listRect.anchoredPosition}");
                for (int i = 0; i < _list.childCount; i++)
                {
                    var child = _list.GetChild(i);
                    var childRect = child as RectTransform;
                    Debug.Log($"[MediaLibraryUI]   child[{i}] name={child.name} active={child.gameObject.activeSelf} pos={childRect?.anchoredPosition} size={childRect?.rect.size}");
                }
            });
        }

        private void AddItem(MediaItemDto item)
        {
            var button = MakeListButton(_list, item.name + (item.seekable ? "" : " (no seek)"));
            button.onClick.AddListener(() => {
                Debug.Log($"[MediaLibraryUI] Button clicked: {item.name} (id={item.id}) playbackNull={_playback == null}");
                StartCoroutine(Play(item));
            });
        }

        private IEnumerator Play(MediaItemDto item)
        {
            if (_catalog == null) { Debug.LogWarning("[MediaLibraryUI] Play skipped: catalog is null"); yield break; }
            Debug.Log($"[MediaLibraryUI] Play: requesting token for {item.id}");
            yield return _catalog.RequestPlayToken(item.id, (token, error) => {
                Debug.Log($"[MediaLibraryUI] Play token callback: tokenEmpty={string.IsNullOrEmpty(token)} error={error}");
                if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(token))
                {
                    var url = _catalog.BuildContentUrl(item.id, token);
                    Debug.Log($"[MediaLibraryUI] Play: calling PlayUrl playbackNull={_playback == null}");
                    _playback?.PlayUrl(url);
                }
                if (!string.IsNullOrEmpty(error)) { Debug.LogError($"[MediaLibraryUI] Play token error: {error}"); AddText("Play failed: " + error, 18); }
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
            // VerticalLayoutGroup uses preferredHeight; without LayoutElement it defaults to 0
            // (Image has no sprite, Text alone doesn't report layout height), making buttons invisible.
            var layout = button.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 55; layout.minHeight = 55;
            return button;
        }
    }
}
