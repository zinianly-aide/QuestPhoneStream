using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace QuestPhoneStream
{
    public sealed class SpatialInteractable : MonoBehaviour
    {
        public static event Action<string> RemoteTargetRemoved;
        public string objectId;
        public bool remoteEnabled = true;
        private bool _removalNotified;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(objectId)) objectId = gameObject.name + "-" + GetInstanceID();
        }

        private void OnEnable() => _removalNotified = false;
        private void OnDisable() => NotifyRemoved();
        private void OnDestroy() => NotifyRemoved();

        private void NotifyRemoved()
        {
            if (_removalNotified || !remoteEnabled || string.IsNullOrWhiteSpace(objectId)) return;
            _removalNotified = true;
            RemoteTargetRemoved?.Invoke(objectId);
        }
    }

    [Serializable]
    public sealed class SpatialInteractionEvent
    {
        public string v = SpatialWire.Version;
        public string capability = "spatial.object.interaction";
        public string streamId;
        public long sequence;
        public long timestamp;
        public string action;
        public string objectId;
        public string hand;
        public bool pressed;
        public SpatialPose pointerPose;
        public SpatialVector3 hitPosition;
        public string ToJson() => JsonUtility.ToJson(this);
    }

    public sealed class SpatialInteractionReset
    {
        public string hand;
        public bool wasPressed;
    }

    public sealed class SpatialInteractionTracker
    {
        private readonly Dictionary<string, string> _targets = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _pressed = new Dictionary<string, bool>(StringComparer.Ordinal);

        public IEnumerable<string> Update(string hand, string targetId, bool pressed)
        {
            _targets.TryGetValue(hand, out var previousTarget);
            _pressed.TryGetValue(hand, out var previousPressed);
            var actions = new List<string>();

            if (!string.Equals(previousTarget, targetId, StringComparison.Ordinal))
            {
                if (previousPressed && !string.IsNullOrEmpty(previousTarget)) actions.Add("select.end");
                if (!string.IsNullOrEmpty(previousTarget)) actions.Add("hover.exit");
                if (!string.IsNullOrEmpty(targetId)) actions.Add("hover.enter");
            }

            if (!previousPressed && pressed && !string.IsNullOrEmpty(targetId)) actions.Add("select.start");
            else if (previousPressed && !pressed && string.Equals(previousTarget, targetId, StringComparison.Ordinal) && !string.IsNullOrEmpty(previousTarget)) actions.Add("select.end");
            else if (pressed && !string.IsNullOrEmpty(targetId) && string.Equals(previousTarget, targetId, StringComparison.Ordinal)) actions.Add("select.update");

            _targets[hand] = targetId;
            _pressed[hand] = pressed && !string.IsNullOrEmpty(targetId);
            return actions;
        }

        public List<SpatialInteractionReset> RemoveTarget(string objectId)
        {
            var resets = new List<SpatialInteractionReset>();
            if (string.IsNullOrWhiteSpace(objectId)) return resets;
            foreach (var hand in new List<string>(_targets.Keys))
            {
                if (!string.Equals(_targets[hand], objectId, StringComparison.Ordinal)) continue;
                resets.Add(new SpatialInteractionReset { hand = hand, wasPressed = _pressed.TryGetValue(hand, out var pressed) && pressed });
                _targets[hand] = null;
                _pressed[hand] = false;
            }
            return resets;
        }

        public string PreviousTarget(string hand) => _targets.TryGetValue(hand, out var value) ? value : null;
        public void Reset() { _targets.Clear(); _pressed.Clear(); }
    }

    public sealed class SpatialObjectInteractionService : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public QuestWebRtcReceiver receiver;
        public SpatialDataPlaneHub dataPlane;
        public float maxDistance = 8f;
        [Range(10f, 60f)] public float updateHz = 30f;

        private readonly SpatialSubscriptionBook _subscriptions = new SpatialSubscriptionBook();
        private readonly SpatialInteractionTracker _tracker = new SpatialInteractionTracker();
        private float _nextTick;
        private long _sequence;

        public int SubscriberCount => _subscriptions.Count;
        public string InteractionState => SubscriberCount == 0 ? "Idle" : "Subscribed";

        private void Start()
        {
            if (signaling == null) signaling = GetComponent<QuestSignalingClient>();
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            if (dataPlane == null) dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            if (signaling == null || receiver == null || dataPlane == null) { enabled = false; return; }
            signaling.SubscriptionCreateRequested += OnSubscriptionCreate;
            signaling.SubscriptionCancelRequested += OnSubscriptionCancel;
            signaling.NegotiationInvalidated += OnNegotiationInvalidated;
            SpatialInteractable.RemoteTargetRemoved += OnRemoteTargetRemoved;
            RefreshCapability();
        }

        private void Update()
        {
            if (_subscriptions.Count == 0 || dataPlane == null || !dataPlane.IsFastOpen || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f / Mathf.Max(10f, updateHz);
            SampleController(XRNode.LeftHand, "left");
            SampleController(XRNode.RightHand, "right");
        }

        private void SampleController(XRNode node, string hand)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
            {
                ResetHandWithoutPose(hand);
                return;
            }

            var direction = rotation * Vector3.forward;
            SpatialInteractable interactable = null;
            Vector3 hitPosition = position + direction * maxDistance;
            if (Physics.Raycast(position, direction, out var hit, maxDistance))
            {
                interactable = hit.collider.GetComponentInParent<SpatialInteractable>();
                hitPosition = hit.point;
                if (interactable != null && !interactable.remoteEnabled) interactable = null;
            }
            var targetId = interactable != null && interactable.remoteEnabled ? interactable.objectId : null;
            var previousTarget = _tracker.PreviousTarget(hand);
            var pressed = device.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerPressed) && triggerPressed;
            foreach (var action in _tracker.Update(hand, targetId, pressed))
            {
                var eventTarget = action == "hover.exit" || (action == "select.end" && string.IsNullOrEmpty(targetId)) ? previousTarget : targetId;
                Broadcast(action, eventTarget, hand, pressed, position, rotation, hitPosition);
            }
        }

        private void ResetHandWithoutPose(string hand)
        {
            var previous = _tracker.PreviousTarget(hand);
            if (string.IsNullOrEmpty(previous)) return;
            foreach (var reset in _tracker.RemoveTarget(previous))
            {
                if (reset.hand != hand) continue;
                if (reset.wasPressed) BroadcastCleanup("select.end", previous, hand);
                BroadcastCleanup("hover.exit", previous, hand);
            }
        }

        private void OnRemoteTargetRemoved(string objectId)
        {
            foreach (var reset in _tracker.RemoveTarget(objectId))
            {
                if (reset.wasPressed) BroadcastCleanup("select.end", objectId, reset.hand);
                BroadcastCleanup("hover.exit", objectId, reset.hand);
            }
        }

        private void BroadcastCleanup(string action, string objectId, string hand)
        {
            if (_subscriptions.Count == 0 || dataPlane == null || !dataPlane.IsFastOpen) return;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var subscription in _subscriptions.Snapshot())
            {
                var packet = new SpatialInteractionEvent
                {
                    streamId = subscription.id,
                    sequence = _sequence++,
                    timestamp = timestamp,
                    action = action,
                    objectId = objectId ?? string.Empty,
                    hand = hand,
                    pressed = false,
                    pointerPose = null,
                    hitPosition = null
                };
                dataPlane.TrySendFastJson(packet.ToJson(), packet.sequence);
            }
        }

        private void Broadcast(string action, string objectId, string hand, bool pressed, Vector3 position, Quaternion rotation, Vector3 hit)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var pose = SpatialCoordinateConverter.ToCanonicalPose(position, rotation, timestamp);
            var canonicalHit = SpatialCoordinateConverter.ToCanonicalPosition(hit);
            foreach (var subscription in _subscriptions.Snapshot())
            {
                var packet = new SpatialInteractionEvent
                {
                    streamId = subscription.id,
                    sequence = _sequence++,
                    timestamp = timestamp,
                    action = action,
                    objectId = objectId,
                    hand = hand,
                    pressed = pressed,
                    pointerPose = pose,
                    hitPosition = canonicalHit
                };
                dataPlane.TrySendFastJson(packet.ToJson(), packet.sequence);
            }
        }

        private void OnSubscriptionCreate(SpatialEnvelope request)
        {
            if (request.payload?.capability != "spatial.object.interaction") return;
            if (!dataPlane.EnsureFastChannel())
            {
                _ = signaling.SendSpatialProtocolErrorAsync(request, "transport_unavailable", "Realtime Spatial DataChannel is unavailable", true);
                return;
            }
            var subscription = new SpatialTelemetrySubscription
            {
                id = Guid.NewGuid().ToString("N"), capability = "spatial.object.interaction",
                rateHz = Mathf.Clamp(request.payload.rateHz <= 0 ? updateHz : request.payload.rateHz, 10f, 60f),
                nextAt = 0f, nextSequence = 0
            };
            if (!_subscriptions.Add(subscription)) return;
            updateHz = subscription.rateHz;
            _ = signaling.SendSubscriptionCreatedAsync(request, subscription.id, subscription.rateHz,
                "qps.spatial.interaction+json", "webrtc.datachannel", "unreliable_unordered");
            RefreshCapability();
        }

        private void OnSubscriptionCancel(SpatialEnvelope request)
        {
            if (request.payload?.capability != "spatial.object.interaction") return;
            if (_subscriptions.Remove(request.payload.subscriptionId, out _))
                _ = signaling.SendSubscriptionClosedAsync(request, request.payload.subscriptionId);
            if (_subscriptions.Count == 0) _tracker.Reset();
            RefreshCapability();
        }

        private void RefreshCapability() => signaling?.ReportCapabilityState("spatial.object.interaction",
            available: true, authorized: true, active: _subscriptions.Count > 0 && dataPlane != null && dataPlane.IsFastOpen);

        private void OnNegotiationInvalidated()
        {
            _subscriptions.Clear();
            _tracker.Reset();
            RefreshCapability();
        }

        private void OnDestroy()
        {
            SpatialInteractable.RemoteTargetRemoved -= OnRemoteTargetRemoved;
            _subscriptions.Clear();
            _tracker.Reset();
            if (signaling != null)
            {
                signaling.SubscriptionCreateRequested -= OnSubscriptionCreate;
                signaling.SubscriptionCancelRequested -= OnSubscriptionCancel;
                signaling.NegotiationInvalidated -= OnNegotiationInvalidated;
                signaling.ReportCapabilityState("spatial.object.interaction", active: false);
            }
        }
    }

    internal static class SpatialObjectInteractionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var service = receiver.GetComponent<SpatialObjectInteractionService>() ?? receiver.gameObject.AddComponent<SpatialObjectInteractionService>();
                service.receiver = receiver;
                service.signaling = receiver.signaling;
                service.dataPlane = SpatialDataPlaneHub.GetOrCreate(receiver);
            }
        }
    }
}
