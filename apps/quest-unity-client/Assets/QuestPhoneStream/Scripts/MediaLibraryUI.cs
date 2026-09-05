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
        private System.Action _onClose;
        private Slider _progress;
        private Slider _volume;
        private Button _playPause;
        private Text _timeText;
        private Button _modeButton, _stereoButton, _eyeButton;
        private Button _flatScaleDownButton, _flatScaleUpButton, _flatRotateButton, _flatResetButton;
        private MediaItemDto _selectedItem;
        private MediaVideoProfile _selectedProfile = MediaVideoProfile.Default;
        private bool _profileInitialized;
        private System.Action<bool, string> _onAvailabilityChanged;
        private Coroutine _probeRoutine;

        public void Initialize(Canvas canvas, MediaCatalogClient catalog, MediaPlaybackController playback, System.Func<string> baseUrl)
        {
            _canvas = canvas; _catalog = catalog; _playback = playback; _baseUrl = baseUrl;
            Build();
        }

        public void SetOnClose(System.Action onClose) => _onClose = onClose;
        public void SetAvailabilityHandler(System.Action<bool, string> handler) => _onAvailabilityChanged = handler;

        public void ProbeAvailability()
        {
            if (_catalog == null)
            {
                _onAvailabilityChanged?.Invoke(false, "Media catalog is unavailable");
                return;
            }
            var url = _baseUrl?.Invoke();
            if (!string.IsNullOrWhiteSpace(url)) _catalog.baseUrl = url.Trim();
            if (_probeRoutine != null) StopCoroutine(_probeRoutine);
            _onAvailabilityChanged?.Invoke(false, null);
            _probeRoutine = StartCoroutine(ProbeRoutine());
        }

        private IEnumerator ProbeRoutine()
        {
            yield return _catalog.GetMedia((items, error) =>
                _onAvailabilityChanged?.Invoke(string.IsNullOrEmpty(error), error));
            _probeRoutine = null;
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
            _onAvailabilityChanged?.Invoke(false, null);
            StartCoroutine(Refresh());
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
            _onClose?.Invoke();
        }

        private void LateUpdate()
        {
            if (_panel == null || !_panel.activeInHierarchy || _playback == null) return;
            var duration = _playback.Duration;
            if (duration > 0)
            {
                _progress?.SetValueWithoutNotify(Mathf.Clamp01((float)(_playback.CurrentTime / duration)));
                if (_timeText != null) _timeText.text = FormatTime(_playback.CurrentTime) + " / " + FormatTime(duration);
            }
            if (_playPause != null)
            {
                var label = _playPause.GetComponentInChildren<Text>();
                if (label != null) label.text = _playback.State == MediaPlaybackState.Playing ? "Pause" : "Resume";
            }
            UpdateFlatInteractionControls();
        }

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
                BuildProfileControls(_panel.transform);
                BuildFlatInteractionControls(_panel.transform);
                BuildPlaybackControls(_panel.transform);
                var close = MakeButton(_panel.transform, "Back", new Vector2(.78f,.02f), new Vector2(.95f,.12f)); close.onClick.AddListener(Close);
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

        private void BuildPlaybackControls(Transform parent)
        {
            _progress = MakeSlider(parent, new Vector2(.05f,.16f), new Vector2(.74f,.22f));
            _progress.onValueChanged.AddListener(value => {
                if (_playback != null && _playback.Duration > 0)
                    _playback.Seek(value * _playback.Duration);
            });
            _timeText = MakeText(parent, "00:00 / 00:00", 15).GetComponent<Text>();
            var timeRect = _timeText.GetComponent<RectTransform>();
            timeRect.anchorMin = new Vector2(.05f,.10f); timeRect.anchorMax = new Vector2(.30f,.16f); timeRect.sizeDelta = Vector2.zero;
            var back = MakeButton(parent, "-10", new Vector2(.32f,.02f), new Vector2(.43f,.12f));
            back.onClick.AddListener(() => _playback?.Seek((_playback?.CurrentTime ?? 0) - 10));
            _playPause = MakeButton(parent, "Play", new Vector2(.45f,.02f), new Vector2(.58f,.12f));
            _playPause.onClick.AddListener(() => {
                if (_playback == null) return;
                if (_playback.State == MediaPlaybackState.Playing) _playback.Pause(); else _playback.Resume();
            });
            var forward = MakeButton(parent, "+10", new Vector2(.60f,.02f), new Vector2(.71f,.12f));
            forward.onClick.AddListener(() => _playback?.Seek((_playback?.CurrentTime ?? 0) + 10));
            _volume = MakeSlider(parent, new Vector2(.05f,.02f), new Vector2(.26f,.08f));
            _volume.value = 1f;
            _volume.onValueChanged.AddListener(value => _playback?.SetVolume(value));
        }

        private void BuildProfileControls(Transform parent)
        {
            _modeButton = MakeButton(parent, "Mode: Flat", new Vector2(.05f,.72f), new Vector2(.35f,.78f));
            _modeButton.onClick.AddListener(() => {
                if (_selectedProfile.projection == ProjectionMode.Flat) {
                    _selectedProfile.projection = ProjectionMode.Equirectangular;
                    _selectedProfile.fov = 180;
                } else if (_selectedProfile.fov == 180) {
                    _selectedProfile.fov = 360;
                } else {
                    _selectedProfile = MediaVideoProfile.Default;
                }
                ApplySelectedProfile();
            });
            _stereoButton = MakeButton(parent, "Stereo: Mono", new Vector2(.38f,.72f), new Vector2(.65f,.78f));
            _stereoButton.onClick.AddListener(() => {
                _selectedProfile.stereo = _selectedProfile.stereo == StereoMode.Mono ? StereoMode.Sbs : StereoMode.Mono;
                ApplySelectedProfile();
            });
            _eyeButton = MakeButton(parent, "Eye: L/R", new Vector2(.68f,.72f), new Vector2(.95f,.78f));
            _eyeButton.onClick.AddListener(() => {
                _selectedProfile.eyeOrder = _selectedProfile.eyeOrder == EyeOrder.Lr ? EyeOrder.Rl : EyeOrder.Lr;
                ApplySelectedProfile();
            });
            UpdateProfileControls();
        }

        private void BuildFlatInteractionControls(Transform parent)
        {
            _flatScaleDownButton = MakeButton(parent, "-", new Vector2(.05f,.62f), new Vector2(.15f,.68f));
            _flatScaleDownButton.onClick.AddListener(() => _playback?.flatPanelController?.ScaleDown());
            _flatScaleUpButton = MakeButton(parent, "+", new Vector2(.16f,.62f), new Vector2(.26f,.68f));
            _flatScaleUpButton.onClick.AddListener(() => _playback?.flatPanelController?.ScaleUp());
            _flatRotateButton = MakeButton(parent, "Rotate", new Vector2(.27f,.62f), new Vector2(.58f,.68f));
            _flatRotateButton.onClick.AddListener(() => _playback?.flatPanelController?.RotateOrientation());
            _flatResetButton = MakeButton(parent, "Reset", new Vector2(.59f,.62f), new Vector2(.95f,.68f));
            _flatResetButton.onClick.AddListener(() => _playback?.flatPanelController?.ResetPose());
            UpdateFlatInteractionControls();
        }

        private void UpdateFlatInteractionControls()
        {
            var active = _playback?.flatPanelController?.IsFlatActive == true;
            foreach (var button in new[] { _flatScaleDownButton, _flatScaleUpButton, _flatRotateButton, _flatResetButton })
            {
                if (button == null) continue;
                button.gameObject.SetActive(active);
                button.interactable = active;
            }
        }

        private void ApplySelectedProfile()
        {
            _profileInitialized = true;
            _selectedProfile = _selectedProfile.Normalize();
            UpdateProfileControls();
            _playback?.ApplyProfile(_selectedProfile);
        }

        private void UpdateProfileControls()
        {
            if (_modeButton != null)
            {
                var label = _modeButton.GetComponentInChildren<Text>();
                if (label != null) label.text = _selectedProfile.projection == ProjectionMode.Flat
                    ? "Mode: Flat" : "Mode: " + _selectedProfile.fov + "°";
            }
            if (_stereoButton != null)
            {
                var label = _stereoButton.GetComponentInChildren<Text>();
                if (label != null) label.text = "Stereo: " + (_selectedProfile.stereo == StereoMode.Sbs ? "SBS" : "Mono");
            }
            if (_eyeButton != null)
            {
                var label = _eyeButton.GetComponentInChildren<Text>();
                if (label != null) label.text = "Eye: " + (_selectedProfile.eyeOrder == EyeOrder.Rl ? "R/L" : "L/R");
            }
        }

        private static Slider MakeSlider(Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Slider"); go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();
            var background = new GameObject("Background"); background.transform.SetParent(go.transform, false);
            var bgImage = background.AddComponent<Image>(); bgImage.color = new Color(.18f,.22f,.3f,1);
            var bgRect = background.GetComponent<RectTransform>(); bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.sizeDelta = Vector2.zero;
            var fill = new GameObject("Fill"); fill.transform.SetParent(go.transform, false);
            var fillImage = fill.AddComponent<Image>(); fillImage.color = new Color(.25f,.65f,.9f,1);
            var fillRect = fill.GetComponent<RectTransform>(); fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
            slider.fillRect = fillRect; slider.minValue = 0; slider.maxValue = 1;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = min; rt.anchorMax = max; rt.sizeDelta = Vector2.zero;
            return slider;
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            var span = System.TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"mm\:ss");
        }

        private void BuildList()
        {
            var listGo = new GameObject("MediaList");
            listGo.transform.SetParent(_panel.transform, false);
            // IMPORTANT: AddComponent<RectTransform>() destroys the existing Transform component
            // and replaces it with a RectTransform. So we must set _list AFTER adding components,
            // otherwise _list references a destroyed Transform (== null returns true, instId=0).
            var listRect = listGo.AddComponent<RectTransform>(); listRect.anchorMin = new Vector2(.05f,.23f); listRect.anchorMax = new Vector2(.95f,.60f); listRect.sizeDelta = Vector2.zero;
            var layout = listGo.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.padding = new RectOffset(8, 8, 8, 8);
            // NOTE: ContentSizeFitter removed — it was calculating preferred height as 0
            // because children hadn't been laid out yet, making the entire list invisible.
            // The list now leaves room for Flat interaction controls above playback.
            _list = listGo.transform;
            Debug.Log($"[MediaLibraryUI] BuildList: instance={GetInstanceID()} _list set, refNull={ReferenceEquals(_list, null)} instId={_list.GetInstanceID()}");
        }

        private IEnumerator Refresh()
        {
            if (_list == null || _catalog == null)
            {
                _onAvailabilityChanged?.Invoke(false, "Media catalog is unavailable");
                SetStatus($"Error: listNull={_list == null} catalogNull={_catalog == null}");
                Debug.LogWarning("[MediaLibraryUI] Refresh skipped: _list or _catalog is null");
                yield break;
            }
            foreach (Transform child in _list) Destroy(child.gameObject);
            SetStatus($"Loading from {_catalog.baseUrl} ...");
            Debug.Log($"[MediaLibraryUI] Fetching media from {_catalog.baseUrl}/v1/media");
            yield return _catalog.GetMedia((items, error) => {
                if (!string.IsNullOrEmpty(error)) { _onAvailabilityChanged?.Invoke(false, error); SetStatus("Request failed: " + error); AddText("Media request failed: " + error, 20); return; }
                _onAvailabilityChanged?.Invoke(true, null);
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
            var profile = MediaVideoProfile.From(item);
            var button = MakeListButton(_list, item.name + "  ·  " + profile.Label + (item.seekable ? "" : " (no seek)"));
            button.onClick.AddListener(() => {
                Debug.Log($"[MediaLibraryUI] Button clicked: {item.name} (id={item.id}) playbackNull={_playback == null}");
                _selectedItem = item;
                if (!_profileInitialized)
                {
                    _selectedProfile = MediaVideoProfile.From(item);
                    _profileInitialized = true;
                }
                UpdateProfileControls();
                StartCoroutine(Play(item, _selectedProfile));
            });
        }

        private IEnumerator Play(MediaItemDto item, MediaVideoProfile profile)
        {
            if (_catalog == null) { Debug.LogWarning("[MediaLibraryUI] Play skipped: catalog is null"); yield break; }
            Debug.Log($"[MediaLibraryUI] Play: requesting token for {item.id}");
            yield return _catalog.RequestPlayToken(item.id, (token, error) => {
                Debug.Log($"[MediaLibraryUI] Play token callback: tokenEmpty={string.IsNullOrEmpty(token)} error={error}");
                if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(token))
                {
                    var url = _catalog.BuildContentUrl(item.id, token);
                    Debug.Log($"[MediaLibraryUI] Play: calling PlayUrl playbackNull={_playback == null}");
                    _playback?.PlayUrl(url, profile);
                    SetStatus("Playing: " + item.name);
                }
                if (!string.IsNullOrEmpty(error)) { Debug.LogError($"[MediaLibraryUI] Play token error: {error}"); AddText("Play failed: " + error, 18); }
            });
        }

        private Text AddText(string value, int size)
        {
            var text = MakeText(_list, value, size); text.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40); return text;
        }

        private void OnDestroy()
        {
            if (_probeRoutine != null) StopCoroutine(_probeRoutine);
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
