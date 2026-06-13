# Architecture

QuestPhoneStream has three runtime components:

1. Android Phone Agent captures the screen with MediaProjection through WebRTC `ScreenCapturerAndroid`.
2. Signaling Server relays registration, session, SDP, ICE, and heartbeat messages.
3. Quest Unity Client receives the WebRTC track and renders it to a panel.

```text
Android Agent -> WebSocket Signaling -> Quest Client
Android Agent -> WebRTC H.264 RTP -> Quest Client
Quest Client -> WebRTC DataChannel(control) -> Android Agent
```

## Decisions

- WebRTC is peer-to-peer; the signaling server does not proxy media.
- MVP uses LAN and STUN. TURN can be added by extending ICE server config on both clients.
- Android uses `ScreenCapturerAndroid`, which is the supported WebRTC wrapper for MediaProjection capture.
- Control commands are JSON on a DataChannel named `control`.

## Security

- Signaling uses a shared token for MVP.
- Do not expose the signaling server directly to the internet without TLS and stronger auth.
- Accessibility Service must be explicitly enabled by the user.

