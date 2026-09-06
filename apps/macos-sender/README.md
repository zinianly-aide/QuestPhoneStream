# macOS Sender

Video-only macOS screen publisher for QuestPhoneStream.

- `_qps-device._tcp.` discovery only; no media HTTP server.
- Reuses the existing legacy signaling/session transport. The transport role remains `android` for wire compatibility, while Spatial/NSD metadata identifies the device as `platform=macos`, `sourceType=screen`.
- Publishes `display.publish` through Spatial Capability Discovery.
- Baseline capture target: 1920×1080 at 30fps.

## Run

```bash
npm install
QPS_SIGNALING_URL=ws://<host>:8787 npm start
```

Optional environment variables: `QPS_SIGNALING_TOKEN`, `QPS_DEVICE_ID`, `QPS_QUEST_DEVICE_ID`, `QPS_SESSION_ID`.

macOS will request Screen Recording permission when capture starts. Audio and remote control are intentionally not implemented in P2-1.
