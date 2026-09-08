using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using QuestPhoneStream.Interaction;

namespace QuestPhoneStream
{
    public sealed class QuestWebRtcReceiver : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public ControlChannel controlChannel;
        public Camera xrCamera;
        public QuestXrUiRig xrUiRig;
        public MediaPlaybackController mediaPlayback;
        public MediaDeviceDiscovery mediaDiscovery;
        public Transform mediaPanelAnchor;
        public Material targetMaterial;
        public Material vrMaterialTemplate;
        public Material panoramicMaterialTemplate;
        public VrBackend vrBackend = VrBackend.UnityPanoramic;
        public Renderer phoneScreenRenderer;
        public int textureWidth = 1280, textureHeight = 720;
        public bool connectOnStart = true;
        private SpatialPanelShell _screenShell;
        private PanelInputMapper _screenMapper;
        private string _screenPlatform;
        public bool CanSendAndroidSurfaceInput => _screenPlatform == "android" &&
            SupportsControl && IsControlConnected && ActiveDevice.Capabilities.IsAuthorized("display.control") &&
            (mediaPlayback == null || !mediaPlayback.IsMediaMode) && phoneScreenRenderer != null && phoneScreenRenderer.enabled;
        private bool _screenInteractive = true;
        private void LateUpdate()
        {
            var interactive = phoneScreenRenderer != null && phoneScreenRenderer.enabled &&
                (mediaPlayback == null || !mediaPlayback.IsMediaMode);
            if (_screenShell == null || interactive == _screenInteractive) return;
            _screenInteractive = interactive;
            _screenShell.Router.screenCollider.enabled = interactive;
            _screenShell.SetInteractive(interactive);
        }
        public void BindScreenSurface(SpatialPanelShell shell, PanelInputMapper mapper)
        { _screenShell = shell; _screenMapper = mapper; RefreshScreenHandler(); }
        private void RefreshScreenHandler()
        {
            if (_screenShell == null) return;
            _screenShell.Input.SetHandler(PanelSurfaceInputFactory.Screen(_screenPlatform, _screenMapper, () => CanSendAndroidSurfaceInput));
        }
        private void OnPeerPlatform(string peer, string platform)
        {
            if (ActiveDevice == null || !ActiveDevice.MatchesPeer(peer)) return;
            _screenPlatform = platform;
            RefreshScreenHandler();
        }

        private RTCPeerConnection _peer;
        private RenderTexture _renderTexture;
        private VideoStreamTrack _videoTrack;
        private Unity.WebRTC.OnVideoReceived _videoReceived;
        private Texture _receivedTexture;
        private Coroutine _webRtcUpdate, _videoRender, _offerRoutine;
        private SettingsUI _settingsUI;
        private QuestHomeUI _homeUI;
        private PanelInputMapper _panelInput;
        private string _negotiationId;
        private ActiveDeviceContext _activeDevice;
        private bool _remoteReady, _handlingOffer, _peerConnected, _hasFrame;
        public string PeerConnectionState { get; private set; } = "None";
        private bool _mediaProbeReady, _mediaProbeChecking, _mediaProbeFailed;
        private string _mediaProbeUrl;
        private float _mediaProbeAt = -Mathf.Infinity;
        private const float MediaProbeTtlSeconds = 30f;
        private readonly Queue<IceCandidateDto> _pendingIce = new Queue<IceCandidateDto>();

