# QuestPhoneStream Object Scan Worker

Minimal desktop receiver for the object-scan POC. It uses only Node.js built-ins and is intended to run on the Mac/PC that will later perform reconstruction.

```bash
cd apps/object-scan-worker
npm test
npm start
```

Defaults:
- listen: `0.0.0.0:8848`
- dataset root: `./object-scans`
- max single-file upload: 32 MiB

Optional environment variables:
- `QPS_SCAN_HOST`
- `QPS_SCAN_PORT`
- `QPS_SCAN_ROOT`
- `QPS_SCAN_MAX_UPLOAD_BYTES`

HTTP contract:
- `GET /health`
- `HEAD /v1/scans/:session/files/frames/000000.jpg`
- `PUT /v1/scans/:session/files/frames/000000.jpg`
- `PUT /v1/scans/:session/files/manifest.json`
- `GET /v1/scans/:session/status`
- `POST /v1/scans/:session/finalize`

`finalize` succeeds only when every frame referenced by the manifest is present. The worker accepts only `manifest.json` and six-digit JPEG frame paths, preventing arbitrary path writes.
