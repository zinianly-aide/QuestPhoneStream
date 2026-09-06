using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class SpatialDeviceDescriptor
    {
        public string id;
        public string name;
        public string[] protocolVersions;
    }

    [Serializable]
    public sealed class SpatialPayload
    {
        public string[] supportedVersions;
        public string selectedVersion;
        public SpatialDeviceDescriptor device;
        public SpatialCapabilityDescriptor[] capabilities;
        public string code;
        public string message;
        public bool retryable;
        public string subscriptionId;
        public string capability;
        public float rateHz;
        public string format;
        public string transport;
        public string reliability;
    }

    [Serializable]
    public sealed class SpatialEnvelope
    {
        public string v;
        public string id;
        public string type;
        public string source;
        public string target;
        public string sessionId;
        public string streamId;
        public string correlationId;
        public long timestamp;
        public SpatialPayload payload;
    }

    [Serializable]
    public sealed class SpatialVector3
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class SpatialQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    [Serializable]
    public sealed class SpatialPose
    {
        public string space;
        public long timestamp;
        public SpatialVector3 position;
        public SpatialQuaternion orientation;
    }

    [Serializable]
    public sealed class SpatialTransform
    {
        public string spaceFrom;
        public string spaceTo;
        public long timestamp;
        public SpatialVector3 translation;
        public SpatialQuaternion rotation;
    }

    public static class SpatialWire
    {
        public const string Version = "1.0";
        private static readonly HashSet<string> MessageTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "device.hello",
            "device.capabilities.get",
            "device.capabilities.result",
            "device.capabilities.changed",
            "subscription.create",
            "subscription.created",
            "subscription.cancel",
            "subscription.closed",
            "protocol.error"
        };

        public static SpatialEnvelope Create(
            string type,
            string source,
            string target,
            SpatialPayload payload = null,
            string sessionId = "",
            string streamId = "",
            string correlationId = "")
        {
            if (!MessageTypes.Contains(type)) throw new ArgumentException("Unsupported Spatial message type");
            return new SpatialEnvelope
            {
                v = Version,
                id = Guid.NewGuid().ToString("N"),
                type = type,
                source = source,
                target = target,
                sessionId = sessionId ?? string.Empty,
                streamId = streamId ?? string.Empty,
                correlationId = correlationId ?? string.Empty,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payload ?? new SpatialPayload()
            };
        }

        public static bool TryParse(string json, out SpatialEnvelope envelope)
        {
            envelope = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { envelope = JsonUtility.FromJson<SpatialEnvelope>(json); }
            catch (Exception) { return false; }
            return envelope != null && envelope.v == Version && MessageTypes.Contains(envelope.type) &&
                   !string.IsNullOrWhiteSpace(envelope.id) && !string.IsNullOrWhiteSpace(envelope.source) &&
                   !string.IsNullOrWhiteSpace(envelope.target) && envelope.payload != null;
        }

        public static string Serialize(SpatialEnvelope envelope) => JsonUtility.ToJson(envelope);

        public static string NegotiateVersion(string[] offered)
        {
            if (offered == null) return null;
            foreach (var version in offered) if (version == Version) return Version;
            return null;
        }

        public static SpatialPayload HelloPayload(string deviceId, string selectedVersion = null)
        {
            return new SpatialPayload
            {
                supportedVersions = selectedVersion == null ? new[] { Version } : null,
                selectedVersion = selectedVersion,
                device = new SpatialDeviceDescriptor { id = deviceId, name = deviceId, protocolVersions = new[] { Version } }
            };
        }

        public static SpatialPayload CapabilitiesPayload(SpatialCapabilityDescriptor[] capabilities) =>
            new SpatialPayload { capabilities = capabilities ?? Array.Empty<SpatialCapabilityDescriptor>() };

        public static SpatialPayload ErrorPayload(string code, string message) =>
            new SpatialPayload { code = code, message = message, retryable = false };
    }
}