        public bool HasVideoFrame => _hasFrame;
        public bool IsPeerConnected => _peerConnected;
        public bool IsControlConnected => controlChannel != null && controlChannel.IsOpen;
        public int VideoWidth => _receivedTexture == null ? 0 : _receivedTexture.width;
        public int VideoHeight => _receivedTexture == null ? 0 : _receivedTexture.height;
        public string VideoSource => mediaPlayback != null && mediaPlayback.IsMediaMode ? "Media" : "Phone";
        public string SelectedAndroidDeviceId => signaling == null ? string.Empty : signaling.androidDeviceId;
        public string ActiveAndroidDeviceId => signaling == null ? string.Empty : signaling.ActiveAndroidDeviceId;
        public string ActiveSessionId => signaling == null ? string.Empty : signaling.ActiveSessionId;
        public bool AcceptSpatialMessage(SpatialEnvelope message) => signaling != null && signaling.AcceptSpatialMessage(message);
        public ActiveDeviceContext ActiveDevice => _activeDevice;
        public event Action<ActiveDeviceContext> ActiveDeviceChanged;
        public MediaDeviceInfo SelectedMediaDevice
        {
            get
            {
                if (mediaDiscovery == null || _activeDevice == null) return null;
                mediaDiscovery.TryGetDevice(_activeDevice.DeviceId, out var device);
                return device;
            }
        }
        public bool HasSelectedDevice => _activeDevice != null && !_activeDevice.IsLost;
        public bool SupportsScreen => HasSelectedDevice && _activeDevice.Capabilities.Supports("display.publish");
        public bool SupportsMedia => HasSelectedDevice &&
            (_activeDevice.Capabilities.Supports("media.list") || _activeDevice.Capabilities.Supports("media.open"));
        public bool SupportsControl => HasSelectedDevice && _activeDevice.Capabilities.Supports("display.control");
        public bool HasMediaUrl => SupportsMedia && !string.IsNullOrWhiteSpace(CurrentMediaUrl);
        public bool IsMediaStale => HasMediaUrl && _mediaProbeReady && _mediaProbeUrl == CurrentMediaUrl &&
            Time.unscaledTime - _mediaProbeAt > MediaProbeTtlSeconds;
        public bool IsMediaReady => HasMediaUrl && _mediaProbeReady && _mediaProbeUrl == CurrentMediaUrl && !IsMediaStale;
        public bool IsMediaChecking => HasMediaUrl &&
            ((!_mediaProbeReady && !_mediaProbeFailed) || _mediaProbeChecking || _mediaProbeUrl != CurrentMediaUrl || IsMediaStale);
        public bool IsMediaFailed => HasMediaUrl && _mediaProbeFailed && _mediaProbeUrl == CurrentMediaUrl;
        public bool HasReadyMediaDevice => mediaDiscovery != null && mediaDiscovery.HasReadyDevice;

        private string CurrentMediaUrl => _activeDevice != null ? _activeDevice.MediaBaseUrl.Trim() : string.Empty;

        /// <summary>
        /// Creates the dedicated unreliable/unordered Spatial data channel on the
        /// existing WebRTC peer. A peer-created bootstrap DataChannel can negotiate
        /// SCTP without implying display.control support, so control is not a gate.
        /// </summary>
        public RTCDataChannel CreateSpatialDataChannel()
        {
            if (_peer == null || !_peerConnected) return null;
            try
            {
                return _peer.CreateDataChannel("spatial", new RTCDataChannelInit
                {
                    ordered = false,
                    maxRetransmits = 0,
                    protocol = "qps-spatial-v1"
                });
            }
            catch (Exception error)
            {
                Debug.LogWarning("[QuestPhoneStream] Spatial DataChannel unavailable: " + error.Message);
                return null;
            }
        }

        private void Start()
        {
            if (signaling == null || controlChannel == null || xrCamera == null || xrUiRig == null || xrUiRig.actionAsset == null)
            {
                Debug.LogError("[QuestPhoneStream] Receiver requires signaling, control, XR camera and UI rig");
                enabled = false;
                return;
            }
            xrUiRig.Initialize(xrCamera, this);
            EnsureMediaPlaybackPanel();
            EnsureMediaDiscovery();
            EnsureHomeUI();
            signaling.MessageReceived += OnSignalMessage;
            signaling.NegotiationInvalidated += ResetPeer;
            signaling.PeerCapabilitiesReceived += OnPeerCapabilities;
            signaling.PeerPlatformReceived += OnPeerPlatform;
            signaling.PeerCapabilitiesChanged += OnPeerCapabilities;
            mediaDiscovery.DevicesChanged += OnDiscoveredDevicesChanged;
            _webRtcUpdate = StartCoroutine(WebRTC.Update());
            _videoRender = StartCoroutine(RenderVideoAtEndOfFrame());
            if (connectOnStart) _ = signaling.ReconnectAsync();
        }

