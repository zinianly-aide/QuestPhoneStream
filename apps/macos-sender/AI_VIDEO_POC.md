# LingBot AI Video POC

This branch keeps signaling/WebRTC/Spatial Protocol unchanged. `src/aiVideoSource.ts` only replaces the source of the existing video `MediaStream`.

## Source hook

`renderer.ts` currently does:

```ts
stream = await navigator.mediaDevices.getUserMedia(screenConstraints);
if (activeSession) await createPeer(activeSession);
```

For the AI-video POC the equivalent source is:

```ts
import { createAiVideoSource, type AiVideoSourceHandle } from "./aiVideoSource";

let aiVideo: AiVideoSourceHandle | null = null;

async function startAiVideo(): Promise<void> {
  stopCapture();
  aiVideo?.stop();
  aiVideo = await createAiVideoSource({
    bridgeUrl: "http://127.0.0.1:8765",
    fps: config.fps,
  });
  stream = aiVideo.stream;
  setRuntimeState(true, false);
  uiState("ready", "LingBot AI video source ready · waiting for Quest session");
  if (activeSession) await createPeer(activeSession);
}
```

On stop, call `aiVideo?.stop()` and clear the handle. `createPeer()` is intentionally unchanged: it already calls `stream.getVideoTracks()[0]` and adds that track to the existing `RTCPeerConnection`.

## Q0 manual check

On the LingBot side:

```bash
python scripts/quest_frame_bridge.py
python scripts/quest_stream_replay.py <generated.mp4> --fps 12
```

Then start the AI source and create the normal Quest session.

Acceptance:
- video reaches the existing Quest receiver,
- reconnect still works,
- `aiVideo.stats()` shows increasing `receivedFrames`,
- bridge remains localhost-only,
- no signaling/schema changes.

The next gate is progressive VAE output; do not add new signaling messages just to transport local generated frames.
