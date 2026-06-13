# Quest Unity Client

## Unity Setup

1. Open `apps/quest-unity-client`.
2. Install dependencies from `Packages/manifest.json`.
3. Switch build target to Android.
4. Enable OpenXR and Meta Quest support.
5. Create a `PhonePanel` quad with a material and mesh collider.
6. Add `QuestSignalingClient`, `QuestWebRtcReceiver`, `ControlChannel`, `PanelInputMapper`, and `PhonePanelController`.

## WebRTC Setup

Assign:

- `QuestWebRtcReceiver.signaling` -> the object with `QuestSignalingClient`
- `QuestWebRtcReceiver.targetMaterial` -> panel material
- `QuestWebRtcReceiver.controlChannel` -> object with `ControlChannel`
- `ControlChannel.signaling` -> same `QuestSignalingClient`

## Build

Build an Android ARM64 APK named:

```text
apps/quest-unity-client/Builds/QuestPhoneStream.apk
```

Install:

```bash
scripts/adb-install-quest.sh
adb logcat -s QuestPhoneStream Unity WebRTC
```

