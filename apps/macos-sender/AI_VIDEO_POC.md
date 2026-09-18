# LingBot AI Video POC

This branch keeps signaling/WebRTC/Spatial Protocol unchanged. The only change is the source of the existing video `MediaStream`.

## Implemented source hook

`src/aiVideoBootstrap.ts` injects a pseudo capture source:

```text
LingBot AI video · localhost bridge
```

When that source is selected, the bootstrap intercepts only the corresponding `getUserMedia()` call and returns the `MediaStream` created by `src/aiVideoSource.ts`:

```text
LingBot localhost JPEG bridge
  -> fetch latest JPEG + sequence/PTS
  -> Canvas
  -> canvas.captureStream(30)
  -> existing renderer.ts stream variable
  -> existing createPeer()
  -> existing RTCPeerConnection
  -> Quest receiver / SpatialPanel
```

Desktop-screen capture continues to use the original Electron `getUserMedia()` path.

The bridge URL is restricted to `localhost` / `127.0.0.1` and defaults to:

```text
http://127.0.0.1:8765
```

No LAN frame server is introduced by this POC.

## Q0 manual check

On the LingBot side:

```bash
python scripts/quest_frame_bridge.py
python scripts/quest_stream_replay.py <generated.mp4> --fps 12
```

On the Mac sender:
1. Start the app normally.
2. Select `LingBot AI video · localhost bridge`.
3. Keep the default bridge URL unless the local port was changed.
4. Click `Start stream`.
5. Create/use the normal Quest session.

Acceptance:
- replay video reaches the existing Quest receiver,
- reconnect still works,
- stop ends the Canvas track and polling loop,
- bridge sequence/PTS increase monotonically,
- bridge remains localhost-only,
- no signaling/schema changes.

## CI

The existing `macOS Sender CI` runs `npm test` and `npm run build` on the draft PR. Q0 is not PASS until a real Quest replay is recorded even if CI is green.

The next model-side gate is progressive VAE output; do not add new signaling messages just to transport locally generated frames.
