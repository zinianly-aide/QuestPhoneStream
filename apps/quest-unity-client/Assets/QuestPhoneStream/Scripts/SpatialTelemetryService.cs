using System;
using UnityEngine;
using UnityEngine.XR;

namespace QuestPhoneStream
{
    public sealed class SpatialTelemetryService : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestWebRtcReceiver receiver;
        public Camera xrCamera;
        public SpatialDataPlaneHub dataPlane;
        [Range(60f, 72f)] public float defaultRateHz = 60f;

        private readonly SpatialSubscriptionBook _subscriptions = new SpatialSubscriptionBook();
        private float _nextAvailabilityProbe;

        public float PoseStreamHz { get; private set; }
        public int DroppedFrames => dataPlane != null ? dataPlane.DroppedFrames : 0;
        public long LastSequence => dataPlane != null ? dataPlane.LastSequence : -1;
        public int ActiveSubscriptionCount => _subscriptions.Count;

        private void Start()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            if (xrCamera == null && receiver != null) xrCamera = receiver.xrCamera;
            if (dataPlane == null) dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            if (signaling == null || receiver == null || dataPlane == null)
            {
                Debug.LogWarning("[QuestPhoneStream] SpatialTelemetryService requires signaling, receiver and data plane");
                enabled = false;
                return;
            }
            signaling.SubscriptionCreateRequested += OnSubscriptionCreate;
            signaling.SubscriptionCancelRequested += OnSubscriptionCancel;
            signaling.NegotiationInvalidated += OnNegotiationInvalidated;
            dataPlane.OpenStateChanged += OnTransportOpenChanged;
            RefreshRuntimeCapabilities();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextAvailabilityProbe)
            {
                _nextAvailabilityProbe = Time.unscaledTime + 1f;
                RefreshRuntimeCapabilities();
            }
            if (dataPlane == null || !dataPlane.IsOpen || _subscriptions.Count == 0) return;

            var now = Time.unscaledTime;
            foreach (var subscription in _subscriptions.Snapshot())
            {
                if (now < subscription.nextAt) continue;
                subscription.nextAt = now + 1f / Mathf.Max(1f, subscription.rateHz);
                if (subscription.capability == "xr.head.pose") SendHead(subscription);
                else if (subscription.capability == "xr.controller.pose") SendControllers(subscription);
            }
        }

        private void OnSubscriptionCreate(SpatialEnvelope request)
        {
            var payload = request.payload;
            if (payload == null || (payload.capability != "xr.head.pose" && payload.capability != "xr.controller.pose")) return;
            if (!string.IsNullOrEmpty(payload.transport) && payload.transport != "webrtc.datachannel")
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "unsupported_transport", "XR telemetry requires webrtc.datachannel", false);
                return;
            }
            if (!string.IsNullOrEmpty(payload.format) && payload.format != "json" && payload.format != "qps.spatial.json")
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "unsupported_format", "XR telemetry currently supports JSON data-plane packets", false);
                return;
            }
            if (!dataPlane.EnsureChannel())
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "transport_unavailable", "Spatial DataChannel requires an active WebRTC peer with SCTP negotiated", true);
                return;
            }

            var subscription = new SpatialTelemetrySubscription
            {
                id = Guid.NewGuid().ToString("N"),
                capability = payload.capability,
                rateHz = NormalizeRate(payload.rateHz <= 0 ? defaultRateHz : payload.rateHz),
                nextAt = Time.unscaledTime,
                nextSequence = 0
            };
            if (!_subscriptions.Add(subscription))
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "subscription_conflict", "Subscription id conflict", true);
                return;
            }
            PoseStreamHz = dataPlane.IsOpen ? _subscriptions.HighestRate() : 0f;
            RefreshRuntimeCapabilities();
            _ = signaling.SendSubscriptionCreatedAsync(request, subscription.id, subscription.rateHz,
                "qps.spatial.json", "webrtc.datachannel", "unreliable_unordered");
        }

        private void OnSubscriptionCancel(SpatialEnvelope request)
        {
            var id = request.payload?.subscriptionId;
            if (!_subscriptions.Remove(id, out _)) return;
            _ = signaling.SendSubscriptionClosedAsync(request, id);
            PoseStreamHz = dataPlane != null && dataPlane.IsOpen ? _subscriptions.HighestRate() : 0f;
            RefreshRuntimeCapabilities();
        }

        private void SendHead(SpatialTelemetrySubscription subscription)
        {
            if (xrCamera == null) return;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var pose = SpatialCoordinateConverter.ToCanonicalPose(xrCamera.transform.localPosition, xrCamera.transform.localRotation, timestamp);
            dataPlane.TrySend(new SpatialTelemetryPacket
            {
                capability = subscription.capability,
                streamId = subscription.id,
                sequence = subscription.nextSequence++,
                timestamp = timestamp,
                space = pose.space,
                position = pose.position,
                orientation = pose.orientation
            });
        }

        private void SendControllers(SpatialTelemetrySubscription subscription)
        {
            SendController(subscription, XRNode.LeftHand, "left");
            SendController(subscription, XRNode.RightHand, "right");
        }

        private void SendController(SpatialTelemetrySubscription subscription, XRNode node, string hand)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation)) return;

            device.TryGetFeatureValue(CommonUsages.trigger, out var trigger);
            device.TryGetFeatureValue(CommonUsages.grip, out var grip);
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis);
            var buttons = 0;
            if (GetButton(device, CommonUsages.primaryButton)) buttons |= 1 << 0;
            if (GetButton(device, CommonUsages.secondaryButton)) buttons |= 1 << 1;
            if (GetButton(device, CommonUsages.triggerButton)) buttons |= 1 << 2;
            if (GetButton(device, CommonUsages.gripButton)) buttons |= 1 << 3;
            if (GetButton(device, CommonUsages.primary2DAxisClick)) buttons |= 1 << 4;
            if (GetButton(device, CommonUsages.menuButton)) buttons |= 1 << 5;

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var pose = SpatialCoordinateConverter.ToCanonicalPose(position, rotation, timestamp);
            dataPlane.TrySend(new SpatialTelemetryPacket
            {
                capability = subscription.capability,
                streamId = subscription.id,
                sequence = subscription.nextSequence++,
                timestamp = timestamp,
                space = pose.space,
                position = pose.position,
                orientation = pose.orientation,
                hand = hand,
                trigger = trigger,
                grip = grip,
                thumbstick = new SpatialVector2 { x = axis.x, y = axis.y },
                buttons = buttons
            });
        }

        private static bool GetButton(InputDevice device, InputFeatureUsage<bool> usage) =>
            device.TryGetFeatureValue(usage, out var value) && value;

        private void RefreshRuntimeCapabilities()
        {
            if (signaling == null) return;
            var headAvailable = xrCamera != null && xrCamera.enabled;
            var controllersAvailable = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid ||
                                       InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
            var channelOpen = dataPlane != null && dataPlane.IsOpen;
            signaling.ReportCapabilityState("xr.head.pose", available: headAvailable, authorized: headAvailable,
                active: headAvailable && channelOpen && _subscriptions.ContainsCapability("xr.head.pose"));
            signaling.ReportCapabilityState("xr.controller.pose", available: controllersAvailable, authorized: controllersAvailable,
                active: controllersAvailable && channelOpen && _subscriptions.ContainsCapability("xr.controller.pose"));
        }

        private void OnTransportOpenChanged(bool open)
        {
            if (!open) _subscriptions.Clear();
            PoseStreamHz = open ? _subscriptions.HighestRate() : 0f;
            RefreshRuntimeCapabilities();
        }

        private void OnNegotiationInvalidated()
        {
            _subscriptions.Clear();
            PoseStreamHz = 0f;
            RefreshRuntimeCapabilities();
        }

        public static float NormalizeRate(float requested) => requested > 66f ? 72f : 60f;

        private void OnDestroy()
        {
            if (signaling != null)
            {
                signaling.SubscriptionCreateRequested -= OnSubscriptionCreate;
                signaling.SubscriptionCancelRequested -= OnSubscriptionCancel;
                signaling.NegotiationInvalidated -= OnNegotiationInvalidated;
                signaling.ReportCapabilityState("xr.head.pose", active: false);
                signaling.ReportCapabilityState("xr.controller.pose", active: false);
            }
            if (dataPlane != null) dataPlane.OpenStateChanged -= OnTransportOpenChanged;
        }
    }

    internal static class SpatialTelemetryBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var hub = SpatialDataPlaneHub.GetOrCreate(receiver);
                var telemetry = receiver.GetComponent<SpatialTelemetryService>() ?? receiver.gameObject.AddComponent<SpatialTelemetryService>();
                telemetry.receiver = receiver;
                telemetry.signaling = receiver.signaling;
                telemetry.xrCamera = receiver.xrCamera;
                telemetry.dataPlane = hub;
            }
        }
    }
}
