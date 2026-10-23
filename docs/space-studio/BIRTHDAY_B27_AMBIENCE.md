# Birthday B2.7 – subtle ambience and validation

## Delivered

- Birthday-v1 decoration and all saved CAD furniture coordinates are unchanged.
- Three new internal balloon-cluster parents animate each set of balloons **together with its own strings**. Sway is bounded to roughly 1.2 cm horizontally, 0.4 cm vertically and about 1.4 degrees rotation. No new physics, colliders, camera, particles or sound.
- The two preexisting decorative point lights gently pulse by 1.5% at low speed. No additional realtime lights and no realtime shadows. The existing LightsOn/ToggleLights setting takes precedence (lights-off stays off).
- A small reusable `BirthdayAmbientMotion` component holds initial local poses, applies transformations without per-frame collections/material allocation, and provides `SetMotionEnabled(false)` / `Restore()` for a static mode. OnDisable restores initial positions and original intensity.
- Mobile HefuVR `ClearDecorations()` turns off motion before removing birthday anchors. Base furniture meshes and scene persistence are untouched.

## Tests and artifacts

- Quest PlayMode: 175/175 PASS, including new motion test for 3 balloon clusters, 2 lights, closed-loop strings, on/off reset, light-off precedence and original furniture transforms.
- HefuVR private editor: `HEFU_B27_AMBIENCE_QA_PASS balloons=3 glows=2 reset=true` plus wall-visibility and three-view screenshot checks. Five anchors and 121 decorative render objects unchanged.
- Quest 3S developer APK was built and signed, transferred to the headset with SHA-256 verification and `pm install -r`, retaining the prior user data. Both private catalog and active ZUK-synced birthday scene passed before/after SHA-256 equality checks.
- Private standalone HefuVR Android 0.3.20 (minSdk 24) built/signed. Private unified QuestPhoneStream Android + Unity library built/signed, 24/24 native tests PASS (minSdk 29). Android test handsets were not connected for real device verification.
- Quest 3S remained asleep and locked (`mWakefulness=Asleep`, Oculus SensorLock), so **real-world headset 72 FPS performance and visual approval have NOT yet been measured**. Do not conflate VrApi logs from Oculus shell with this app's frame rate.

## Privacy and source control

- APKs, actual home CAD asset bundle, room catalog, private photos, and screenshot captures remain on the private Mac/Quest storage, not in Git.
- Public repository includes only reusable geometry/motion code and tests; avoids XR/OpenXR settings and unrelated local edits.
