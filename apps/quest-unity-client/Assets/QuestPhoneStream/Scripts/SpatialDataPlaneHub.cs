using System;
using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>Owns the one dedicated Spatial data channel for all high-rate XR publishers.</summary>
    public sealed class SpatialDataPlaneHub : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestWebRtcReceiver receiver;

        private readonly SpatialDataChannelTransport _transport = new SpatialDataChannelTransport();
        public bool IsOpen => _transport.IsOpen;
        public bool HasChannel => _transport.HasChannel;
        public long LastSequence => _transport.LastSequence;
        public int DroppedFrames => _transport.DroppedFrames;
        public int FramesSent => _transport.FramesSent;
        public event Action<bool> OpenStateChanged;

        private void Awake()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            _transport.OpenStateChanged += RelayOpenState;
        }

        private void OnEnable()
        {
            if (signaling != null) signaling.NegotiationInvalidated += ResetTransport;
        }

        private void OnDisable()
        {
            if (signaling != null) signaling.NegotiationInvalidated -= ResetTransport;
        }

        public bool EnsureChannel()
        {
            if (_transport.IsOpen || _transport.HasChannel) return true;
            if (receiver == null) return false;
            var channel = receiver.CreateSpatialDataChannel();
            if (channel == null) return false;
            _transport.Attach(channel);
            return true;
        }

        public bool TrySend(SpatialTelemetryPacket packet) => _transport.TrySend(packet);
        public bool TrySendJson(string json, long sequence) => _transport.TrySendJson(json, sequence);

        public void ResetTransport() => _transport.Reset();

        private void RelayOpenState(bool open) => OpenStateChanged?.Invoke(open);

        private void OnDestroy()
        {
            _transport.OpenStateChanged -= RelayOpenState;
            _transport.Dispose();
        }

        public static SpatialDataPlaneHub GetOrCreate(QuestWebRtcReceiver receiver)
        {
            if (receiver == null) return null;
            var hub = receiver.GetComponent<SpatialDataPlaneHub>() ?? receiver.gameObject.AddComponent<SpatialDataPlaneHub>();
            hub.receiver = receiver;
            hub.signaling = receiver.signaling;
            return hub;
        }
    }
}
