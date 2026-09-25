# Quest Object Scan POC

Branch: `feat/object-scan-poc`

Goal: prove that Quest 3/3S can produce reconstruction-ready multi-view datasets, move them to a desktop reconstruction worker, and return a safe preview without changing the existing signaling, control, Spatial Protocol, media, or specialized 3DGS architecture.

## G0 — Reconstruction dataset capture

Implemented:
- reuse `QuestVisionService` / `PassthroughCameraAccess` for RGB frames;
- require camera timestamp, world-space camera pose, and camera intrinsics for every accepted frame;
- save JPEG frames plus a versioned `manifest.json` under `Application.persistentDataPath/ObjectScans/<session>/`;
- reject redundant views using time, translation, and rotation thresholds;
- default target: up to 60 views;
- rewrite the manifest after each accepted frame so an interrupted scan retains usable metadata.

Hardware acceptance remains pending: capture 30–60 useful Quest 3S views around one static object and verify all frames have stable pose/intrinsics.

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

G2 intentionally does not change NSD, pairing, signaling, or Spatial Protocol.

## G3 — Mac reconstruction worker

Implemented:
- finalized datasets are validated before reconstruction;
- G3 currently requires one stable image resolution/intrinsics set and fails explicitly when the camera model changes;
- original Quest camera-to-world poses are retained in `quest-poses.json` as priors/evidence rather than force-converted into uncertain COLMAP extrinsics;
- a deterministic COLMAP PINHOLE plan runs `feature_extractor -> exhaustive_matcher -> mapper -> model_converter(TXT)` when COLMAP is installed;
- missing COLMAP produces `status: blocked` / `colmap_not_found`, never a false PASS;
- COLMAP `points3D.txt` is converted into `sparse-preview.ply`, a deterministic ASCII x/y/z+RGB format supported by the existing Quest POC renderer;
- `result.json` records version, backend, status, inputs, outputs, timings, and warnings.

This is a sparse reconstruction/preview gate, not yet a trained full 3D Gaussian Splat or textured mesh pipeline.

## G4 — Result return and Quest preview

Implemented:
- worker exposes only fixed `GET /v1/scans/:session/result` and `GET /v1/scans/:session/result/sparse-preview.ply` endpoints;
- arbitrary reconstruction files are not exposed;
- preview is served only when the current `result.json` reports `status=completed`, preventing stale PLY reuse after a failed/blocked run;
- Quest `ObjectScanResultClient` checks the version/session identity and feeds the fixed preview URL into the existing `GaussianSplatPocRenderer`;
- no duplicate PLY parser and no Spatial Protocol extension were introduced.

The G4 sparse preview is reconstruction-local and is explicitly **not world-aligned** to the scanned physical object. World registration needs a separately verified Quest/COLMAP coordinate transform and scale strategy.

## G5 — Scan UX and quality guidance

Next gate:
- add a minimal `Scan Object` controller/state model rather than restructuring Home;
- show capture count, coverage, missing-angle hints, upload/result status, and preview state;
- use camera viewpoints around a user-selected target to compute coverage bins;
- keep optional Environment Depth as auxiliary target/mask/scale evidence, not the primary reconstruction source;
- no device discovery/protocol expansion until the POC has real Quest 3S evidence.