        private void EnsureMediaPlaybackPanel()
        {
            if (mediaPlayback != null)
            {
                if (mediaPlayback.vrRenderer != null) mediaPlayback.vrRenderer.vrBackend = vrBackend;
                mediaPlayback.vrRenderer?.Initialize(xrCamera, mediaPlayback.renderer, vrMaterialTemplate, panoramicMaterialTemplate);
                mediaPlayback.phoneScreenRenderer = phoneScreenRenderer;
                ConfigureFlatMediaPanel();
                return;
            }
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "MediaPanel";
            if (mediaPanelAnchor != null)
            {
                panel.transform.position = mediaPanelAnchor.position + mediaPanelAnchor.forward * 0.015f;
                panel.transform.rotation = mediaPanelAnchor.rotation;
                panel.transform.localScale = mediaPanelAnchor.lossyScale;
            }
            else
            {
                panel.transform.SetParent(transform, false);
                panel.transform.localPosition = new Vector3(0, 1.45f, 1.22f);
                panel.transform.localScale = new Vector3(.72f, 1.6f, 1f);
            }
            var meshRenderer = panel.GetComponent<MeshRenderer>();
            if (targetMaterial != null) meshRenderer.material = new Material(targetMaterial);
            mediaPlayback = panel.AddComponent<MediaPlaybackController>();
            mediaPlayback.renderer.targetRenderer = meshRenderer;
            mediaPlayback.vrRenderer.vrBackend = vrBackend;
            mediaPlayback.vrRenderer.Initialize(xrCamera, mediaPlayback.renderer, vrMaterialTemplate, panoramicMaterialTemplate);
            mediaPlayback.phoneScreenRenderer = phoneScreenRenderer;
            ConfigureFlatMediaPanel();
            panel.SetActive(false);
        }

        private void ConfigureFlatMediaPanel()
        {
            if (mediaPlayback == null) return;
            if (mediaPlayback.flatPanelController == null)
                mediaPlayback.flatPanelController = mediaPlayback.gameObject.GetComponent<FlatMediaPanelController>() ??
                    mediaPlayback.gameObject.AddComponent<FlatMediaPanelController>();
            var target = mediaPlayback.renderer?.targetRenderer ?? mediaPlayback.gameObject.GetComponent<Renderer>();
            mediaPlayback.flatPanelController.Initialize(xrCamera, target);
            mediaPlayback.flatPanelController.ConfigureBackend(xrUiRig != null ? xrUiRig.PanelDependencies : null);
        }

        private void EnsureMediaDiscovery()
        {
            if (mediaDiscovery == null)
                mediaDiscovery = gameObject.GetComponent<MediaDeviceDiscovery>() ?? gameObject.AddComponent<MediaDeviceDiscovery>();
            mediaDiscovery.StartDiscovery();
        }

        public bool SelectDevice(string deviceId)
        {
            if (mediaDiscovery == null || !mediaDiscovery.TryGetReadyDevice(deviceId, out var device)) return false;
            _activeDevice = ActiveDeviceContext.FromDiscovered(device);
            _screenPlatform = null;
            RefreshScreenHandler();
            EnsureSettingsUI();
            // Screen-only publishers (e.g. the macOS sender) advertise no media
            // capability; never point the catalog/probe at their NSD port, which is
            // not an HTTP server. Only media-capable devices get a catalog base URL.
            var mediaCapable = SupportsMedia;
            if (mediaCapable)
            {
                _settingsUI.SetMediaBaseUrl(_activeDevice.MediaBaseUrl);
                _mediaProbeReady = false;
                _mediaProbeChecking = false;
                _mediaProbeFailed = false;
                _mediaProbeAt = -Mathf.Infinity;
                _mediaProbeUrl = null;
            }
            _settingsUI.ApplyDiscoveredSignaling(_activeDevice.SignalingUrl, _activeDevice.StreamId);
            // Persist discovered endpoint so restart keeps the same peer.
            if (!string.IsNullOrWhiteSpace(_activeDevice.SignalingUrl))
                PlayerPrefs.SetString("QuestPhoneStream_SignalingUrl_v2", _activeDevice.SignalingUrl.Trim());
            if (!string.IsNullOrWhiteSpace(_activeDevice.StreamId))
                PlayerPrefs.SetString("QuestPhoneStream_AndroidDeviceId", _activeDevice.StreamId.Trim());
            PlayerPrefs.Save();
            // Device selection may preconnect the display transport for latency, but does not
            // change the selected content mode. Screen, Media, and Control stay explicit actions.
            if (signaling != null && !string.IsNullOrWhiteSpace(_activeDevice.SignalingUrl))
            {
                Debug.Log($"[QuestPhoneStream] Background preconnect after device select url={_activeDevice.SignalingUrl} android={_activeDevice.StreamId}");
                _ = signaling.ReconnectAsync();
            }
            ActiveDeviceChanged?.Invoke(_activeDevice);
            Debug.Log($"[QuestPhoneStream] Selected device name={_activeDevice.Name} id={_activeDevice.DeviceId} baseUrl={(mediaCapable ? _activeDevice.MediaBaseUrl : "(none, screen-only)")} mediaCapable={mediaCapable}");
            _homeUI?.RefreshStatus();
            return true;
        }

