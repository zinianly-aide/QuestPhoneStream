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

## 2026-10-24: actual phone crash root cause and verified fix (B3.2.2)

Device: OnePlus PJX110 / Android 16, ADB USB (phone serial intentionally not committed).
Previous v3.2 unified APK actually crashed twice at 11:15:09 and 11:15:23;
Android `FATAL EXCEPTION: main`, `UnityPlayerActivity.onCreate(40)`,
`UnityPlayer.<init>` → `Resources.getString(0)` →
`Resources$NotFoundException: String resource ID #0x0`.

Root cause: Unity 2022 standalone `launcher/src/main/res/values/strings.xml`
provides `game_view_content_description`; Unity-as-a-Library exports this
resource **only in the launcher**, not in `unityLibrary`. The integrated Android
sender does not use that launcher. The missing string makes UnityPlayer's
Android Surface initialization fail before rendering. This was not caused by
furniture model complexity or device memory.

Fix: the optional Android `src/unityOverlay/res/values/strings.xml` supplies
`game_view_content_description` to the **integrated build only**, via the same
Gradle build-conditional `sourceSets` as `:spaces` activity manifest overlay.
Do not copy the private launcher assets or overwrite `app_name`/app icons.

Validation:
- `:app:testDebugUnitTest :app:assembleDebug`: 24/24 PASS; AAPT verifies that
  `com.questphonestream.agent:string/game_view_content_description` has a
  non-zero resource ID in packaged APK; APK signature verified.
- USB `adb install -r` succeeded, no uninstall/data clear. Prior installed APK
  backed up privately outside this public repo.
- Actual device `am start .MainActivity --ez qps_debug_open_3d true` opens
  internal Unity Activity, which remains foreground. Independent
  `com.questphonestream.agent:spaces` plus main process both alive.
- Unity engine initialized on `Adreno 750 / OpenGL ES 3.2` and emitted
  `[HefuPhone] BIRTHDAY_READY anchors=5 decor=126 preset=birthday-v1` and
  `[QPS3D] Unity Hefu Home initialized`; main Android process received
  `Unity Hefu scene ready; handoff complete`.
- ADB screenshot shows Hefu mobile home's overhead 3D geometry and on-screen
  controls. No repeated resource exception or fatal crash during smoke test.
- Native-only build compatibility also checked separately: conditional Unity
  launcher resources/activities must not appear without `QPS_UNITY_LIBRARY_DIR`.
- Private home layout files, original phone APK and runtime screenshots remain
  local; no CAD, user photos or personal app data committed to GitHub.

## 2026-10-24: v3.2.5 photo-reference furniture

The opt-in integrated Unity phone build (`QPS_UNITY_LIBRARY_DIR` set) now
advertises `versionName=3.2.5`, `versionCode=2`, allowing upgrades from
v3.2.4. Native-only CI builds retain their original `0.1.0`/code 1 version.

Owner-approved public reference furniture refinements (warm-white four-door
fridge, olive-leather three-seat sofa, walnut dual-drawer coffee table) are
built from the local HefuVR Unity project without embedding reference photos.
The original CAD movable identities/colliders, optimized dining furniture,
scene persistence, screen streaming and Birthday layout remain unchanged.

The earlier blanket warning against publishing *personal* debug APKs still
applies to unreviewed builds; this individually audited, owner-authorized
v3.2.5 preview may be published as a clearly labeled **pre-release**. Do not
publish scene saves, raw DWG or private photos. Phone physical QA and WebRTC
regression remain pending until an Android 10+ device is available.

## 2026-10-24: v3.2.6 material fidelity and scene cleanup

The opt-in Unity-enabled Android pre-release now has code 3 / version 3.2.6.
HefuVR Android's obsolete `DECOR_Accent_Color_Swatch` was deleted from the
prepared phone scene (not from CAD); the small preview rug was moved beneath
living-room seating and assigned a woven material. Procedural 384px colour
textures, UVs, refined glossy/metallic values, and bounded soft directional
shadows improve leather, walnut, oak and upholstered furniture. The textures
are synthetic; this is a stylised realtime preview, not photorealistic PBR.
The native-only CI build retains code 1 / version 0.1.0. Physical device
rendering and frame rate still require testing.

