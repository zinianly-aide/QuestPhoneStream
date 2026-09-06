using System;
using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class SpatialHandTrackingService : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestWebRtcReceiver receiver;
        public SpatialDataPlaneHub dataPlane;
        [Range(60f, 72f)] public float defaultRateHz = 60f;

        private readonly SpatialSubscriptionBook _subscriptions = new SpatialSubscriptionBook();
        private readonly HandTrackingProvider _provider = new HandTrackingProvider();
        private float _nextProbe;

        public string HandTrackingState => _provider.StateText;
        public bool LeftTracked => _provider.LeftTracked;
        public bool RightTracked => _provider.RightTracked;
        public int ActiveSubscriptionCount => _subscriptions.Count;

        private void Start()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            if (dataPlane == null) dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            if (signaling == null || receiver == null || dataPlane == null)
            {
                Debug.LogWarning("[QuestPhoneStream] SpatialHandTrackingService requires signaling, receiver and data plane");
                enabled = false;
                return;
            }
            signaling.SubscriptionCreateRequested += OnSubscriptionCreate;
            signaling.SubscriptionCancelRequested += OnSubscriptionCancel;
            signaling.NegotiationInvalidated += OnNegotiationInvalidated;
            dataPlane.OpenStateChanged += OnTransportOpenChanged;
            RefreshCapabilityState();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextProbe)
            {
                _nextProbe = Time.unscaledTime + 0.5f;
                _provider.Refresh();
                RefreshCapabilityState();
            }
            if (dataPlane == null || !dataPlane.IsOpen || _subscriptions.Count == 0) return;

            var now = Time.unscaledTime;
            foreach (var subscription in _subscriptions.Snapshot())
            {
                if (subscription.capability != "xr.hand.pose" || now < subscription.nextAt) continue;
                subscription.nextAt = now + 1f / Mathf.Max(1f, subscription.rateHz);
                SendHand(subscription, "left");
                SendHand(subscription, "right");
            }
        }

        private void OnSubscriptionCreate(SpatialEnvelope request)
        {
            var payload = request.payload;
            if (payload == null || payload.capability != "xr.hand.pose") return;
            if (!_provider.IsAvailable)
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "capability_unavailable", "OpenXR hand tracking subsystem is not running", true);
                return;
            }
            if (!string.IsNullOrEmpty(payload.transport) && payload.transport != "webrtc.datachannel")
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "unsupported_transport", "Hand telemetry requires webrtc.datachannel", false);
                return;
            }
            if (!string.IsNullOrEmpty(payload.format) && payload.format != "json" && payload.format != "qps.spatial.hand+json")
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "unsupported_format", "Hand telemetry supports qps.spatial.hand+json", false);
                return;
            }
            if (!dataPlane.EnsureChannel())
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "transport_unavailable", "Spatial DataChannel is unavailable", true);
                return;
            }

            var subscription = new SpatialTelemetrySubscription
            {
                id = Guid.NewGuid().ToString("N"),
                capability = "xr.hand.pose",
                rateHz = SpatialTelemetryService.NormalizeRate(payload.rateHz <= 0 ? defaultRateHz : payload.rateHz),
                nextAt = Time.unscaledTime,
                nextSequence = 0
            };
            if (!_subscriptions.Add(subscription))
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "subscription_conflict", "Subscription id conflict", true);
                return;
            }
            RefreshCapabilityState();
            _ = signaling.SendSubscriptionCreatedAsync(request, subscription.id, subscription.rateHz,
                "qps.spatial.hand+json", "webrtc.datachannel", "unreliable_unordered");
        }

        private void OnSubscriptionCancel(SpatialEnvelope request)
        {
            if (request.payload?.capability != "xr.hand.pose") return;
            var id = request.payload.subscriptionId;
            if (!_subscriptions.Remove(id, out _)) return;
            _ = signaling.SendSubscriptionClosedAsync(request, id);
            RefreshCapabilityState();
        }

        private void SendHand(SpatialTelemetrySubscription subscription, string hand)
        {
            var sequence = subscription.nextSequence++;
            if (_provider.TryCapture(hand, subscription.id, sequence, out var frame))
                dataPlane.TrySendJson(frame.ToJson(), frame.sequence);
        }

        private void RefreshCapabilityState()
        {
            if (signaling == null) return;
            var available = _provider.IsAvailable;
            var tracked = _provider.LeftTracked || _provider.RightTracked;
            var transportActive = dataPlane != null && dataPlane.IsOpen;
            signaling.ReportCapabilityState("xr.hand.pose",
                available: available,
                authorized: available,
                active: available && tracked && transportActive && _subscriptions.ContainsCapability("xr.hand.pose"));
        }

        private void OnTransportOpenChanged(bool _) => RefreshCapabilityState();

        private void OnNegotiationInvalidated()
        {
            _subscriptions.Clear();
            RefreshCapabilityState();
        }

        private void OnDestroy()
        {
            if (signaling != null)
            {
                signaling.SubscriptionCreateRequested -= OnSubscriptionCreate;
                signaling.SubscriptionCancelRequested -= OnSubscriptionCancel;
                signaling.NegotiationInvalidated -= OnNegotiationInvalidated;
                signaling.ReportCapabilityState("xr.hand.pose", active: false);
            }
            if (dataPlane != null) dataPlane.OpenStateChanged -= OnTransportOpenChanged;
        }
    }

    internal static class SpatialHandTrackingBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var service = receiver.GetComponent<SpatialHandTrackingService>() ?? receiver.gameObject.AddComponent<SpatialHandTrackingService>();
                service.receiver = receiver;
                service.signaling = receiver.signaling;
                service.dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            }
        }
    }
}
