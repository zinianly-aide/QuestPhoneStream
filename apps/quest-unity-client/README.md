# Quest Unity Client

Open this folder with Unity 6 or Unity 2022 LTS. The scripts are intentionally small so the MVP scene can be assembled quickly.

## Scene Setup

1. Install packages from `Packages/manifest.json`.
2. Enable Android build support and switch platform to Android.
3. Enable OpenXR and the Meta Quest feature group in Project Settings.
4. Create a scene with:
   - `XR Origin`
   - `PhonePanel` quad or plane with a mesh collider
   - `QuestWebRtcReceiver`, `QuestSignalingClient`, `ControlChannel`, `PanelInputMapper`
5. Assign the panel material to `QuestWebRtcReceiver.targetMaterial`.
6. Assign the same renderer/collider to `PanelInputMapper`.

## Runtime Defaults

- Signaling URL: `ws://<host-lan-ip>:8787`
- Token: `dev-token`
- Android device id: `android-phone-001`
- Quest device id: `quest-3s-001`
- Session id: `local-session-001`

