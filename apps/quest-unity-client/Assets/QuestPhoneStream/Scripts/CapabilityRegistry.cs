using System;
using System.Collections.Generic;
using System.Linq;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class SpatialCapabilityState
    {
        public bool available;
        public bool authorized;
        public bool active;
    }

    [Serializable]
    public sealed class SpatialCapabilityLimit
    {
        public string name;
        public string value;
    }

    [Serializable]
    public sealed class SpatialCapabilityDescriptor
    {
        public string name;
        public string version = "1.0";
        public SpatialCapabilityState state;
        public string[] transports = Array.Empty<string>();
        public string[] features = Array.Empty<string>();
        public SpatialCapabilityLimit[] limits = Array.Empty<SpatialCapabilityLimit>();
        public string[] permissions = Array.Empty<string>();
    }

    public sealed class CapabilityRegistry
    {
        private readonly Dictionary<string, SpatialCapabilityDescriptor> _capabilities;
        public event Action<SpatialCapabilityDescriptor[]> Changed;

        private CapabilityRegistry(IEnumerable<SpatialCapabilityDescriptor> capabilities)
        {
            _capabilities = new Dictionary<string, SpatialCapabilityDescriptor>(StringComparer.Ordinal);
            foreach (var capability in capabilities)
            {
                if (capability == null || string.IsNullOrWhiteSpace(capability.name))
                    throw new ArgumentException("Capability name is required");
                if (_capabilities.ContainsKey(capability.name))
                    throw new ArgumentException("Duplicate capability: " + capability.name);
                _capabilities.Add(capability.name, capability);
            }
        }

        public SpatialCapabilityDescriptor[] All() => _capabilities.Values.OrderBy(value => value.name, StringComparer.Ordinal).ToArray();

        public bool UpdateState(string name, bool? available = null, bool? authorized = null, bool? active = null)
        {
            if (!_capabilities.TryGetValue(name, out var capability)) return false;
            var nextAvailable = available ?? capability.state.available;
            var nextAuthorized = authorized ?? capability.state.authorized;
            var nextActive = active ?? capability.state.active;
            if (!nextAvailable || !nextAuthorized) nextActive = false;
            if (nextAvailable == capability.state.available &&
                nextAuthorized == capability.state.authorized &&
                nextActive == capability.state.active) return false;
            capability.state.available = nextAvailable;
            capability.state.authorized = nextAuthorized;
            capability.state.active = nextActive;
            Changed?.Invoke(All());
            return true;
        }

        public static CapabilityRegistry CreateQuestDefaults()
        {
            return new CapabilityRegistry(new[]
            {
                Descriptor("display.consume", true, true, false, new[] { "webrtc.video" }, new[] { "screen.video" }),
                Descriptor("display.control", true, true, false, new[] { "webrtc.datachannel" }, new[] { "pointer", "gesture", "text" }),
                Descriptor("media.consume", true, false, false, new[] { "http.range" }, new[] { "catalog", "range" }, new[] { "qps.media.pairing" }),
                Descriptor("media.render", true, false, false, new[] { "http.range", "local" }, new[] { "flat", "panorama", "stereo-vr" }, new[] { "qps.media.pairing" }),
                Descriptor("xr.head.pose", true, true, false, new[] { "local", "webrtc.datachannel" }, new[] { "openxr.pose", "60hz", "72hz" }),
                Descriptor("xr.controller.pose", true, true, false, new[] { "local", "webrtc.datachannel" }, new[] { "openxr.pose", "left", "right", "60hz", "72hz" }),
                Descriptor("camera.rgb", false, false, false, new[] { "local", "webrtc.track" },
                    new[] { "single-frame", "sampled-preview", "passthrough-rgb" }, new[] { "horizonos.permission.HEADSET_CAMERA" })
            });
        }

        private static SpatialCapabilityDescriptor Descriptor(
            string name,
            bool available,
            bool authorized,
            bool active,
            string[] transports,
            string[] features,
            string[] permissions = null)
        {
            return new SpatialCapabilityDescriptor
            {
                name = name,
                version = "1.0",
                state = new SpatialCapabilityState { available = available, authorized = authorized, active = active },
                transports = transports ?? Array.Empty<string>(),
                features = features ?? Array.Empty<string>(),
                limits = Array.Empty<SpatialCapabilityLimit>(),
                permissions = permissions ?? Array.Empty<string>()
            };
        }
    }
}
