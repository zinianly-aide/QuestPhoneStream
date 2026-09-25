# QuestPhoneStream Object Scan Worker

Minimal desktop receiver/reconstruction worker for the object-scan POC. It uses only Node.js built-ins for transfer/orchestration; COLMAP is optional and invoked only for real reconstruction.

```bash
cd apps/object-scan-worker
npm test
npm start
```

Defaults:
- listen: `0.0.0.0:8848`
- dataset root: `./object-scans`
- max single-file upload: 32 MiB
- max served preview result: 32 MiB

Optional environment variables:
- `QPS_SCAN_HOST`
- `QPS_SCAN_PORT`
- `QPS_SCAN_ROOT`
- `QPS_SCAN_MAX_UPLOAD_BYTES`
- `QPS_SCAN_MAX_RESULT_BYTES`

HTTP transfer contract:
- `GET /health`
- `HEAD /v1/scans/:session/files/frames/000000.jpg`
- `PUT /v1/scans/:session/files/frames/000000.jpg`
- `PUT /v1/scans/:session/files/manifest.json`
- `GET /v1/scans/:session/status`
- `POST /v1/scans/:session/finalize`

`finalize` succeeds only when every frame referenced by the manifest is present. The worker accepts only `manifest.json` and six-digit JPEG frame paths, preventing arbitrary path writes.

## Reconstruction

After a session is finalized:

```bash
node src/reconstruct.mjs --session <session-id>
```

Use `--dry-run` to validate the dataset and generate `quest-poses.json`, `plan.json`, and a planned `result.json` without invoking COLMAP.

When `colmap` is available on PATH, the worker runs a PINHOLE sparse pipeline:

```text
feature_extractor -> exhaustive_matcher -> mapper -> model_converter(TXT)
```

The original Quest camera poses are retained in `quest-poses.json` as priors/evidence; the initial POC does not force-convert those poses into COLMAP extrinsics. The COLMAP `points3D.txt` output is converted into `sparse-preview.ply`, an ASCII x/y/z+RGB PLY compatible with the existing Quest `GaussianSplatPocRenderer` point-splat preview.

If COLMAP is not installed, reconstruction writes `status: blocked` with `colmap_not_found` instead of reporting a false success.

## Result return

The worker exposes only two fixed reconstruction outputs:
- `GET /v1/scans/:session/result` — versioned `result.json`;
- `GET /v1/scans/:session/result/sparse-preview.ply` — the fixed sparse preview, only when result status is `completed`.

Arbitrary reconstruction paths are never exposed. A stale PLY is not served when the current result is `blocked` or incomplete. The sparse preview is a reconstruction-local POC and is not world-aligned to the original physical object.