## 2026-10-24: v3.2.7 effect-image-derived material preview

Opt-in Unity-enabled Android APK version 3.2.7 (versionCode 4). New mobile
scene assets under the local HefuVR Android Unity project blend isolated colour
samples extracted from an approved AI-generated design render with tileable
procedural high-frequency detail. UV coordinates are world-space projected
onto a **copy** of the CAD living/dining floor mesh at 0.78 m per porcelain
tile; the original imported FBX and collider are not altered. Normal maps and
material roughness/smoothness are tuned for floor, walnut, oak and olive
leather. It is not a full-scene image projection and does not create real
photogrammetry or offline path-traced lightmaps. Current mobile device FPS and
appearance still need on-device comparison against the render reference.

Native-only CI builds retain versionCode 1 / versionName 0.1.0.

## 2026-10-25: v3.2.8 mobile outdoor / glazing atmosphere preview

The opt-in Unity-integrated Android version `3.2.8` (code 5) uses the same
signing identity as v3.2.4–v3.2.7 and preserves the native-only CI version
(`0.1.0`, code 1). The local HefuVR Unity export gains transparent CAD window
materials, an interactive transparent kitchen door on CAD wall 26, 2 transparent
balcony-slider panes, a south-oriented directional sun with bounded shadows,
procedural skybox, five low-poly Blender trees and nine abstract distant
buildings *outside* the southern window plane. These last objects are generic
illustrative placeholders, not the user's actual outside environment.

Reference implementation and non-private source assets live under
`assets/public/hefu-cad/atmosphere/`; detailed plan and staged QA in
`docs/space-studio/hefu-realtime-realism-plan-2026-10-25.md`. Device rendering,
WebRTC and FPS remain pending until an Android device is connected.

## 2026-10-25: v3.2.9 P1 Blender furniture & P2 interior light preview

The opt-in mobile Unity-integrated `v3.2.9` (code 6) adds two **visual-only**
Blender meshes, preserving the `Move_Sofa` and `Move_Coffee_Table` identities,
legacy visual renderer as hidden fallback, and original CAD collision. The
three-dimensional sofa has per-seat cushion silhouettes and piping; the
walnut table has physically modelled two drawer fronts, small bevels, brass
pulls and legs. Two non-shadow-casting warm fill lights (living/dining) and
skybox-based ambient specular reflection supplement the existing single
south-directed shadowed daylight source. No global illumination or reflection
probe baking has been implemented; these remain P2 future work.

`HefuP1P2RegressionSuite.Validate` ran 6 local Unity editor QA routines PASS,
covering alignment/mesh/lighting plus original interior, CAD orientation,
glass, material, and nine photo viewpoints. Native Kotlin 24-unit Gradle tests
and full Android APK build must run for each new release. Phone live visuals,
framerate, layout persistence and WebRTC still need physical device QA.

Blender source, FBX and Unity integration code:
`assets/public/hefu-cad/interior-p1p2/`.

## 2026-10-25: v3.3.0 sofa seating and south window visibility

This opt-in Unity-integrated Android build uses versionName `3.3.0`, code `7`
(native-only remains `0.1.0` / code 1). It keeps `Move_Sofa` and its collider
intact but visually rotates only the Blender sofa child so the three seats
face west toward the TV rather than showing a blank back panel.

For the 2 south-facing CAD window groups, 4 duplicate outer tint renderers are
now disabled while all 6 original meshes/material slots remain in the scene;
each center pane uses a single Fade glass sheet, alpha 0.075. Other 12 CAD
windows plus kitchen/slider glass keep transparency; both door interactions
are unchanged. Outside Blender tree import scale and axis conversion are
preserved instead of overwritten, placing correctly upright model trees beyond
the south glass without colliders. Generated window scenery remains entirely
illustrative, not the real surrounding neighborhood.

