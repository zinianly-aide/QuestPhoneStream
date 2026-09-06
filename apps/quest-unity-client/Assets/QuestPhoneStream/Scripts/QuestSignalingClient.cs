using System;
using System.Collections.Generic;
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
        public event Action<SpatialCapabilityDescriptor[]> CapabilitiesReceived;
        public event Action<SpatialCapabilityDescriptor[]> CapabilitiesChanged;
        public event Action<string, SpatialCapabilityDescriptor[]> PeerCapabilitiesReceived;
        public event Action<string, SpatialCapabilityDescriptor[]> PeerCapabilitiesChanged;
        public event Action NegotiationInvalidated;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly HashSet<string> _spatialPeers = new HashSet<string>(StringComparer.Ordinal);
        private CapabilityRegistry _capabilities;
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
            _capabilities = CapabilityRegistry.CreateQuestDefaults();
            _capabilities.Changed += BroadcastCapabilityChange;
        }

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

                // Spatial v1 is an additive semantic control layer. It never replaces
                // the legacy session/negotiation flow below and does not carry the legacy token.
                await SendSpatialBootstrapAsync(epoch);

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
                            try { HandleJson(json, epoch); }
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

        private void HandleJson(string json, int epoch)
        {
            if (SpatialWire.TryParse(json, out var spatial))
            {
                HandleSpatial(spatial, epoch);
                return;
            }
            HandleMessage(JsonUtility.FromJson<SignalMessage>(json), epoch);
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
                // Old signaling servers reject additive Spatial message types. Preserve the
                // legacy connection and let the normal session request determine readiness.
                if (message.code == "bad_request" && string.IsNullOrEmpty(message.sessionId) &&
                    string.IsNullOrEmpty(message.negotiationId) &&
                    (State == ConnectionState.Registered || State == ConnectionState.SessionRequesting))
                    return;
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

        private void HandleSpatial(SpatialEnvelope message, int epoch)
        {
            if (!IsCurrent(epoch) || message.target != _activeQuest) return;
            var source = message.source;
            var signalingError = message.type == "protocol.error" && source == "signaling";
            if (!signalingError && source != _activeAndroid)
            {
                Debug.LogWarning("[QuestPhoneStream] Ignored Spatial message from non-active peer: " + source);
                return;
            }
            switch (message.type)
            {
                case "device.hello":
                {
                    var selected = !string.IsNullOrEmpty(message.payload.selectedVersion)
                        ? (message.payload.selectedVersion == SpatialWire.Version ? SpatialWire.Version : null)
                        : SpatialWire.NegotiateVersion(message.payload.supportedVersions);
                    if (selected == null)
                    {
                        _ = SendSpatialErrorAsync(source, message.id, "unsupported_version", "No compatible Spatial Protocol version", epoch);
                        return;
                    }
                    _spatialPeers.Add(source);
                    if (string.IsNullOrEmpty(message.payload.selectedVersion))
                        _ = SendSpatialAsync(SpatialWire.Create("device.hello", _activeQuest, source,
                            SpatialWire.HelloPayload(_activeQuest, selected), _activeSession, "", message.id), epoch);
                    return;
                }
                case "device.capabilities.get":
                    _spatialPeers.Add(source);
                    _ = SendSpatialAsync(SpatialWire.Create("device.capabilities.result", _activeQuest, source,
                        SpatialWire.CapabilitiesPayload(_capabilities.All()), _activeSession, "", message.id), epoch);
                    return;
                case "device.capabilities.result":
                {
                    _spatialPeers.Add(source);
                    var capabilities = message.payload.capabilities ?? Array.Empty<SpatialCapabilityDescriptor>();
                    PeerCapabilitiesReceived?.Invoke(source, capabilities);
                    CapabilitiesReceived?.Invoke(capabilities);
                    return;
                }
                case "device.capabilities.changed":
                {
                    _spatialPeers.Add(source);
                    var capabilities = message.payload.capabilities ?? Array.Empty<SpatialCapabilityDescriptor>();
                    PeerCapabilitiesChanged?.Invoke(source, capabilities);
                    CapabilitiesChanged?.Invoke(capabilities);
                    return;
                }
                case "subscription.create":
                case "subscription.cancel":
                    _ = SendSpatialErrorAsync(source, message.id, "not_implemented", "Subscription data plane is not implemented", epoch);
                    return;
                case "subscription.created":
                case "subscription.closed":
                    return;
                case "protocol.error":
                    Debug.LogWarning("[QuestPhoneStream] Spatial peer error: " + (message.payload.code ?? "error"));
                    return;
            }
        }

        private async Task SendSpatialBootstrapAsync(int epoch)
        {
            var hello = SpatialWire.Create("device.hello", _activeQuest, _activeAndroid, SpatialWire.HelloPayload(_activeQuest));
            await SendSpatialAsync(hello, epoch);
            var get = SpatialWire.Create("device.capabilities.get", _activeQuest, _activeAndroid, new SpatialPayload(), "", "", hello.id);
            await SendSpatialAsync(get, epoch);
        }

        private Task SendSpatialErrorAsync(string target, string correlationId, string code, string message, int epoch) =>
            SendSpatialAsync(SpatialWire.Create("protocol.error", _activeQuest, target,
                SpatialWire.ErrorPayload(code, message), _activeSession, "", correlationId), epoch);

        private Task SendSpatialAsync(SpatialEnvelope envelope, int epoch) =>
            SendTextAsync(SpatialWire.Serialize(envelope), epoch);

        private void BroadcastCapabilityChange(SpatialCapabilityDescriptor[] capabilities)
        {
            if (_destroyed || !IsOpen) return;
            var epoch = _epoch;
            foreach (var peer in new List<string>(_spatialPeers))
                _ = SendSpatialAsync(SpatialWire.Create("device.capabilities.changed", _activeQuest, peer,
                    SpatialWire.CapabilitiesPayload(capabilities), _activeSession), epoch);
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

        private Task SendAsync(SignalMessage message, int epoch) =>
            SendTextAsync(SignalingWire.Serialize(message), epoch);

        private async Task SendTextAsync(string json, int epoch)
        {
            if (!IsCurrent(epoch) || !IsOpen) throw new OperationCanceledException();
            var socket = _socket;
            var cancellation = _cts.Token;
            await _sendLock.WaitAsync(cancellation);
            try
            {
                if (!IsCurrent(epoch)) throw new OperationCanceledException();
                var bytes = Encoding.UTF8.GetBytes(json);
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
                _capabilities.UpdateState("display.consume", authorized: true, active: true);
                _mediaReady.TrySetResult(true);
            }
        }

        public bool ReportCapabilityState(string name, bool? authorized = null, bool? active = null) =>
            _capabilities != null && _capabilities.UpdateState(name, authorized, active);

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
            _spatialPeers.Clear();
            _capabilities?.UpdateState("display.consume", active: false);
            _capabilities?.UpdateState("display.control", active: false);
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
            if (_capabilities != null) _capabilities.Changed -= BroadcastCapabilityChange;
            StopTransport();
        }
    }
}
