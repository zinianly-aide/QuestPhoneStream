# QuestPhoneStream Agent Guide

## Repository Structure

Three independent apps, one protocol directory:

- `apps/signaling-server` — Node.js/TypeScript WebSocket signaling (pnpm, vitest)
- `apps/android-agent` — Kotlin Android app (Gradle, Android 36, Java 17)
- `apps/quest-unity-client` — Unity client (Unity 2022 LTS or Unity 6)
- `apps/web-viewer` — React/Vite browser client
- `protocol/` — JSON schemas for signaling and control messages

## Commands

**Signaling server** (the only Node.js app with tests):
```bash
cd apps/signaling-server
pnpm install
pnpm dev          # tsx watch, auto-reload
pnpm test         # vitest run
pnpm build        # tsc → dist/
```

**Root-level shortcuts:**
```bash
pnpm dev:signaling    # same as pnpm dev in apps/signaling-server
pnpm test:signaling   # same as pnpm test
pnpm build:signaling  # same as pnpm build
```

**Android agent:**
```bash
cd apps/android-agent
./gradlew assembleDebug
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

**Web viewer:**
```bash
cd apps/web-viewer
npx vite
npx vite build
```

**Health check:**
```bash
scripts/doctor.sh   # verifies adb, java, gradle, Android SDK, Unity
```

## Development

1. Start signaling: `scripts/dev-start.sh` (copies `.env.example` → `.env` if missing, then runs pnpm dev)
2. Default token: `dev-token` (set in `.env` via `SIGNALING_TOKEN`)
3. Default signaling port: `8787`
4. Tests use port 0 (random) to avoid conflicts — safe to run in parallel

## Conventions

- Signaling server uses ESM (`"type": "module"` in package.json)
- TypeScript strict mode, target ES2022, module NodeNext
- Protocol messages defined in `protocol/signaling.schema.json` and `protocol/control-command.schema.json` — update both schema and `src/protocol.ts` when changing message types
- Tests import from `../src/index.js` (note the `.js` extension required by NodeNext resolution)
- Android: namespace `com.questphonestream.agent`, compileSdk 36, minSdk 29
- WebRTC dependency: `org.webrtc:google-webrtc:1.0.32006`
