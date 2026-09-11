# Quest Object Scan POC

Branch: `feat/object-scan-poc`

Goal: prove that Quest 3/3S can produce reconstruction-ready multi-view datasets without changing the existing signaling, control, Spatial Protocol, media, or specialized 3DGS architecture.

## G0 — Reconstruction dataset capture

Implemented:
- reuse `QuestVisionService` / `PassthroughCameraAccess` for RGB frames;
- require camera timestamp, world-space camera pose, and camera intrinsics for every accepted frame;
- save JPEG frames plus a versioned `manifest.json` under `Application.persistentDataPath/ObjectScans/<session>/`;
- reject redundant views using time, translation, and rotation thresholds;
- default target: up to 60 views;
- rewrite the manifest after each accepted frame so an interrupted scan retains usable metadata.

G0 does not upload data, run reconstruction, add new Spatial Protocol messages, or claim real-device validation.

Acceptance:
- 30–60 useful views can be captured around one static object;
- every frame has non-zero focal lengths/sensor resolution and a world pose;
- nearby duplicate viewpoints are suppressed;
- dataset remains readable after an interrupted scan.

## G1 — Pin the Quest camera runtime

Configuration implemented:
- pin `com.meta.xr.mrutilitykit` to `85.0.0`, matching Meta's Passthrough Camera API sample instead of using a floating/latest dependency;
- retain the existing generated-manifest postprocessor for `horizonos.permission.HEADSET_CAMERA`;
- add required `com.oculus.feature.PASSTHROUGH` through that postprocessor instead of replacing Unity's main Android manifest.

Still requires hardware validation on Quest 3S for camera permission, `PassthroughCameraAccess`, `GetCameraPose()`, intrinsics, RGB frames, and the chosen camera resolution. CI proves package/project compilation only.

## G2 — LAN dataset transfer

Add an object-scan export/upload path to a desktop reconstruction worker. Keep bulk JPEG transfer outside Spatial Protocol v1 initially. Include manifest integrity and resumable/session-scoped transfer.

## G3 — Mac reconstruction worker

Consume the G0 dataset and convert Unity camera conventions as needed for the selected backend. First support a reproducible offline reconstruction path (COLMAP/MASt3R-class pose refinement plus 3DGS or mesh output), preserving the original Quest poses as priors/evidence.

## G4 — Result return and Quest preview

Return GLB/PLY/splat results to Quest. Reuse existing 3D/3DGS rendering entry points where appropriate; do not rewrite their specialized rendering architecture for this POC.

## G5 — Scan UX and quality guidance

Add `Scan Object`, capture progress, view-coverage guidance, missing-angle hints, and optional environment-depth use for masking/scale priors. Depth is auxiliary evidence, not the primary reconstruction source.
