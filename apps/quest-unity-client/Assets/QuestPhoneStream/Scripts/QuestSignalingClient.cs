using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class QuestSignalingClient : MonoBehaviour
    {
        public string signalingUrl = "ws://192.168.1.10:8787";
        public string token = "dev-token";
        public string questDeviceId = "quest-3s-001";
        public string androidDeviceId = "android-phone-001";
        public string sessionId = "local-session-001";

        public event Action<SignalMessage> MessageReceived;
        public bool IsOpen => _socket != null && _socket.State == WebSocketState.Open;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;

        private async void Start()
        {
            await ConnectAsync();
            StartCoroutine(HeartbeatLoop());
        }

        private async void OnDestroy()
        {
            _cts?.Cancel();
            if (_socket != null && _socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "destroyed", CancellationToken.None);
            }
            _socket?.Dispose();
        }

        public async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(new Uri(signalingUrl), _cts.Token);
            Debug.Log("[QuestPhoneStream] Signaling connected");
            await SendAsync(new SignalMessage
            {
                type = "register",
                token = token,
                role = "quest",
                deviceId = questDeviceId
            });
            _ = ReceiveLoop();
        }

        public Task SendAnswerAsync(string sdp)
        {
            return SendAsync(new SignalMessage
            {
                type = "answer",
                token = token,
                sessionId = sessionId,
                from = questDeviceId,
                to = androidDeviceId,
                sdp = sdp
            });
        }

        public Task SendIceAsync(IceCandidateDto candidate)
        {
            return SendAsync(new SignalMessage
            {
                type = "ice",
                token = token,
                sessionId = sessionId,
                from = questDeviceId,
                to = androidDeviceId,
                candidate = candidate
            });
        }

        public async Task SendAsync(SignalMessage message)
        {
            if (!IsOpen) return;
            string json = JsonUtility.ToJson(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[64 * 1024];
            while (_socket != null && _socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var message = JsonUtility.FromJson<SignalMessage>(json);
                UnityMainThread.Enqueue(() => MessageReceived?.Invoke(message));
            }
        }

        private IEnumerator HeartbeatLoop()
        {
            while (enabled)
            {
                if (IsOpen)
                {
                    _ = SendAsync(new SignalMessage
                    {
                        type = "heartbeat",
                        token = token,
                        deviceId = questDeviceId,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }
                yield return new WaitForSeconds(15f);
            }
        }
    }
}
