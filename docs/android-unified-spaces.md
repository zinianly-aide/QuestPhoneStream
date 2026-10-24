# Unified Android sender + generic 3D Spaces (M1 prototype)

## What is implemented

- `apps/android-agent` remains a native Kotlin launcher and preserves the existing WebRTC MediaProjection foreground service, Accessibility control and media server.
- Optional private Unity 2022 `unityLibrary` powers **3D Spaces / Home Design** in the same Android package, `com.questphonestream.agent`. The Unity launcher intent-filter is removed, so only the Kotlin app appears in the launcher.
- 3D Spaces opens Hefu Home or an imported **arbitrary** room preview. A user explicitly picks a `qps-room-preview-v1` JSON with Android SAF; importer validates format, object IDs, 512-object limit, dimensions and 1 MB maximum. Import stays inside the app's private external files; no remote upload.
- The private Hefu Unity preview shows arbitrary wall/floor/ceiling/furniture/volume boxes and hides the existing house during that preview. It does not change Hefu's saved layout when another scene is open.
- Quest Spaces has an explicit **Share to phone** action: exports the selected SceneWorkspace as approximate, box-based preview in private `SceneWorkspaces/Exports/`; creates a fresh, random 192-bit one-use token and a LAN endpoint shown inside Quest. Offer expires after 3 minutes; phone imports it using **3D Spaces → Receive from Quest (LAN)** by entering `IP:port` and the 32-character key. This is an explicit pull, not automatic background sharing.
- The LAN prototype uses **HTTP WITHOUT ENCRYPTION** despite the one-time bearer key; intended only for trusted private Wi-Fi. Payload excludes private photos, raw camera data and Meta anchor identifiers. An authenticated and encrypted pairing channel plus QR workflow are required before enabling always-on sync or use on untrusted Wi-Fi.

## Private build (Mac only)

1. Export Hefu Unity project: `Unity -batchmode -quit -projectPath ${HEFU_PRIVATE_PROJECT} -buildTarget Android -executeMethod HefuVR.Editor.HefuUnityLibraryExport.Export -logFile /tmp/hefu-unity-export.log`.
2. Set `QPS_UNITY_LIBRARY_DIR=${HEFU_PRIVATE_PROJECT}/Builds/UnityLibraryExport/unityLibrary`.
3. Run `scripts/prepare-private-unity-library.sh` once after export. It keeps the Unity activity non-exported, removes the duplicate launcher filter, resolves the private Android photo-picker `.aar` from a generated local Maven repo, and pins Unity's NDK 23.1.
4. In `apps/android-agent`, use Gradle 8.10.2 + Java 21 to build `assembleDebug`. The result is a **personal debug APK** with Unity model data inside. Do NOT upload this APK to a public GitHub Release or commit the exported Unity library to Git.
5. Without the `QPS_UNITY_LIBRARY_DIR` environment variable, the existing native-only agent still builds; the Spaces entry explains that no Unity module is present.

## Devices and migration constraints

- Unified APK minSdk 29 (Android 10). The ZUK Z2121 Android 7 device cannot run the native QuestPhoneStream sender, even though its standalone HefuVR editor runs. Do not silently lower sender minSdk without service/WebRTC testing.
- Existing HefuVR installed as `com.hefu.homedesign` has a different Android sandbox. Do not clear/uninstall it: export its `hefu-layout-v3.json` first, then migrate only with explicit user action. The unified Unity `Application.persistentDataPath` is now under `com.questphonestream.agent`.
- Unity as a Library renders full screen and shares process memory with the native host. On a current Android phone, verify: foreground WebRTC streaming still runs after launching Unity; returning does not terminate the capture service; Android screenshot permission is still explicitly user-granted; layout persists; Unity can resume after pause.

## M2: true Quest Space Setup → phone (NOT yet implemented)

- Current Quest Unity project has OpenXR + XRI but **does not yet include Meta MR Utility Kit**. Existing `SceneWorkspace` CAD/blank exports are **not** physical-room scans. Never describe proxy boxes as LiDAR/photogrammetry or high-fidelity room meshes.
- Install a supported Meta XR Core SDK + MRUK version in a separate Quest branch, explicitly declare/request `com.oculus.permission.USE_SCENE`, only in response to user choosing Scan. Prefer MRUK Scene data; do not request passthrough-camera access merely for wall planes.
- Meta's system **Space Setup** runs on-headset and requires user permission. It supplies semantic walls/floor/ceiling and furniture volumes when available; scene-mesh quality and completeness vary. Adapter should translate MRUK room/anchor planes/volumes into `qps-room-preview-v1` boxes without private anchor IDs. Higher fidelity GLB/mesh exports need a distinct audited schema and explicit opt-in.
- Promote the currently explicit one-use manual LAN transfer to authenticated **encrypted** pairing (TLS with fingerprint/pinned ephemeral cert or equivalent), QR-based discovery via `_qps-quest._tcp.`, preview/compare/approve and versioned import. Do not treat the one-time HTTP prototype as production private-data protection.

## Regression

- Quest PlayMode tests: 174/174, including one-time LAN token rejection/consumption test and arbitrary room export.
- Native Android/Unity merged debug APK: Gradle `BUILD SUCCESSFUL` and APK Signature v2 verification (~66 MB). Additional native LAN endpoint tests now included. Its real on-device Unity/WebRTC lifecycle, on-device LAN receive, and Meta Space Setup scan have **not** yet been tested on Android 10+ and Quest 3S.

## 2026-10-24: B3.2.1 Android Unity launch resilience

The Android sender previously launched Unity in the same OS process as WebRTC.
On supported Android 10+ devices, the optional private Unity renderer now runs
in `com.questphonestream.agent:spaces`, using an **optional manifest overlay**
activated only when `QPS_UNITY_LIBRARY_DIR` is set. The native-only APK's
manifest remains free of Unity activity references. Internal launches remain
`exported=false`; the same app UID retains access to its own private scene files.

`MainActivity` catches scene-list and Activity launch exceptions, persists a
startup-pending marker, and on return queries Android 11+ process exit
reasons (Java crash / native crash / ANR / low memory). Private runtime code
signals Unity ready through an explicit non-exported `SpacesReadyReceiver`,
so the main process owns the preferences state rather than relying on unsupported
multi-process SharedPreferences caching. Existing screen-share, signaling,
scene JSON, furniture layout and all stored Quest assets are unmodified.

**Verification so far:** optional-integrated Android build + 24 unit tests
pass, Unity activity declared in `:spaces`, native-only Android build and tests
pass, and package signing matches the prior debug build. No handset ADB was
connected, so **the specific original phone crash stack is still unknown**.
The Quest-based temporary package smoke test could not reach Unity because the
Meta SensorLock dialog blocked 2D app launch; the temporary package was removed
without changing the existing QuestPhoneStream Quest package.

For actual crash forensics, enable USB or Wireless Debugging on the Android
phone, retry the 3D Spaces launch, then run:

```bash
scripts/diagnose-android-3d-crash.sh <phone-adb-serial>
```

Do not use Quest's system FPS or launch results as proof of Android phone
stability. Keep private Hefu Unity scenes, screenshots and built APK out of
public Git commits.