        // Compatibility entry point for existing UI/tests while terminology migrates to Device.
        public bool SelectMediaDevice(string deviceId) => SelectDevice(deviceId);

        public void ToggleSettings()
        {
            EnsureSettingsUI();
            if (_settingsUI.IsVisible) _settingsUI.Hide();
            else _settingsUI.ShowAdvanced();
        }

        public void ToggleHome()
        {
            if (_homeUI == null) EnsureHomeUI();
            if (_settingsUI != null && _settingsUI.IsVisible) _settingsUI.Hide();
            _homeUI?.Toggle();
        }

        public void ShowHome()
        {
            if (_homeUI == null) EnsureHomeUI();
            _settingsUI?.Hide();
            _homeUI?.Show();
        }

        public void ProbeMedia()
        {
            if (!HasMediaUrl) return;
            EnsureSettingsUI();
            _settingsUI.mediaLibrary?.ProbeAvailability();
        }

        public void OpenVideoLibrary()
        {
            if (!SupportsMedia) return;
            EnsureSettingsUI();
            _settingsUI.SetAdvancedVisible(false);
            _settingsUI.Show();
            _settingsUI.mediaLibrary?.Open();
            _homeUI?.Hide();
        }

        public void SetPhoneScreenMode()
        {
            if (!SupportsScreen) return;
            mediaPlayback?.SetPhoneScreenMode();
            if (phoneScreenRenderer != null) phoneScreenRenderer.enabled = true;
            _homeUI?.Show();
        }

        private void EnsureSettingsUI()
        {
            if (_settingsUI != null) return;
            var settingsGo = new GameObject("SettingsUI");
            settingsGo.transform.SetParent(transform, false);
            _settingsUI = settingsGo.AddComponent<SettingsUIFactory>().Initialize(signaling, xrCamera, mediaPlayback, this);
            _settingsUI.onBackToHome = ShowHome;
            _settingsUI.mediaLibrary?.SetAvailabilityHandler((ready, error) => {
                _mediaProbeUrl = CurrentMediaUrl;
                _mediaProbeChecking = !ready && string.IsNullOrEmpty(error);
                _mediaProbeReady = ready;
                _mediaProbeFailed = !ready && !string.IsNullOrEmpty(error);
                if (ready) _mediaProbeAt = Time.unscaledTime;
                _homeUI?.RefreshStatus();
            });
        }

        private void EnsureHomeUI()
        {
            if (_homeUI == null) _homeUI = gameObject.AddComponent<QuestHomeUI>();
            _homeUI.Initialize(signaling, xrCamera, this);
        }

        private void OnPeerCapabilities(string peerId, SpatialCapabilityDescriptor[] capabilities)
        {
            // The signaling client already isolates the active reconnect epoch. Keep a second
            // device identity check here so an A -> B switch cannot restore A's UI capability set.
            if (_activeDevice == null || !_activeDevice.MatchesPeer(peerId)) return;
            _activeDevice.Capabilities.ApplySpatial(capabilities);
            // Older Android hello descriptors omit platform. Their formal, Android-specific
            // accessibility permission identifies the existing control implementation.
            if (string.IsNullOrEmpty(_screenPlatform))
            {
                foreach (var capability in capabilities ?? Array.Empty<SpatialCapabilityDescriptor>())
                    if (capability?.name == "display.control" &&
                        Array.IndexOf(capability.permissions ?? Array.Empty<string>(), "android.accessibility_service") >= 0)
                        _screenPlatform = "android";
                RefreshScreenHandler();
            }
            ActiveDeviceChanged?.Invoke(_activeDevice);
            _homeUI?.RefreshStatus();
        }

