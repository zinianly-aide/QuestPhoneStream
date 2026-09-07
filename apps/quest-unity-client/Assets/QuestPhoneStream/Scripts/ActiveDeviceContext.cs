using System;
using System.Collections.Generic;

namespace QuestPhoneStream
{
    /// <summary>
    /// The one selected remote device. Selecting it may warm the display transport,
    /// but never selects a content mode (Screen, Media, or Control).
    /// </summary>
    public sealed class ActiveDeviceContext
    {
        public string DeviceId { get; private set; }
        public string Name { get; private set; }
        public string StreamId { get; private set; }
        public string SignalingUrl { get; private set; }
        public string MediaBaseUrl { get; private set; }
        public DeviceCapabilities Capabilities { get; } = new DeviceCapabilities();
        public bool IsLost { get; private set; }

        public static ActiveDeviceContext FromDiscovered(MediaDeviceInfo device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            var context = new ActiveDeviceContext();
            context.UpdateFromDiscovery(device);
            return context;
        }

        public void UpdateFromDiscovery(MediaDeviceInfo device)
        {
            if (device == null) return;
            DeviceId = device.deviceId ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(device.name) ? DeviceId : device.name;
            StreamId = device.streamId ?? string.Empty;
            SignalingUrl = device.signalingUrl ?? string.Empty;
            MediaBaseUrl = device.BaseUrl ?? string.Empty;
            IsLost = !device.IsReady;
            Capabilities.ApplyNsdBootstrap(device.capabilities);
        }

        public void MarkLost() => IsLost = true;

        public bool MatchesPeer(string peerId) =>
            !string.IsNullOrWhiteSpace(peerId) &&
            (string.Equals(peerId, StreamId, StringComparison.Ordinal) ||
             string.Equals(peerId, DeviceId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Keeps the small NSD bootstrap hint separate from authoritative Spatial
    /// capabilities. Once a Spatial result arrives it entirely takes precedence.
    /// </summary>
    public sealed class DeviceCapabilities
    {
        private readonly Dictionary<string, SpatialCapabilityDescriptor> _values =
            new Dictionary<string, SpatialCapabilityDescriptor>(StringComparer.Ordinal);

        public bool HasSpatialCapabilities { get; private set; }

        public void ApplyNsdBootstrap(string caps)
        {
            if (HasSpatialCapabilities) return;
            _values.Clear();
            foreach (var raw in (caps ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "screen": AddBootstrap("display.publish"); break;
                    case "control": AddBootstrap("display.control"); break;
                    case "media":
                        AddBootstrap("media.list");
                        AddBootstrap("media.open");
                        break;
                }
            }
        }

        public void ApplySpatial(SpatialCapabilityDescriptor[] capabilities)
        {
            _values.Clear();
            foreach (var descriptor in capabilities ?? Array.Empty<SpatialCapabilityDescriptor>())
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.name)) continue;
                _values[descriptor.name] = descriptor;
            }
            HasSpatialCapabilities = true;
        }

        public bool Supports(string name) =>
            !string.IsNullOrWhiteSpace(name) && _values.TryGetValue(name, out var descriptor) &&
            descriptor.state != null && descriptor.state.available;

        public bool IsAuthorized(string name) =>
            _values.TryGetValue(name ?? string.Empty, out var descriptor) && descriptor.state != null && descriptor.state.authorized;

        private void AddBootstrap(string name) => _values[name] = new SpatialCapabilityDescriptor
        {
            name = name,
            state = new SpatialCapabilityState { available = true, authorized = false, active = false }
        };
    }
}
