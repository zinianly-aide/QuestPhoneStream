using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class QuestSignalingClient : MonoBehaviour
    {
        public string signalingUrl = "ws://192.168.1.9:8787";
        public string token = "dev-token";
        public string questDeviceId = "quest-3s-001";
        public string androidDeviceId = "android-phone-001";
        public string sessionId = "local-session-001";
        public int signalingTimeoutMs = 10000;
        public int mediaTimeoutMs = 30000;
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnecting { get; private set; }
        public bool IsOpen => _socket != null && _socket.State == WebSocketState.Open;
        public string NegotiationId { get; private set; }
        public event Action<ConnectionState> StateChanged;
        public event Action<SignalMessage> MessageReceived;
        public event Action NegotiationInvalidated;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private Task _attempt;
        private int _epoch;
        private bool _destroyed;
        private string _activeSession, _activeQuest, _activeAndroid, _activeToken;
        private TaskCompletionSource<bool> _registered, _sessionReady, _mediaReady;

        private void Awake()
        {
            signalingUrl = PlayerPrefs.GetString("QuestPhoneStream_SignalingUrl", signalingUrl);
            token = PlayerPrefs.GetString("QuestPhoneStream_Token", token);
            questDeviceId = PlayerPrefs.GetString("QuestPhoneStream_QuestDeviceId", questDeviceId);
            androidDeviceId = PlayerPrefs.GetString("QuestPhoneStream_AndroidDeviceId", androidDeviceId);
            sessionId = PlayerPrefs.GetString("QuestPhoneStream_SessionId", sessionId);
        }

        // Called by the receiver after all media event subscriptions are installed.
        public Task ReconnectAsync()
        {
            if (_destroyed) return Task.CompletedTask;
            if (IsConnecting) return _attempt ?? Task.CompletedTask;
            _attempt = RunReconnectAsync();
            return _attempt;
        }

        private async Task RunReconnectAsync()
        {
            IsConnecting = true;
            StopTransport();
            var epoch = _epoch;
            NegotiationId = Guid.NewGuid().ToString("N");
            _activeSession = sessionId;
            _activeQuest = questDeviceId;
            _activeAndroid = androidDeviceId;
            _activeToken = token;
            _registered = NewCompletion();
            _sessionReady = NewCompletion();
            _mediaReady = NewCompletion();
            _cts = new CancellationTokenSource();
            var cancellation = _cts.Token;
            var socket = new ClientWebSocket();
            _socket = socket;
            try
            {
                SetState(ConnectionState.WebSocketConnecting);
                if (!Uri.TryCreate(signalingUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "ws" && uri.Scheme != "wss") || !string.IsNullOrEmpty(uri.UserInfo) ||
                    string.IsNullOrWhiteSpace(_activeSession) || string.IsNullOrWhiteSpace(_activeQuest) ||
                    string.IsNullOrWhiteSpace(_activeAndroid) || string.IsNullOrWhiteSpace(_activeToken))
                    throw new ArgumentException("Invalid connection settings");
                var connecting = socket.ConnectAsync(uri, cancellation);
                if (await Task.WhenAny(connecting, Task.Delay(signalingTimeoutMs, cancellation)) != connecting)
                {
                    Fail(ConnectionState.SignalingFailed, epoch);
                    // Observe the aborted connect task, without allowing a stale continuation to change state.
                    try { await connecting; } catch (Exception) { }
                    return;
                }
                await connecting;
                if (!IsCurrent(epoch)) return;
                SetState(ConnectionState.WebSocketConnected);
                SetState(ConnectionState.Registering);
                _ = ReceiveLoop(socket, epoch, cancellation);
                await SendAsync(new SignalMessage { type = "register", token = _activeToken, role = "quest", deviceId = _activeQuest }, epoch);
                if (!await WaitFor(_registered.Task, signalingTimeoutMs, ConnectionState.SignalingFailed, epoch, cancellation)) return;
                SetState(ConnectionState.SessionRequesting);
                await SendAsync(new SignalMessage {
                    type = "create_session", token = _activeToken, sessionId = _activeSession,
                    questDeviceId = _activeQuest, androidDeviceId = _activeAndroid, negotiationId = NegotiationId
                }, epoch);
                _ = HeartbeatLoop(epoch, cancellation);
                if (!await WaitFor(_sessionReady.Task, signalingTimeoutMs, ConnectionState.SessionFailed, epoch, cancellation)) return;
                await WaitFor(_mediaReady.Task, mediaTimeoutMs, ConnectionState.MediaFailed, epoch, cancellation);
            }
            catch (Exception)
            {
                if (IsCurrent(epoch)) Fail(ConnectionState.SignalingFailed, epoch);
            }
            finally
            {
                IsConnecting = false;
                if (!_destroyed) StateChanged?.Invoke(State);
            }
        }

        private static TaskCompletionSource<bool> NewCompletion() =>
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task<bool> WaitFor(Task<bool> task, int timeoutMs, ConnectionState timeoutState, int epoch, CancellationToken cancellation)
        {
            if (await Task.WhenAny(task, Task.Delay(timeoutMs, cancellation)) != task)
            {
                if (IsCurrent(epoch)) Fail(timeoutState, epoch);
                return false;
            }
            return await task && IsCurrent(epoch);
        }

        private bool IsCurrent(int epoch) => !_destroyed && epoch == _epoch;
        public bool IsCurrentNegotiation(string id) =>
            !_destroyed && !string.IsNullOrEmpty(id) && id == NegotiationId && !ConnectionStatus.IsFailure(State);

        private async Task ReceiveLoop(ClientWebSocket socket, int epoch, CancellationToken cancellation)
        {
            var buffer = new byte[8192];
            try
            {
                while (!cancellation.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    using (var message = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                UnityMainThread.Enqueue(() => Fail(ConnectionState.SignalingFailed, epoch));
                                return;
                            }
                            if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > 1024 * 1024)
                                throw new InvalidDataException("Invalid signaling frame");
                            message.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);
                        var json = Encoding.UTF8.GetString(message.ToArray());
                        UnityMainThread.Enqueue(() =>
                        {
                            if (!IsCurrent(epoch)) return;
                            try { HandleMessage(JsonUtility.FromJson<SignalMessage>(json), epoch); }
                            catch (Exception) { Fail(ConnectionState.SignalingFailed, epoch); }
                        });
                    }
                }
            }
            catch (Exception)
            {
                UnityMainThread.Enqueue(() => { if (IsCurrent(epoch)) Fail(ConnectionState.SignalingFailed, epoch); });
            }
        }

        private void HandleMessage(SignalMessage message, int epoch)
        {
            if (message == null || !IsCurrent(epoch)) return;
            if (message.type == "registered")
            {
                if (State != ConnectionState.Registering) return;
                if (message.role != "quest" || message.deviceId != _activeQuest)
                { Fail(ConnectionState.SignalingFailed, epoch); return; }
                SetState(ConnectionState.Registered);
                _registered.TrySetResult(true);
                return;
            }
            if (message.type == "error")
            {
                if (!string.IsNullOrEmpty(message.negotiationId) && message.negotiationId != NegotiationId) return;
                if (!string.IsNullOrEmpty(message.sessionId) && message.sessionId != _activeSession) return;
                Fail(message.code == "unauthorized" ? ConnectionState.AuthFailed :
                    State == ConnectionState.Registering ? ConnectionState.SignalingFailed : ConnectionState.SessionFailed, epoch);
                return;
            }
            if (message.sessionId != _activeSession || message.negotiationId != NegotiationId) return;
            if (message.type == "peer_unavailable")
            { Fail(ConnectionState.DeviceOffline, epoch); return; }
            if (message.type == "session_created")
            {
                if (State != ConnectionState.SessionRequesting) return;
                if (message.androidDeviceId != _activeAndroid || message.questDeviceId != _activeQuest)
                { Fail(ConnectionState.SessionFailed, epoch); return; }
                SetState(ConnectionState.Negotiating);
                MessageReceived?.Invoke(message);
                _sessionReady.TrySetResult(true);
                return;
            }
            if ((message.type == "offer" || message.type == "ice") &&
                message.from == _activeAndroid && message.to == _activeQuest &&
                (State == ConnectionState.Negotiating || State == ConnectionState.PeerConnected || State == ConnectionState.MediaConnected))
                MessageReceived?.Invoke(message);
        }

        public Task SendAnswerAsync(string sdp, string id) =>
            SendRelay(new SignalMessage { type = "answer", sdp = sdp }, id);
        public Task SendIceAsync(IceCandidateDto candidate, string id) =>
            SendRelay(new SignalMessage { type = "ice", candidate = candidate }, id);

        private async Task SendRelay(SignalMessage message, string id)
        {
            if (!IsCurrentNegotiation(id)) return;
            var epoch = _epoch;
            message.token = _activeToken;
            message.sessionId = _activeSession;
            message.negotiationId = id;
            message.from = _activeQuest;
            message.to = _activeAndroid;
            try { await SendAsync(message, epoch); }
            catch (Exception) { Fail(ConnectionState.SignalingFailed, epoch); }
        }

        private async Task SendAsync(SignalMessage message, int epoch)
        {
            if (!IsCurrent(epoch) || !IsOpen) throw new OperationCanceledException();
            var socket = _socket;
            var cancellation = _cts.Token;
            await _sendLock.WaitAsync(cancellation);
            try
            {
                if (!IsCurrent(epoch)) throw new OperationCanceledException();
                var bytes = Encoding.UTF8.GetBytes(SignalingWire.Serialize(message));
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation);
            }
            finally { _sendLock.Release(); }
        }

        private async Task HeartbeatLoop(int epoch, CancellationToken cancellation)
        {
            try
            {
                while (IsCurrent(epoch))
                {
                    await Task.Delay(15000, cancellation);
                    await SendAsync(new SignalMessage {
                        type = "heartbeat", token = _activeToken, deviceId = _activeQuest,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    }, epoch);
                }
            }
            catch (Exception) { if (IsCurrent(epoch)) Fail(ConnectionState.SignalingFailed, epoch); }
        }

        public void ReportMediaState(string id, ConnectionState state)
        {
            if (!IsCurrentNegotiation(id)) return;
            if (ConnectionStatus.IsFailure(state)) { Fail(state, _epoch); return; }
            if (state == ConnectionState.PeerConnected && State == ConnectionState.Negotiating)
                SetState(state);
            if (state == ConnectionState.MediaConnected && State == ConnectionState.PeerConnected)
            {
                SetState(state);
                _mediaReady.TrySetResult(true);
            }
        }

        private void SetState(ConnectionState state)
        {
            if (State == state) return;
            State = state;
            Debug.Log("[QuestPhoneStream] " + ConnectionStatus.Text(state));
            StateChanged?.Invoke(state);
        }

        private void Fail(ConnectionState state, int epoch)
        {
            if (!IsCurrent(epoch)) return;
            StopTransport();
            SetState(state);
        }

        private void StopTransport()
        {
            ++_epoch;
            NegotiationId = null;
            _registered?.TrySetResult(false);
            _sessionReady?.TrySetResult(false);
            _mediaReady?.TrySetResult(false);
            _cts?.Cancel();
            _socket?.Abort();
            _socket?.Dispose();
            _socket = null;
            _cts?.Dispose();
            _cts = null;
            NegotiationInvalidated?.Invoke();
        }

        private void OnDestroy()
        {
            _destroyed = true;
            StopTransport();
        }
    }
}
