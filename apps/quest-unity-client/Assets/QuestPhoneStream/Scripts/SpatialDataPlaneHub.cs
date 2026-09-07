using System;
using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>
    /// Owns separate Spatial data planes: realtime is unreliable/unordered;
    /// lifecycle/object state is reliable/ordered. The legacy `spatial` fast
    /// channel remains as a temporary compatibility alias for older consumers.
    /// </summary>
    public sealed class SpatialDataPlaneHub : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestWebRtcReceiver receiver;

        private readonly SpatialDataChannelTransport _fast = new SpatialDataChannelTransport();
        private readonly SpatialDataChannelTransport _fastCompat = new SpatialDataChannelTransport();
        private readonly SpatialDataChannelTransport _reliable = new SpatialDataChannelTransport();

        public bool IsFastOpen => _fast.IsOpen || _fastCompat.IsOpen;
        public bool IsReliableOpen => _reliable.IsOpen;
        public bool IsOpen => IsFastOpen;
        public bool HasChannel => _fast.HasChannel || _fastCompat.HasChannel;
        public bool HasReliableChannel => _reliable.HasChannel;
        public long LastSequence => Math.Max(_fast.LastSequence, _fastCompat.LastSequence);
        public long ReliableLastSequence => _reliable.LastSequence;
        public int DroppedFrames => _fast.DroppedFrames + _fastCompat.DroppedFrames;
        public int ReliableDroppedFrames => _reliable.DroppedFrames;
        public int FramesSent => _fast.FramesSent + _fastCompat.FramesSent;
        public int ReliableFramesSent => _reliable.FramesSent;
        public event Action<bool> OpenStateChanged;
        public event Action<bool> ReliableOpenStateChanged;

        private void Awake()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            _fast.OpenStateChanged += RelayFastOpenState;
            _fastCompat.OpenStateChanged += RelayFastOpenState;
            _reliable.OpenStateChanged += RelayReliableOpenState;
        }

        private void OnEnable()
        {
            if (signaling != null) signaling.NegotiationInvalidated += ResetTransport;
        }

        private void OnDisable()
        {
            if (signaling != null) signaling.NegotiationInvalidated -= ResetTransport;
        }

        public bool EnsureFastChannel()
        {
            if (IsFastOpen || _fast.HasChannel || _fastCompat.HasChannel) return true;
            if (receiver == null) return false;
            try
            {
                var primary = SpatialRtcChannelFactory.CreateFast(receiver);
                if (primary != null) _fast.Attach(primary);
                // Compatibility channel is intentionally also realtime/unreliable.
                var compat = SpatialRtcChannelFactory.CreateFast(receiver, compatibility: true);
                if (compat != null) _fastCompat.Attach(compat);
                return primary != null || compat != null;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[QuestPhoneStream] Spatial fast channel unavailable: " + error.Message);
                return false;
            }
        }

        public bool EnsureReliableChannel()
        {
            if (_reliable.IsOpen || _reliable.HasChannel) return true;
            if (receiver == null) return false;
            try
            {
                var channel = SpatialRtcChannelFactory.CreateReliable(receiver);
                if (channel == null) return false;
                _reliable.Attach(channel);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[QuestPhoneStream] Spatial reliable channel unavailable: " + error.Message);
                return false;
            }
        }

        // P2 compatibility aliases: all existing pose/hand/depth/interaction senders are realtime.
        public bool EnsureChannel() => EnsureFastChannel();
        public bool TrySend(SpatialTelemetryPacket packet) => packet != null && TrySendFastJson(packet.ToJson(), packet.sequence);
        public bool TrySendJson(string json, long sequence) => TrySendFastJson(json, sequence);

        public bool TrySendFastJson(string json, long sequence)
        {
            if (_fast.IsOpen) return _fast.TrySendJson(json, sequence);
            if (_fastCompat.IsOpen) return _fastCompat.TrySendJson(json, sequence);
            // Count the loss on the primary transport without attempting to publish on reliable.
            return _fast.TrySendJson(json, sequence);
        }

        public bool TrySendReliableJson(string json, long sequence) => _reliable.TrySendJson(json, sequence);

        public void ResetTransport()
        {
            _fast.Reset();
            _fastCompat.Reset();
            _reliable.Reset();
        }

        private void RelayFastOpenState(bool _) => OpenStateChanged?.Invoke(IsFastOpen);
        private void RelayReliableOpenState(bool open) => ReliableOpenStateChanged?.Invoke(open);

        private void OnDestroy()
        {
            _fast.OpenStateChanged -= RelayFastOpenState;
            _fastCompat.OpenStateChanged -= RelayFastOpenState;
            _reliable.OpenStateChanged -= RelayReliableOpenState;
            _fast.Dispose();
            _fastCompat.Dispose();
            _reliable.Dispose();
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
