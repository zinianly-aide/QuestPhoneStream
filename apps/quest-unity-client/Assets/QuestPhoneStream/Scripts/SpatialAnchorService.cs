using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class SpatialAnchorRecord
    {
        public string id;
        public string space = "local";
        public bool persistent;
        public long createdAt;
        public long updatedAt;
        public SpatialVector3 position;
        public SpatialQuaternion orientation;
    }

    [Serializable]
    public sealed class SpatialAnchorEvent
    {
        public string v = SpatialWire.Version;
        public string capability = "spatial.anchor";
        public string streamId;
        public long sequence;
        public long timestamp;
        public string action;
        public SpatialAnchorRecord anchor;
        public string anchorId;
        public string ToJson() => JsonUtility.ToJson(this);
    }

    public sealed class SpatialAnchorStore
    {
        private readonly Dictionary<string, SpatialAnchorRecord> _items = new Dictionary<string, SpatialAnchorRecord>(StringComparer.Ordinal);
        public int Count => _items.Count;
        public IEnumerable<SpatialAnchorRecord> All => _items.Values;

        public SpatialAnchorRecord Create(Vector3 position, Quaternion rotation, string id = null)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var pose = SpatialCoordinateConverter.ToCanonicalPose(position, rotation, now);
            var anchor = new SpatialAnchorRecord
            {
                id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id,
                persistent = false,
                createdAt = now,
                updatedAt = now,
                position = pose.position,
                orientation = pose.orientation
            };
            if (_items.ContainsKey(anchor.id)) throw new ArgumentException("Duplicate anchor id");
            _items.Add(anchor.id, anchor);
            return anchor;
        }

        public bool Update(string id, Vector3 position, Quaternion rotation, out SpatialAnchorRecord anchor)
        {
            anchor = default;
            if (string.IsNullOrWhiteSpace(id) || !_items.TryGetValue(id, out anchor)) return false;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var pose = SpatialCoordinateConverter.ToCanonicalPose(position, rotation, now);
            anchor.position = pose.position;
            anchor.orientation = pose.orientation;
            anchor.updatedAt = now;
            return true;
        }

        public bool Remove(string id, out SpatialAnchorRecord anchor) => _items.Remove(id, out anchor);
        public bool TryGet(string id, out SpatialAnchorRecord anchor) => _items.TryGetValue(id, out anchor);
        public void Clear() => _items.Clear();
    }

    public sealed class SpatialAnchorService : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestWebRtcReceiver receiver;
        public SpatialDataPlaneHub dataPlane;

        private readonly SpatialAnchorStore _anchors = new SpatialAnchorStore();
        private readonly SpatialSubscriptionBook _subscriptions = new SpatialSubscriptionBook();
        private long _sequence;

        public int AnchorCount => _anchors.Count;
        public int SubscriberCount => _subscriptions.Count;
        public string StateText => $"{AnchorCount} anchors · {SubscriberCount} subscribers · reliable";

        private void Start()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            if (dataPlane == null) dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            if (signaling == null || receiver == null || dataPlane == null) { enabled = false; return; }
            signaling.SubscriptionCreateRequested += OnSubscriptionCreate;
            signaling.SubscriptionCancelRequested += OnSubscriptionCancel;
            signaling.NegotiationInvalidated += OnNegotiationInvalidated;
            dataPlane.ReliableOpenStateChanged += OnReliableOpenChanged;
            RefreshCapability();
        }

        public SpatialAnchorRecord CreateAnchor(Vector3 localPosition, Quaternion localRotation)
        {
            var anchor = _anchors.Create(localPosition, localRotation);
            Broadcast("created", anchor, anchor.id);
            RefreshCapability();
            return anchor;
        }

        public bool UpdateAnchor(string id, Vector3 localPosition, Quaternion localRotation)
        {
            if (!_anchors.Update(id, localPosition, localRotation, out var anchor)) return false;
            Broadcast("updated", anchor, id);
            return true;
        }

        public bool RemoveAnchor(string id)
        {
            if (!_anchors.Remove(id, out var anchor)) return false;
            Broadcast("removed", anchor, id);
            RefreshCapability();
            return true;
        }

        private void OnSubscriptionCreate(SpatialEnvelope request)
        {
            if (request.payload?.capability != "spatial.anchor") return;
            if (!string.IsNullOrEmpty(request.payload.transport) && request.payload.transport != "webrtc.datachannel")
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "unsupported_transport", "Anchor events require webrtc.datachannel", false);
                return;
            }
            if (!string.IsNullOrEmpty(request.payload.reliability) && request.payload.reliability != "reliable_ordered")
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "unsupported_reliability", "Anchor lifecycle requires reliable_ordered", false);
                return;
            }
            if (!dataPlane.EnsureReliableChannel())
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "transport_unavailable", "Reliable Spatial DataChannel is unavailable", true);
                return;
            }
            var subscription = new SpatialTelemetrySubscription
            {
                id = Guid.NewGuid().ToString("N"), capability = "spatial.anchor", rateHz = 1f,
                nextAt = Time.unscaledTime, nextSequence = 0
            };
            if (!_subscriptions.Add(subscription)) return;
            _ = signaling.SendSubscriptionCreatedAsync(request, subscription.id, 1f,
                "qps.spatial.anchor+json", "webrtc.datachannel", "reliable_ordered");
            if (dataPlane.IsReliableOpen)
                foreach (var anchor in _anchors.All) BroadcastTo(subscription, "snapshot", anchor, anchor.id);
            RefreshCapability();
        }

        private void OnSubscriptionCancel(SpatialEnvelope request)
        {
            if (request.payload?.capability != "spatial.anchor") return;
            if (_subscriptions.Remove(request.payload.subscriptionId, out _))
                _ = signaling.SendSubscriptionClosedAsync(request, request.payload.subscriptionId);
            RefreshCapability();
        }

        private void Broadcast(string action, SpatialAnchorRecord anchor, string anchorId)
        {
            foreach (var subscription in _subscriptions.Snapshot()) BroadcastTo(subscription, action, anchor, anchorId);
        }

        private void BroadcastTo(SpatialTelemetrySubscription subscription, string action, SpatialAnchorRecord anchor, string anchorId)
        {
            if (dataPlane == null || !dataPlane.IsReliableOpen) return;
            var packet = new SpatialAnchorEvent
            {
                streamId = subscription.id,
                sequence = _sequence++,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                action = action,
                anchor = anchor,
                anchorId = anchorId
            };
            dataPlane.TrySendReliableJson(packet.ToJson(), packet.sequence);
        }

        private void OnReliableOpenChanged(bool open)
        {
            if (!open)
            {
                _subscriptions.Clear();
                RefreshCapability();
                return;
            }
            foreach (var subscription in _subscriptions.Snapshot())
                foreach (var anchor in _anchors.All)
                    BroadcastTo(subscription, "snapshot", anchor, anchor.id);
            RefreshCapability();
        }

        private void RefreshCapability() => signaling?.ReportCapabilityState("spatial.anchor",
            available: true, authorized: true,
            active: AnchorCount > 0 || (SubscriberCount > 0 && dataPlane != null && dataPlane.IsReliableOpen));

        private void OnNegotiationInvalidated()
        {
            _subscriptions.Clear();
            _anchors.Clear();
            _sequence = 0;
            RefreshCapability();
        }

        private void OnDestroy()
        {
            _subscriptions.Clear();
            _anchors.Clear();
            if (dataPlane != null) dataPlane.ReliableOpenStateChanged -= OnReliableOpenChanged;
            if (signaling != null)
            {
                signaling.SubscriptionCreateRequested -= OnSubscriptionCreate;
                signaling.SubscriptionCancelRequested -= OnSubscriptionCancel;
                signaling.NegotiationInvalidated -= OnNegotiationInvalidated;
                signaling.ReportCapabilityState("spatial.anchor", active: false);
            }
        }
    }

    internal static class SpatialAnchorBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var service = receiver.GetComponent<SpatialAnchorService>() ?? receiver.gameObject.AddComponent<SpatialAnchorService>();
                service.receiver = receiver;
                service.signaling = receiver.signaling;
                service.dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            }
        }
    }
}
