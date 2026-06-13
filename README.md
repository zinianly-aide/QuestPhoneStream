# QuestPhoneStream

QuestPhoneStream streams an Android phone screen to Meta Quest 3S through WebRTC and renders it as a floating phone panel in Unity. Control commands are sent back over a WebRTC DataChannel and executed on Android through an Accessibility Service.

## Architecture

```text
Android Phone Agent
  MediaProjection -> ScreenCapturerAndroid -> WebRTC H.264 video track
  DataChannel(control) -> Accessibility gestures

Node.js Signaling Server
  WebSocket registration, session creation, SDP forwarding, ICE forwarding, heartbeat

Quest 3S Unity Client
  Unity WebRTC receiver -> RenderTexture -> floating panel
  XR ray hit UV -> Android coordinates -> DataChannel control JSON
```

## Quick Start

Start signaling:

```bash
cd apps/signaling-server
pnpm install
cp .env.example .env
pnpm dev
```

Build Android agent:

```bash
cd apps/android-agent
./gradlew assembleDebug
adb install -r app/build/outputs/apk/debug/app-debug.apk
adb shell am start -n com.questphonestream.agent/.MainActivity
```

Unity Quest client:

1. Open `apps/quest-unity-client` in Unity 2022 LTS or Unity 6.
2. Install packages from `Packages/manifest.json`.
3. Create a scene with the scripts in `Assets/QuestPhoneStream/Scripts`.
4. Build for Android ARM64 and install to Quest.

## Default Local Values

- Signaling URL: `ws://<host-lan-ip>:8787`
- Token: `dev-token`
- Android device id: `android-phone-001`
- Quest device id: `quest-3s-001`
- Session id: `local-session-001`

## MVP Acceptance

- Quest sees the Android screen in a floating panel.
- Video runs at 720p/1080p and 30fps.
- End-to-end latency target is below 300ms on a local Wi-Fi network.

