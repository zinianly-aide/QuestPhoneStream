using System;
using System.Reflection;
using Unity.WebRTC;

namespace QuestPhoneStream
{
    /// <summary>
    /// Creates Spatial data channels on the receiver's already-negotiated WebRTC peer.
    /// Field metadata is cached once; this is not provider discovery and does not scan assemblies.
    /// </summary>
    internal static class SpatialRtcChannelFactory
    {
        public const string FastLabel = "spatial-fast";
        public const string FastCompatLabel = "spatial";
        public const string ReliableLabel = "spatial-reliable";
        public const string FastProtocol = "qps-spatial-v1-fast";
        public const string ReliableProtocol = "qps-spatial-v1-reliable";

        private static readonly FieldInfo PeerField = typeof(QuestWebRtcReceiver)
            .GetField("_peer", BindingFlags.Instance | BindingFlags.NonPublic);

        public static RTCDataChannel CreateFast(QuestWebRtcReceiver receiver, bool compatibility = false)
        {
            var peer = ResolvePeer(receiver);
            if (peer == null) return null;
            return peer.CreateDataChannel(compatibility ? FastCompatLabel : FastLabel, new RTCDataChannelInit
            {
                ordered = false,
                maxRetransmits = 0,
                protocol = FastProtocol
            });
        }

        public static RTCDataChannel CreateReliable(QuestWebRtcReceiver receiver)
        {
            var peer = ResolvePeer(receiver);
            if (peer == null) return null;
            return peer.CreateDataChannel(ReliableLabel, new RTCDataChannelInit
            {
                ordered = true,
                protocol = ReliableProtocol
            });
        }

        private static RTCPeerConnection ResolvePeer(QuestWebRtcReceiver receiver)
        {
            if (receiver == null || !receiver.IsPeerConnected || PeerField == null) return null;
            try { return PeerField.GetValue(receiver) as RTCPeerConnection; }
            catch { return null; }
        }
    }
}
