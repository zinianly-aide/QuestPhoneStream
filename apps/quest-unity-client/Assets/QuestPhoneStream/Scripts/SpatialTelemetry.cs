using System;
using System.Collections.Generic;
using System.Text;
using Unity.WebRTC;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class SpatialVector2
    {
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class SpatialTelemetryPacket
    {
        public string v = SpatialWire.Version;
        public string capability;
        public string streamId;
        public long sequence;
        public long timestamp;
        public string space = "local";
        public SpatialVector3 position;
        public SpatialQuaternion orientation;
        public string hand;
        public float trigger;
        public float grip;
        public SpatialVector2 thumbstick;
        public int buttons;

        public string ToJson() => JsonUtility.ToJson(this);
        public static SpatialTelemetryPacket FromJson(string json) => JsonUtility.FromJson<SpatialTelemetryPacket>(json);
    }

    public static class SpatialCoordinateConverter
    {
        public static SpatialVector3 ToCanonicalPosition(Vector3 value) => new SpatialVector3
        {
            x = value.x,
            y = value.y,
            z = -value.z
        };

        public static SpatialQuaternion ToCanonicalRotation(Quaternion value)
        {
            var normalized = value.normalized;
            return new SpatialQuaternion
            {
                x = -normalized.x,
                y = -normalized.y,
                z = normalized.z,
                w = normalized.w
            };
        }

        public static SpatialPose ToCanonicalPose(Vector3 position, Quaternion rotation, long timestamp, string space = "local") =>
            new SpatialPose
            {
                space = string.IsNullOrWhiteSpace(space) ? "local" : space,
                timestamp = timestamp,
                position = ToCanonicalPosition(position),
                orientation = ToCanonicalRotation(rotation)
            };
    }

    public sealed class SpatialSequenceGate
    {
        private readonly Dictionary<string, long> _last = new Dictionary<string, long>(StringComparer.Ordinal);

        public bool Accept(string streamId, long sequence)
        {
            if (string.IsNullOrWhiteSpace(streamId) || sequence < 0) return false;
            if (_last.TryGetValue(streamId, out var previous) && sequence <= previous) return false;
            _last[streamId] = sequence;
            return true;
        }

        public void Reset(string streamId = null)
        {
            if (string.IsNullOrWhiteSpace(streamId)) _last.Clear();
            else _last.Remove(streamId);
        }
    }

    public sealed class SpatialTelemetrySubscription
    {
        public string id;
        public string capability;
        public float rateHz;
        public float nextAt;
        public long nextSequence;
    }

    public sealed class SpatialSubscriptionBook
    {
        private readonly Dictionary<string, SpatialTelemetrySubscription> _items =
            new Dictionary<string, SpatialTelemetrySubscription>(StringComparer.Ordinal);

        public int Count => _items.Count;

        public bool Add(SpatialTelemetrySubscription subscription)
        {
            if (subscription == null || string.IsNullOrWhiteSpace(subscription.id) ||
                string.IsNullOrWhiteSpace(subscription.capability) || subscription.rateHz <= 0 || _items.ContainsKey(subscription.id))
                return false;
            _items.Add(subscription.id, subscription);
            return true;
        }

        public bool Remove(string id, out SpatialTelemetrySubscription subscription)
        {
            if (string.IsNullOrWhiteSpace(id) || !_items.TryGetValue(id, out subscription)) return false;
            _items.Remove(id);
            return true;
        }

        public bool ContainsCapability(string capability)
        {
            foreach (var item in _items.Values)
                if (item.capability == capability) return true;
            return false;
        }

        public float HighestRate()
        {
            var result = 0f;
            foreach (var item in _items.Values) result = Mathf.Max(result, item.rateHz);
            return result;
        }

        public List<SpatialTelemetrySubscription> Snapshot() => new List<SpatialTelemetrySubscription>(_items.Values);
        public void Clear() => _items.Clear();
    }

    public sealed class SpatialDataChannelTransport : IDisposable
    {
        private RTCDataChannel _channel;
        public long LastSequence { get; private set; } = -1;
        public int FramesSent { get; private set; }
        public int DroppedFrames { get; private set; }
        public bool HasChannel => _channel != null;
        public bool IsOpen => _channel != null && _channel.ReadyState == RTCDataChannelState.Open;
        public event Action<bool> OpenStateChanged;

        public void Attach(RTCDataChannel channel)
        {
            Reset();
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _channel.OnOpen = () => OpenStateChanged?.Invoke(true);
            _channel.OnClose = () => OpenStateChanged?.Invoke(false);
            _channel.OnError = _ => OpenStateChanged?.Invoke(false);
        }

        public bool TrySend(SpatialTelemetryPacket packet) =>
            packet != null && TrySendJson(packet.ToJson(), packet.sequence);

        public bool TrySendJson(string json, long sequence)
        {
            if (string.IsNullOrEmpty(json) || !IsOpen)
            {
                DroppedFrames++;
                return false;
            }
            try
            {
                _channel.Send(Encoding.UTF8.GetBytes(json));
                LastSequence = sequence;
                FramesSent++;
                return true;
            }
            catch (Exception)
            {
                DroppedFrames++;
                return false;
            }
        }

        public void Reset()
        {
            if (_channel != null)
            {
                _channel.OnOpen = null;
                _channel.OnClose = null;
                _channel.OnError = null;
                try { _channel.Close(); } catch (Exception) { }
                _channel.Dispose();
                _channel = null;
            }
            LastSequence = -1;
            FramesSent = 0;
            DroppedFrames = 0;
            OpenStateChanged?.Invoke(false);
        }

        public void Dispose() => Reset();
    }
}
