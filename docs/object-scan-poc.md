# Quest Object Scan POC

Branch: `feat/object-scan-poc`

Goal: prove that Quest 3/3S can produce reconstruction-ready multi-view datasets and move them to a desktop reconstruction worker without changing the existing signaling, control, Spatial Protocol, media, or specialized 3DGS architecture.

## G0 — Reconstruction dataset capture

Implemented:
- reuse `QuestVisionService` / `PassthroughCameraAccess` for RGB frames;
- require camera timestamp, world-space camera pose, and camera intrinsics for every accepted frame;
- save JPEG frames plus a versioned `manifest.json` under `Application.persistentDataPath/ObjectScans/<session>/`;
- reject redundant views using time, translation, and rotation thresholds;
- default target: up to 60 views;
- rewrite the manifest after each accepted frame so an interrupted scan retains usable metadata.

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

Still requires hardware validation on Quest 3S for camera permission, `PassthroughCameraAccess`, `GetCameraPose()`, intrinsics, RGB frames, and actual camera resolution. CI proves package/project compilation only.

## G2 — LAN dataset transfer

Implemented as a separate bulk-data path, not Spatial Protocol:
- `apps/object-scan-worker` is a dependency-free Node.js desktop receiver (default port `8848`);
- Quest `ObjectScanUploader` uses a manually configured worker base URL stored in PlayerPrefs;
- frame uploads are session-scoped raw `PUT`s;
- a `HEAD` probe skips a remote file when its byte size already matches, providing simple resumable retry behavior;
- `manifest.json` is uploaded after JPEGs;
- `POST /finalize` verifies every manifest-referenced frame exists before writing `READY.json`;
- worker accepts only `manifest.json` and `frames/NNNNNN.jpg` to prevent arbitrary path writes.

G2 intentionally does not change NSD, pairing, signaling, or Spatial Protocol. Device discovery for the reconstruction worker can be considered after the POC proves useful.

## G3 — Mac reconstruction worker

Next implementation gate: consume a finalized G2 session and convert Unity camera conventions as needed for the selected reconstruction backend. First support a reproducible offline path that emits both backend-ready metadata and one real reconstruction output. Preserve original Quest poses as priors/evidence rather than discarding them.

Candidate first path:
1. validate dataset and camera intrinsics;
2. export COLMAP-compatible cameras/images text or database inputs;
3. run feature matching / pose refinement where available;
4. produce a 3DGS/PLY or mesh/GLB result;
5. write `result.json` with backend, input session, output files, timings, and warnings.

## G4 — Result return and Quest preview

Return GLB/PLY/splat results to Quest. Reuse existing 3D/3DGS rendering entry points where appropriate; do not rewrite their specialized rendering architecture for this POC.

## G5 — Scan UX and quality guidance

Add `Scan Object`, capture progress, view-coverage guidance, missing-angle hints, transfer status, and optional environment-depth use for masking/scale priors. Depth is auxiliary evidence, not the primary reconstruction source.
