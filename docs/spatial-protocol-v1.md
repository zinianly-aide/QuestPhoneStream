# Spatial Protocol v1

## Core model

Spatial Protocol v1 is an additive semantic control layer over the existing QuestPhoneStream connection. Every control message uses one envelope with `v`, `id`, `type`, `source`, `target`, `sessionId`, `streamId`, `correlationId`, `timestamp`, and `payload`. The v1 control types are `device.hello`, `device.capabilities.get`, `device.capabilities.result`, `device.capabilities.changed`, `subscription.create`, `subscription.created`, `subscription.cancel`, `subscription.closed`, and `protocol.error`.

A device is identified independently from its capabilities. Capability descriptors use `name`, `version`, `state.available`, `state.authorized`, `state.active`, `transports`, `features`, `limits`, and `permissions`. Discovery means a capability exists; it does not grant permission. Sensitive future capabilities such as camera, microphone, hand tracking, or room data must remain unavailable to a session until their own permission gate has authorized use.

`_qps-device._tcp.` remains the primary LAN bootstrap and `_qps-media._tcp.` remains the media fallback. TXT `caps=media,screen,control` stays as a coarse compatibility hint; `capv=1` and `spatial=1` indicate that full descriptors can be queried after signaling registration. NSD does not carry the complete capability document.

## Control / data plane

Spatial Protocol is the low-frequency control plane. The minimum upgraded flow is `connect -> device.hello -> device.capabilities.get -> device.capabilities.result`, with `device.capabilities.changed` for runtime state changes. Subscription messages negotiate future streams but do not carry stream samples.

The existing data plane remains unchanged: screen video uses WebRTC video, remote control uses the WebRTC DataChannel, local media uses HTTP Range, and the existing WebSocket continues to provide signaling plus Spatial control envelopes. High-frequency XR pose samples, video, camera frames, and audio frames are not valid signaling JSON payloads. Existing `sessionId` and `negotiationId` behavior is unchanged.

The current signaling token remains a legacy connection bootstrap credential. Spatial envelopes do not carry it and capability authorization is not derived from it; future durable trust must be device/session scoped rather than a global development token.

## Capability vocabulary

Capability names are functional rather than platform classes and are rooted at `display.*`, `media.*`, `xr.*`, `camera.*`, `audio.*`, `spatial.*`, `ai.*`, or `input.*`. `available` means the implementation exists on the device, `authorized` means the relevant local/session permission gate has passed, and `active` means the capability is currently participating in an active operation. These states are independent.

Camera, microphone, hand/room, AI, robot, QUIC, 6DoF-video, and 4DGS implementations are outside v1. Their namespaces may be used by future schemas and extensions, but they are not registered as active capabilities by the current Android or Quest registries.

## Transport mapping

| Capability/data | Current transport |
| --- | --- |
| `display.publish` / `display.consume` video | WebRTC video |
| `display.control` | WebRTC DataChannel |
| `media.list` / `media.open` / `media.publish` / `media.consume` / `media.render` | HTTP + HTTP Range, local render |
| Spatial capability discovery and subscription negotiation | Existing signaling WebSocket |
| `xr.head.pose` / `xr.controller.pose` samples | Local only in v1; no network pose stream implemented |

Subscription descriptors negotiate `rateHz`, `format`, `transport`, and `reliability`. v1 permits data transports such as `webrtc.datachannel`, `webrtc.track`, or `local`; signaling WebSocket is intentionally not a high-rate subscription data transport.

## Coordinate convention

Spatial v1 uses meters and normalized quaternions `(x, y, z, w)`. The canonical frame is right-handed, with +X right, +Y up, and -Z forward. `timestamp` is Unix epoch milliseconds in v1. Every `SpatialPose` must include a `space` (`local`, `stage`, `view`, `unbounded`, or `device`) together with position and orientation; bare XYZ values are invalid. Every transform identifies both `spaceFrom` and `spaceTo`. Platform adapters are responsible for converting native coordinate systems to this convention.

## Current capability mapping

Android registers `display.publish`, `display.control`, `media.list`, `media.open`, and `media.publish`. Screen publication requires MediaProjection authorization; remote control requires the existing Accessibility permission; media capabilities retain the existing pairing gate. Availability is advertised independently from authorization and activity.

Quest registers `display.consume`, `display.control`, `media.consume`, `media.render`, `xr.head.pose`, and `xr.controller.pose`. Display and media capabilities map to the existing WebRTC/DataChannel/HTTP rendering paths. Head and controller pose capabilities are real local OpenXR inputs, but their v1 transport is `local` and their network-active state remains false because pose streaming is not implemented.

## Extension method

Add a new capability by defining a namespaced descriptor, registering it only where an implementation exists, and adding a permission gate before setting `authorized` or `active`. If it needs continuous data, first negotiate a subscription on the Spatial control plane, then bind the negotiated subscription to a dedicated data-plane transport. New optional envelope or payload fields must be ignored by v1 readers; a new message type or incompatible semantic change requires an explicit protocol-version negotiation rather than overloading an existing type.