The six-gate Unity regression suite now also explicitly asserts the sofa's
front orientation and correctly scaled window trees. Metal-powered Unity
Editor rendered the three test viewpoints plus sofa before/after comparison,
see `docs/space-studio/qa/v3.3.0/`. These Editor renderings alone cannot
establish similarity to the approved effect reference or mobile GPU performance;
phone live rendering still must be tested.

## 2026-10-25: Android Spaces v3.3.1 — Luoyang autumn sun and environment

Opt-in Unity-enabled build versionName `3.3.1`, versionCode `8` (native-only CI
version remains `0.1.0` / 1). HefuVR now uses a NOAA-style solar position
approximation for Luoyang, China (34.6836N 112.4536E, UTC+8), **design date
2026-10-25 14:30**: altitude ~33.4°, azimuth ~220.9° from north clockwise.
The existing single directional shadow caster enters from SW through south
windows; autumn-tinted equirect skybox, warm ambient fog, five color-varied
Blender foliage trees and one synthetic grass/soil outside ground renderer
replace prior cool, bare placeholder scenery. No custom geolocation, exact
real-world skyline, live weather, runtime time slider or baked GI in this version.

HefuLuoyangAutumnQA and all six prior Unity editor regression gates pass.
Actual Metal-powered Unity camera comparison at
`docs/space-studio/qa/v3.3.1/`; hardware Android FPS and WebRTC state remain
unverified without ADB device connection. Documentation and source assets:
`assets/public/hefu-cad/atmosphere/`.

## 2026-10-25: v3.3.2 warm interior reference finish

Android Spaces Unity-integrated version `3.3.2`, versionCode `9` (native-only
unchanged). The real-CAD dining table gets two lightweight ivory/brass pendant
models. South living window side margins gain two synthetic-textured linen
side curtains and a light rail; no curtain covers the center glass, no new
colliders, realtime lights or change to window transparency/door controls.
`HefuDecorSwitcher` retains all 3 Style choices and makes the default Neutral
walls a warmer cream to match the user-approved interior moodboard. Sofa,
walnut coffee table, fridge, 17 movable records, fixed room geometry, v3.3.1
Luoyang autumn sky and lighting remain intact.

QA / actual Metal editor images:
`docs/space-studio/qa/v3.3.2/`; public standalone source-pack:
`assets/public/hefu-cad/interior-warm-style/`. Other user-owned Unity files,
private local model/library and real photos remain outside this public source
pack. GPU frame time, GI baking and device screenshot remain pending.

## 2026-10-25: v3.3.3 wall PBR and offline room reflection

Version `3.3.3`, versionCode `10` for Unity-integrated Android. Fixes the
source of flat wall shading: **20 imported CAD wall meshes lacked UVs**. Adds
local wall-face UV projection at metre scale, low-contrast plaster PBR colour
and normal, one combined 107-segment baseboard mesh and 2 offline 64px
baked box-reflection maps (Custom mode; no runtime capture). No changes to
source CAD vertices/physics, kitchen doors, 17 furniture root transforms,
Style switcher or original outdoor Luoyang autumn lighting.

Actual Unity Editor render comparisons and caveats:
`docs/space-studio/qa/v3.3.3/`; public source assets and QA:
`assets/public/hefu-cad/wall-pbr-probes/`. This is not full scene GI/lightmap
baking, and cannot yet claim phone FPS or 80% photo-matching without device QA.

## 2026-10-25: v3.3.4 CAD ceiling + baked GI

Opt-in Unity integrated Android versionName `3.3.4` versionCode `11`.
The dining/living/hall ceiling is generated from **16 original CAD floor
triangles** at 2.677m, with a pooled crown perimeter. The phone camera
shows it in an eye-level interior view and automatically hides it from a
high-angle/top-down floorplan. No furniture changes or new colliders.

