using System.Collections;
using Unity.WebRTC;
using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class QuestWebRtcReceiver : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public ControlChannel controlChannel;
        public Material targetMaterial;
        public int textureWidth = 1280;
        public int textureHeight = 720;

        private RTCPeerConnection _peer;
        private RenderTexture _renderTexture;
        private Coroutine _webRtcUpdate;

        private void Awake()
        {
            _webRtcUpdate = StartCoroutine(WebRTC.Update());
        }

        private void Start()
        {
            if (signaling == null) signaling = FindFirstObjectByType<QuestSignalingClient>();
            if (controlChannel == null) controlChannel = FindFirstObjectByType<ControlChannel>();
            signaling.MessageReceived += OnSignalMessage;
            CreatePeerConnection();
        }

        private void OnDestroy()
        {
            if (signaling != null) signaling.MessageReceived -= OnSignalMessage;
            _peer?.Close();
            _peer?.Dispose();
            if (_webRtcUpdate != null) StopCoroutine(_webRtcUpdate);
        }

        private void CreatePeerConnection()
        {
            var config = new RTCConfiguration
            {
                iceServers = new[]
                {
                    new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
                }
            };
            _peer = new RTCPeerConnection(ref config);
            _peer.OnIceCandidate = candidate =>
            {
                _ = signaling.SendIceAsync(new IceCandidateDto
                {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex ?? 0
                });
            };
            _peer.OnDataChannel = channel =>
            {
                if (channel.Label == "control" && controlChannel != null)
                {
                    controlChannel.Attach(channel);
                }
            };
            _peer.OnTrack = e =>
            {
                if (e.Track is VideoStreamTrack videoTrack)
                {
                    videoTrack.OnVideoReceived += texture =>
                    {
                        EnsureRenderTexture(texture.width, texture.height);
                        Graphics.Blit(texture, _renderTexture);
                        if (targetMaterial != null) targetMaterial.mainTexture = _renderTexture;
                    };
                }
            };
        }

        private void OnSignalMessage(SignalMessage message)
        {
            switch (message.type)
            {
                case "offer":
                    StartCoroutine(HandleOffer(message.sdp));
                    break;
                case "ice":
                    if (message.candidate != null)
                    {
                        _peer.AddIceCandidate(new RTCIceCandidate(new RTCIceCandidateInit
                        {
                            candidate = message.candidate.candidate,
                            sdpMid = message.candidate.sdpMid,
                            sdpMLineIndex = message.candidate.sdpMLineIndex
                        }));
                    }
                    break;
            }
        }

        private IEnumerator HandleOffer(string sdp)
        {
            var offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
            var remoteOp = _peer.SetRemoteDescription(ref offer);
            yield return remoteOp;
            if (remoteOp.IsError)
            {
                Debug.LogError($"[QuestPhoneStream] SetRemoteDescription failed: {remoteOp.Error.message}");
                yield break;
            }

            var answerOp = _peer.CreateAnswer();
            yield return answerOp;
            if (answerOp.IsError)
            {
                Debug.LogError($"[QuestPhoneStream] CreateAnswer failed: {answerOp.Error.message}");
                yield break;
            }

            var answer = answerOp.Desc;
            var localOp = _peer.SetLocalDescription(ref answer);
            yield return localOp;
            if (localOp.IsError)
            {
                Debug.LogError($"[QuestPhoneStream] SetLocalDescription failed: {localOp.Error.message}");
                yield break;
            }

            _ = signaling.SendAnswerAsync(answer.sdp);
        }

        private void EnsureRenderTexture(int width, int height)
        {
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height) return;
            if (_renderTexture != null) _renderTexture.Release();
            _renderTexture = new RenderTexture(width > 0 ? width : textureWidth, height > 0 ? height : textureHeight, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
        }
    }
}