        private void OnDiscoveredDevicesChanged()
        {
            if (_activeDevice == null || mediaDiscovery == null) return;
            if (mediaDiscovery.TryGetDevice(_activeDevice.DeviceId, out var device))
                _activeDevice.UpdateFromDiscovery(device);
            else
                _activeDevice.MarkLost();
            ActiveDeviceChanged?.Invoke(_activeDevice);
            _homeUI?.RefreshStatus();
        }

        private bool IsCurrent(RTCPeerConnection peer, string id) =>
            peer == _peer && id == _negotiationId && signaling.IsCurrentNegotiation(id);

        private void ResetPeer()
        {
            _negotiationId = null;
            if (_offerRoutine != null) { StopCoroutine(_offerRoutine); _offerRoutine = null; }
            _pendingIce.Clear();
            _remoteReady = _handlingOffer = _peerConnected = _hasFrame = false;
            PeerConnectionState = "None";
            _receivedTexture = null;
            controlChannel?.ResetChannel();
            if (_videoTrack != null)
            {
                _videoTrack.OnVideoReceived -= _videoReceived;
                _videoTrack.Dispose();
                _videoTrack = null;
                _videoReceived = null;
            }
            var old = _peer;
            _peer = null;
            old?.Close();
            old?.Dispose();
            if (targetMaterial != null) targetMaterial.mainTexture = null;
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        private void CreatePeerConnection(string id)
        {
            ResetPeer();
            _negotiationId = id;
            var config = new RTCConfiguration {
                iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
            };
            var peer = new RTCPeerConnection(ref config);
            _peer = peer;
            PeerConnectionState = "New";
            peer.OnIceCandidate = candidate =>
            {
                if (candidate == null || !IsCurrent(peer, id)) return;
                _ = signaling.SendIceAsync(new IceCandidateDto {
                    candidate = candidate.Candidate, sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex ?? 0
                }, id);
            };
            peer.OnConnectionStateChange = state =>
            {
                if (!IsCurrent(peer, id)) return;
                PeerConnectionState = state.ToString();
                if (state == RTCPeerConnectionState.Connected)
                {
                    _peerConnected = true;
                    signaling.ReportMediaState(id, ConnectionState.PeerConnected);
                    if (_hasFrame) signaling.ReportMediaState(id, ConnectionState.MediaConnected);
                }
                else if (state == RTCPeerConnectionState.Failed || state == RTCPeerConnectionState.Disconnected)
                    signaling.ReportMediaState(id, ConnectionState.MediaFailed);
            };
            peer.OnIceConnectionChange = state =>
            {
                if (IsCurrent(peer, id) && state == RTCIceConnectionState.Failed)
                    signaling.ReportMediaState(id, ConnectionState.IceFailed);
            };
            peer.OnDataChannel = channel =>
            {
                if (!IsCurrent(peer, id) || channel.Label != "control") { channel.Close(); channel.Dispose(); return; }
                controlChannel.Attach(channel);
            };
            peer.OnTrack = e =>
            {
                if (!IsCurrent(peer, id)) { e.Track.Dispose(); return; }
                if (e.Track is VideoStreamTrack track)
                {
                    if (_videoTrack != null)
                    {
                        _videoTrack.OnVideoReceived -= _videoReceived;
                        _videoTrack.Dispose();
                    }
                    _videoTrack = track;
                    _videoReceived = texture => {
                        if (IsCurrent(peer, id) && _videoTrack == track) OnVideoReceived(texture);
                    };
                    track.OnVideoReceived += _videoReceived;
                }
            };
        }

        private void OnVideoReceived(Texture texture)
        {
            if (_peer == null || texture == null || !signaling.IsCurrentNegotiation(_negotiationId)) return;
            EnsureRenderTexture(texture.width, texture.height);
            _receivedTexture = texture;
            if (targetMaterial != null) targetMaterial.mainTexture = _renderTexture;
            if (_panelInput == null) _panelInput = FindFirstObjectByType<PanelInputMapper>();
            _panelInput?.SetAndroidResolution(texture.width, texture.height);
        }

        private IEnumerator RenderVideoAtEndOfFrame()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                var source = _receivedTexture;
                var target = _renderTexture;
                var id = _negotiationId;
                if (source == null || target == null || !signaling.IsCurrentNegotiation(id)) continue;

                Graphics.Blit(source, target);
                if (_hasFrame) continue;
                _hasFrame = true;
                Debug.Log($"[QuestPhoneStream] Video frame {source.width}x{source.height} -> RT {target.width}x{target.height} " +
                          $"mat={targetMaterial?.name} phase=end-of-frame");
                if (_peerConnected) signaling.ReportMediaState(id, ConnectionState.MediaConnected);
            }
        }

        private void OnSignalMessage(SignalMessage message)
        {
            if (message.type == "session_created") { CreatePeerConnection(message.negotiationId); return; }
            if (_peer == null || message.negotiationId != _negotiationId) return;
            if (message.type == "offer")
            {
                if (_handlingOffer || _remoteReady) return;
                _handlingOffer = true;
                _offerRoutine = StartCoroutine(HandleOffer(message.sdp, _peer, _negotiationId));
            }
            else if (message.type == "ice" && message.candidate != null)
            {
                if (!_remoteReady)
                {
                    if (_pendingIce.Count >= 256) { signaling.ReportMediaState(_negotiationId, ConnectionState.IceFailed); return; }
                    _pendingIce.Enqueue(message.candidate);
                }
                else AddIce(message.candidate);
            }
        }

        private void AddIce(IceCandidateDto candidate)
        {
            using (var ice = new RTCIceCandidate(new RTCIceCandidateInit {
                candidate = candidate.candidate, sdpMid = candidate.sdpMid, sdpMLineIndex = candidate.sdpMLineIndex
            }))
            {
                if (!_peer.AddIceCandidate(ice))
                    signaling.ReportMediaState(_negotiationId, ConnectionState.IceFailed);
            }
        }

        private IEnumerator HandleOffer(string sdp, RTCPeerConnection peer, string id)
        {
            var offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
            var remoteOp = peer.SetRemoteDescription(ref offer);
            yield return remoteOp;
            if (!IsCurrent(peer, id)) yield break;
            if (remoteOp.IsError) { signaling.ReportMediaState(id, ConnectionState.MediaFailed); yield break; }
            _remoteReady = true;
            while (_pendingIce.Count > 0 && IsCurrent(peer, id)) AddIce(_pendingIce.Dequeue());
            if (!IsCurrent(peer, id)) yield break;
            var answerOp = peer.CreateAnswer();
            yield return answerOp;
            if (!IsCurrent(peer, id)) yield break;
            if (answerOp.IsError) { signaling.ReportMediaState(id, ConnectionState.MediaFailed); yield break; }
            var answer = answerOp.Desc;
            var localOp = peer.SetLocalDescription(ref answer);
            yield return localOp;
            if (!IsCurrent(peer, id)) yield break;
            if (localOp.IsError) { signaling.ReportMediaState(id, ConnectionState.MediaFailed); yield break; }
            _handlingOffer = false;
            _ = signaling.SendAnswerAsync(answer.sdp, id);
        }

        private void EnsureRenderTexture(int width, int height)
        {
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height) return;
            if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); }
            _renderTexture = new RenderTexture(width > 0 ? width : textureWidth, height > 0 ? height : textureHeight, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
        }

        private void OnDestroy()
        {
            mediaDiscovery?.StopDiscovery();
            if (signaling != null)
            {
                signaling.MessageReceived -= OnSignalMessage;
                signaling.NegotiationInvalidated -= ResetPeer;
                signaling.PeerCapabilitiesReceived -= OnPeerCapabilities;
                signaling.PeerPlatformReceived -= OnPeerPlatform;
                signaling.PeerCapabilitiesChanged -= OnPeerCapabilities;
            }
            if (mediaDiscovery != null) mediaDiscovery.DevicesChanged -= OnDiscoveredDevicesChanged;
            if (_videoRender != null) StopCoroutine(_videoRender);
            ResetPeer();
            if (_webRtcUpdate != null) StopCoroutine(_webRtcUpdate);
        }
    }
}