A real Unity Metal Progressive GI bake covers 20 static architecture objects,
with 46 sampled room probes and one small lightmap atlas; furniture remains
dynamic and obtains lighting from Light Probes. Higher-padding UV2 generation
and exclusion of two duplicate tiny CAD wall meshes from the GI flags
eliminated the initial 11 and then 2 overlapping-lightmap-UV warnings.

**Important**: Unity 2022.3 macOS native crash if the bake and Android
BuildPipeline run within the same process. Do `Prepare`, `Bake`, `QA`,
`ExportPrepared` in separate editor executions to keep the lightmaps and
scene synchronized. See `assets/public/hefu-cad/ceiling-gi/README.md` for
source and exact restrictions. Real phone frame-time is unverified.

## 2026-10-25: Android Spaces v3.3.5 — Indoor daylight exposure and cabinetry PBR

Opt-in embedded Unity `versionName=3.3.5`, `versionCode=12`. The prior
2 unshadowed point fill lights are brightened to 1.15/.92 with ranges 5.2/5.4m,
and neutral ambient is raised without adding a light or expensive shadows. The
original Luoyang afternoon sun remains at the same orientation; its intensity
changes to 1.04. Real GI is rebaked after Prepare in a separate Unity process:
1 lightmap and 46 probes remain in the shipping mobile scene. Original 17
furniture IDs, camera cutaway, windows, glass and WebRTC native client remain.

A geometry-overlay builder adds ~3mm shallow chamfered front faces on **60
cabinet doors/wardrobe leaves** in six pooled root-attached meshes and repeatable
pale lacquer woodgrain at 48cm physical tiles. Eight dining sideboard walnut
niche/worktop surfaces receive true metre-scale, projected 48cm walnut UVs.
The original cabinet mesh, colliders, layout and original supplier dimensions
are not changed.

Actual Unity GPU fixed-view dining/living luminance rises from YAVG
91.32→127.50 and 89.13→121.64 respectively; see
`docs/space-studio/qa/v3.3.5/`, public source:
`assets/public/hefu-cad/daylight-cabinet-pbr/`. This is an 8-bit image
statistic, not a measured lux or photo-reconstruction score. ADB phone test
remains required.

## 2026-10-25: Android Spaces v3.3.6 — lights visibly originate in fixtures

Integrated Unity versionName `3.3.6` / versionCode `13`. The original 2 ivory
pendants now contain two downward SpotLights and emissive underside panels;
a brass/ivory floor lamp with an emissive fabric shade contains the living
PointLight. Previous 2 detached ambient point fills are deleted during scene
generation. Only the original sun casts realtime shadows; pixelLightCount=4.

Existing Birthday ON/OFF and Day/Night ambience controls swap 2 separately
baked 128px lightmaps and 46 LightProbe SH coefficients per mode, plus emissive
materials and physical light power. A raycast regression prevents the floor
lamp being hidden behind CAD wall 12. Source+assets:
`assets/public/hefu-cad/visible-lights-dual-gi/`. Actual Unity fixed-camera
comparisons: `docs/space-studio/qa/v3.3.6/`. Phone FPS/UI still require
live device QA.

## 2026-10-25: Android Spaces v3.3.7 — brighter practicals, denser baked GI, cached toggles

`versionName=3.3.7`, `versionCode=14`. Same three physical lamps (two actual
dining pendant spots plus one living lampshade point), no new detached fills,
no additional shadow lights. Brighter direct and indirect lighting is baked
into **two separate 256px lightmaps** and **46 probes per mode**. Dynamic
`HefuFixtureLighting` caches unflattened SH and LightmapData only once and
skips same-mode no-op requests; 16 toggle QA PASS. Screenshots and YAVG
comparisons: `docs/space-studio/qa/v3.3.7/`; source and GI assets:
`assets/public/hefu-cad/light-comfort-v337/`. Hardware phone FPS and switch
latency are still unverified until ADB connects.
