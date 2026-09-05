using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class QuestWebRtcReceiver : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public ControlChannel controlChannel;
        public Camera xrCamera;
        public QuestXrUiRig xrUiRig;
        public Material targetMaterial;
        public int textureWidth = 1280, textureHeight = 720;
        public bool connectOnStart = true;

        private RTCPeerConnection _peer;
        private RenderTexture _renderTexture;
        private VideoStreamTrack _videoTrack;
        private Unity.WebRTC.OnVideoReceived _videoReceived;
        private Texture _receivedTexture;
        private Coroutine _webRtcUpdate, _videoRender, _offerRoutine;
        private SettingsUI _settingsUI;
        private PanelInputMapper _panelInput;
        private string _negotiationId;
        private bool _remoteReady, _handlingOffer, _peerConnected, _hasFrame;
        private readonly Queue<IceCandidateDto> _pendingIce = new Queue<IceCandidateDto>();

        private void Start()
        {
            if (signaling == null || controlChannel == null || xrCamera == null || xrUiRig == null || xrUiRig.actionAsset == null)
            {
                Debug.LogError("[QuestPhoneStream] Receiver requires signaling, control, XR camera and UI rig");
                enabled = false;
                return;
            }
            xrUiRig.Initialize(xrCamera, this);
            signaling.MessageReceived += OnSignalMessage;
            signaling.NegotiationInvalidated += ResetPeer;
            _webRtcUpdate = StartCoroutine(WebRTC.Update());
            _videoRender = StartCoroutine(RenderVideoAtEndOfFrame());
            if (connectOnStart) _ = signaling.ReconnectAsync();
        }

        public void ToggleSettings()
        {
            if (_settingsUI == null)
            {
                var settingsGo = new GameObject("SettingsUI");
                settingsGo.transform.SetParent(transform, false);
                _settingsUI = settingsGo.AddComponent<SettingsUIFactory>().Initialize(signaling, xrCamera);
            }
            _settingsUI.Toggle();
        }

        private bool IsCurrent(RTCPeerConnection peer, string id) =>
            peer == _peer && id == _negotiationId && signaling.IsCurrentNegotiation(id);

        private void ResetPeer()
        {
            _negotiationId = null;
            if (_offerRoutine != null) { StopCoroutine(_offerRoutine); _offerRoutine = null; }
            _pendingIce.Clear();
            _remoteReady = _handlingOffer = _peerConnected = _hasFrame = false;
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
            // Push the incoming video dimensions to the input mapper so touch coords scale correctly.
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
            if (signaling != null)
            {
                signaling.MessageReceived -= OnSignalMessage;
                signaling.NegotiationInvalidated -= ResetPeer;
            }
            if (_videoRender != null) StopCoroutine(_videoRender);
            ResetPeer();
            if (_webRtcUpdate != null) StopCoroutine(_webRtcUpdate);
        }
    }
}
