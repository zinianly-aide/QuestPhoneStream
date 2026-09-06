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
        private SixDofMediaService _sixDof;
        private GaussianSplatPocRenderer _splat;
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
        private Button _recenterButton;
        private Button _flatScaleDownButton, _flatScaleUpButton, _flatRotateButton, _flatResetButton;
        private MediaItemDto _selectedItem;
        private MediaVideoProfile _selectedProfile = MediaVideoProfile.Default;
        private bool _manualProfileOverride;
        private System.Action<bool, string> _onAvailabilityChanged;
        private Coroutine _probeRoutine;

        public MediaRouteKind CurrentRoute { get; private set; } = MediaRouteKind.Video;

        public void Initialize(Canvas canvas, MediaCatalogClient catalog, MediaPlaybackController playback, System.Func<string> baseUrl)
        {
            _canvas = canvas;
            _catalog = catalog;
            _playback = playback;
            _baseUrl = baseUrl;
            ResolveSpatialRenderers();
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
            if (_panel == null) return;
            ResolveSpatialRenderers();
            _panel.SetActive(true);
            var urlFromSettings = _baseUrl?.Invoke();
            if (_catalog != null && !string.IsNullOrEmpty(urlFromSettings)) _catalog.baseUrl = urlFromSettings;
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
            if (_panel == null || !_panel.activeInHierarchy) return;
            var videoRoute = CurrentRoute == MediaRouteKind.Video;
            var duration = videoRoute && _playback != null ? _playback.Duration : 0;
            if (duration > 0)
            {
                _progress?.SetValueWithoutNotify(Mathf.Clamp01((float)(_playback.CurrentTime / duration)));
                if (_timeText != null) _timeText.text = FormatTime(_playback.CurrentTime) + " / " + FormatTime(duration);
            }
            else if (_timeText != null && !videoRoute) _timeText.text = CurrentRoute == MediaRouteKind.SixDof ? "6DoF provider" : "3DGS POC";
            if (_playPause != null)
            {
                _playPause.interactable = videoRoute;
                var label = _playPause.GetComponentInChildren<Text>();
                if (label != null) label.text = videoRoute && _playback != null && _playback.State == MediaPlaybackState.Playing ? "Pause" : "Resume";
            }
            if (_progress != null) _progress.interactable = videoRoute;
            if (_volume != null) _volume.interactable = videoRoute;
            UpdateFlatInteractionControls();
            UpdateRecenterControl();
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
            Debug.Log($"[MediaLibraryUI] Status: {message}");
        }

        private void Build()
        {
            if (_canvas == null) return;
            if (_panel != null && _list != null) return;
            if (_panel != null) { BuildList(); return; }

            _panel = new GameObject("VideoLibraryPanel");
            _panel.transform.SetParent(_canvas.transform, false);
            var image = _panel.AddComponent<Image>(); image.color = new Color(.06f, .07f, .1f, .98f);
            var root = _panel.GetComponent<RectTransform>(); root.anchorMin = new Vector2(.08f, .08f); root.anchorMax = new Vector2(.92f, .92f); root.sizeDelta = Vector2.zero;
            var title = MakeText(_panel.transform, "Media Library", 28); title.GetComponent<RectTransform>().anchorMin = new Vector2(.05f,.85f); title.GetComponent<RectTransform>().anchorMax = new Vector2(.95f,.98f);
            _statusText = MakeText(_panel.transform, "Ready", 16); _statusText.color = new Color(.7f,.8f,1f,1); _statusText.alignment = TextAnchor.UpperLeft; var stRect = _statusText.GetComponent<RectTransform>(); stRect.anchorMin = new Vector2(.05f,.78f); stRect.anchorMax = new Vector2(.95f,.85f); stRect.sizeDelta = Vector2.zero;
            BuildProfileControls(_panel.transform);
            BuildFlatInteractionControls(_panel.transform);
            BuildPlaybackControls(_panel.transform);
            var close = MakeButton(_panel.transform, "Back", new Vector2(.78f,.02f), new Vector2(.95f,.12f)); close.onClick.AddListener(Close);
            BuildList();
            _panel.SetActive(false);
        }

        private void BuildPlaybackControls(Transform parent)
        {
            _progress = MakeSlider(parent, new Vector2(.05f,.16f), new Vector2(.74f,.22f));
            _progress.onValueChanged.AddListener(value => { if (_playback != null && CurrentRoute == MediaRouteKind.Video && _playback.Duration > 0) _playback.Seek(value * _playback.Duration); });
            _timeText = MakeText(parent, "00:00 / 00:00", 15);
            var timeRect = _timeText.GetComponent<RectTransform>(); timeRect.anchorMin = new Vector2(.05f,.10f); timeRect.anchorMax = new Vector2(.30f,.16f); timeRect.sizeDelta = Vector2.zero;
            var back = MakeButton(parent, "-10", new Vector2(.32f,.02f), new Vector2(.43f,.12f));
            back.onClick.AddListener(() => { if (CurrentRoute == MediaRouteKind.Video) _playback?.Seek((_playback?.CurrentTime ?? 0) - 10); });
            _playPause = MakeButton(parent, "Play", new Vector2(.45f,.02f), new Vector2(.58f,.12f));
            _playPause.onClick.AddListener(() => {
                if (_playback == null || CurrentRoute != MediaRouteKind.Video) return;
                if (_playback.State == MediaPlaybackState.Playing) _playback.Pause(); else _playback.Resume();
            });
            var forward = MakeButton(parent, "+10", new Vector2(.60f,.02f), new Vector2(.71f,.12f));
            forward.onClick.AddListener(() => { if (CurrentRoute == MediaRouteKind.Video) _playback?.Seek((_playback?.CurrentTime ?? 0) + 10); });
            _volume = MakeSlider(parent, new Vector2(.05f,.02f), new Vector2(.26f,.08f));
            _volume.value = 1f;
            _volume.onValueChanged.AddListener(value => { if (CurrentRoute == MediaRouteKind.Video) _playback?.SetVolume(value); });
        }

        private void BuildProfileControls(Transform parent)
        {
            _modeButton = MakeButton(parent, "Mode: Flat", new Vector2(.05f,.72f), new Vector2(.35f,.78f));
            _modeButton.onClick.AddListener(() => {
                if (CurrentRoute != MediaRouteKind.Video) return;
                if (_selectedProfile.projection == ProjectionMode.Flat) { _selectedProfile.projection = ProjectionMode.Equirectangular; _selectedProfile.fov = 180; }
                else if (_selectedProfile.fov == 180) _selectedProfile.fov = 360;
                else _selectedProfile = MediaVideoProfile.Default;
                ApplySelectedProfile(true);
            });
            _stereoButton = MakeButton(parent, "Stereo: Mono", new Vector2(.38f,.72f), new Vector2(.65f,.78f));
            _stereoButton.onClick.AddListener(() => { if (CurrentRoute == MediaRouteKind.Video) { _selectedProfile.stereo = _selectedProfile.stereo == StereoMode.Mono ? StereoMode.Sbs : StereoMode.Mono; ApplySelectedProfile(true); } });
            _eyeButton = MakeButton(parent, "Eye: L/R", new Vector2(.68f,.72f), new Vector2(.95f,.78f));
            _eyeButton.onClick.AddListener(() => { if (CurrentRoute == MediaRouteKind.Video) { _selectedProfile.eyeOrder = _selectedProfile.eyeOrder == EyeOrder.Lr ? EyeOrder.Rl : EyeOrder.Lr; ApplySelectedProfile(true); } });
            _recenterButton = MakeButton(parent, "Recenter", new Vector2(.68f,.54f), new Vector2(.95f,.60f));
            _recenterButton.onClick.AddListener(() => _playback?.vrRenderer?.RecenterPanoramic());
            UpdateProfileControls();
            UpdateRecenterControl();
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
            var active = CurrentRoute == MediaRouteKind.Video && _playback != null && _playback.IsMediaMode &&
                _playback.Profile.projection == ProjectionMode.Flat && _playback.flatPanelController?.IsFlatActive == true;
            foreach (var button in new[] { _flatScaleDownButton, _flatScaleUpButton, _flatRotateButton, _flatResetButton })
            {
                if (button == null) continue;
                button.gameObject.SetActive(active);
                button.interactable = active;
            }
        }

        private void UpdateRecenterControl()
        {
            var active = CurrentRoute == MediaRouteKind.Video && _playback != null && _playback.IsMediaMode &&
                _playback.Profile.projection == ProjectionMode.Equirectangular && _playback.vrRenderer != null &&
                _playback.vrRenderer.vrBackend == VrBackend.UnityPanoramic && _playback.vrRenderer.IsPanoramicVisible;
            if (_recenterButton == null) return;
            _recenterButton.gameObject.SetActive(active);
            _recenterButton.interactable = active;
        }

        private void ApplySelectedProfile(bool manualOverride)
        {
            if (manualOverride) _manualProfileOverride = true;
            _selectedProfile = _selectedProfile.Normalize();
            UpdateProfileControls();
            if (CurrentRoute == MediaRouteKind.Video) _playback?.ApplyProfile(_selectedProfile);
        }

        private void UpdateProfileControls()
        {
            var video = CurrentRoute == MediaRouteKind.Video;
            if (_modeButton != null) { _modeButton.interactable = video; var label = _modeButton.GetComponentInChildren<Text>(); if (label != null) label.text = video ? (_selectedProfile.projection == ProjectionMode.Flat ? "Mode: Flat" : "Mode: " + _selectedProfile.fov + "°") : "Mode: Spatial"; }
            if (_stereoButton != null) { _stereoButton.interactable = video; var label = _stereoButton.GetComponentInChildren<Text>(); if (label != null) label.text = "Stereo: " + (_selectedProfile.stereo == StereoMode.Sbs ? "SBS" : "Mono"); }
            if (_eyeButton != null) { _eyeButton.interactable = video; var label = _eyeButton.GetComponentInChildren<Text>(); if (label != null) label.text = "Eye: " + (_selectedProfile.eyeOrder == EyeOrder.Rl ? "R/L" : "L/R"); }
        }

        private static Slider MakeSlider(Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Slider"); go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();
            var background = new GameObject("Background"); background.transform.SetParent(go.transform, false); var bgImage = background.AddComponent<Image>(); bgImage.color = new Color(.18f,.22f,.3f,1); var bgRect = background.GetComponent<RectTransform>(); bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.sizeDelta = Vector2.zero;
            var fill = new GameObject("Fill"); fill.transform.SetParent(go.transform, false); var fillImage = fill.AddComponent<Image>(); fillImage.color = new Color(.25f,.65f,.9f,1); var fillRect = fill.GetComponent<RectTransform>(); fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
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
            var listRect = listGo.AddComponent<RectTransform>(); listRect.anchorMin = new Vector2(.05f,.23f); listRect.anchorMax = new Vector2(.95f,.60f); listRect.sizeDelta = Vector2.zero;
            var layout = listGo.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.padding = new RectOffset(8, 8, 8, 8);
            _list = listGo.transform;
        }

        private IEnumerator Refresh()
        {
            if (_list == null || _catalog == null)
            {
                _onAvailabilityChanged?.Invoke(false, "Media catalog is unavailable");
                SetStatus("Media catalog is unavailable");
                yield break;
            }
            foreach (Transform child in _list) Destroy(child.gameObject);
            SetStatus($"Loading from {_catalog.baseUrl} ...");
            yield return _catalog.GetMedia((items, error) => {
                if (!string.IsNullOrEmpty(error)) { _onAvailabilityChanged?.Invoke(false, error); SetStatus("Request failed: " + error); AddText("Media request failed: " + error, 20); return; }
                _onAvailabilityChanged?.Invoke(true, null);
                if (items == null || items.Count == 0) { SetStatus("No shared media found"); AddText("No shared media", 22); return; }
                SetStatus($"Found {items.Count} item(s)");
                foreach (var item in items) AddItem(item);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_list as RectTransform);
            });
        }

        private void AddItem(MediaItemDto item)
        {
            var label = item.name + "  ·  " + item.RouteLabel + (item.seekable ? "" : " (no seek)");
            var button = MakeListButton(_list, label);
            button.onClick.AddListener(() => {
                _selectedItem = item;
                CurrentRoute = item.Route;
                if (!_manualProfileOverride && CurrentRoute == MediaRouteKind.Video) _selectedProfile = MediaVideoProfile.From(item);
                UpdateProfileControls();
                StartCoroutine(Play(item, _selectedProfile));
            });
        }

        private IEnumerator Play(MediaItemDto item, MediaVideoProfile profile)
        {
            if (_catalog == null) yield break;
            ResolveSpatialRenderers();
            yield return _catalog.RequestPlayToken(item.id, (token, error) => {
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(token))
                {
                    SetStatus("Play failed: " + (error ?? "token unavailable"));
                    return;
                }
                var contentUrl = _catalog.BuildContentUrl(item.id, token);
                var sourceUrl = MediaUrlBuilder.ResolveManifest(_catalog.baseUrl, item.manifestUrl, contentUrl);
                RoutePlayback(item, profile, sourceUrl);
            });
        }

        private void RoutePlayback(MediaItemDto item, MediaVideoProfile profile, string sourceUrl)
        {
            CurrentRoute = item.Route;
            switch (CurrentRoute)
            {
                case MediaRouteKind.SixDof:
                    _splat?.CancelLoad(clearAsset: true);
                    _playback?.Stop();
                    if (_sixDof == null || !_sixDof.TryPlay(item, sourceUrl))
                        SetStatus("6DoF unavailable · external volumetric provider required");
                    else SetStatus("Playing 6DoF POC: " + item.name);
                    break;
                case MediaRouteKind.GaussianSplat:
                    _sixDof?.StopPlayback();
                    _playback?.Stop();
                    if (_splat == null) SetStatus("3DGS POC renderer unavailable");
                    else if (_splat.LoadUrl(sourceUrl) == null && _splat.LoadState == GaussianSplatLoadState.Error)
                        SetStatus("3DGS load failed: " + _splat.LastError);
                    else SetStatus("Loading 3DGS POC: " + item.name);
                    break;
                default:
                    _sixDof?.StopPlayback();
                    _splat?.CancelLoad(clearAsset: true);
                    _playback?.PlayUrl(sourceUrl, profile);
                    SetStatus("Playing: " + item.name);
                    break;
            }
            UpdateProfileControls();
        }

        private void ResolveSpatialRenderers()
        {
            if (_sixDof == null) _sixDof = FindFirstObjectByType<SixDofMediaService>();
            if (_splat == null) _splat = FindFirstObjectByType<GaussianSplatPocRenderer>();
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
            var layout = button.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 55; layout.minHeight = 55;
            return button;
        }
    }
}
