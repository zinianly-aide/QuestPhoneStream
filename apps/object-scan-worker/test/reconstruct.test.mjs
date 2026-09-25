import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { colmapPointsTextToAsciiPly, poseToMatrix, prepareReconstruction, runColmapReconstruction } from "../src/reconstruct.mjs";

async function makeSession(overrides = {}) {
  const root = await mkdtemp(join(tmpdir(), "qps-reconstruct-"));
  const sessionId = "scan-g3-test";
  const sessionPath = join(root, sessionId);
  await mkdir(join(sessionPath, "frames"), { recursive: true });
  const intrinsics = { valid: true, fx: 700, fy: 701, cx: 640, cy: 480, sensorWidth: 1280, sensorHeight: 960 };
  const frames = [0, 1, 2].map(index => ({
    index,
    image: `frames/${String(index).padStart(6, "0")}.jpg`,
    timestampMs: 1000 + index,
    width: 1280,
    height: 960,
    cameraPosition: { x: index * 0.1, y: 1, z: 0 },
    cameraRotation: { x: 0, y: 0, z: 0, w: 1 },
    intrinsics: { ...intrinsics }
  }));
  if (overrides.mutateFrames) overrides.mutateFrames(frames);
  for (const frame of frames) await writeFile(join(sessionPath, ...frame.image.split("/")), Buffer.from([0xff, 0xd8, frame.index, 0xff, 0xd9]));
  const manifest = {
    version: "qps-object-scan-poc-v1",
    sessionId,
    coordinateSystem: "unity-world-y-up-z-forward",
    frameCount: frames.length,
    frames
  };
  await writeFile(join(sessionPath, "manifest.json"), JSON.stringify(manifest));
  await writeFile(join(sessionPath, "READY.json"), JSON.stringify({ ready: true }));
  return { root, sessionId, sessionPath };
}

test("prepares deterministic COLMAP inputs while preserving Quest poses", async () => {
  const fixture = await makeSession();
  try {
    const prepared = await prepareReconstruction({ root: fixture.root, sessionId: fixture.sessionId });
    assert.equal(prepared.frames.length, 3);
    assert.equal(prepared.plan.cameraModel, "PINHOLE");
    assert.match(prepared.plan.cameraParams, /^700\.00000000,701\.00000000,640\.00000000,480\.00000000$/);
    assert.deepEqual(prepared.plan.commands[0].slice(0, 3), ["feature_extractor", "--database_path", prepared.plan.database]);
    const poses = JSON.parse(await readFile(prepared.posePath, "utf8"));
    assert.equal(poses.version, "qps-object-scan-quest-poses-v1");
    assert.equal(poses.frames.length, 3);
    assert.deepEqual(poses.frames[0].cameraToWorldUnity[3], [0, 0, 0, 1]);

    const result = await runColmapReconstruction(prepared, { dryRun: true });
    assert.equal(result.status, "planned");
    assert.equal(result.backend, "colmap");
  } finally { await rm(fixture.root, { recursive: true, force: true }); }
});

test("rejects mixed intrinsics instead of silently using the wrong camera model", async () => {
  const fixture = await makeSession({ mutateFrames: frames => { frames[1].intrinsics.fx = 760; } });
  try {
    await assert.rejects(
      prepareReconstruction({ root: fixture.root, sessionId: fixture.sessionId }),
      /mixed_intrinsics_not_supported_g3/
    );
  } finally { await rm(fixture.root, { recursive: true, force: true }); }
});

test("converts COLMAP sparse points into the ASCII PLY supported by Quest preview", () => {
  const text = [
    "# 3D point list",
    "1 1.25 -2.5 3.75 12 34 56 0.4 1 2",
    "2 0 0 0 300 -10 128 0.2 1 3"
  ].join("\n");
  const ply = colmapPointsTextToAsciiPly(text);
  assert.match(ply, /format ascii 1\.0/);
  assert.match(ply, /element vertex 2/);
  assert.match(ply, /1\.25 -2\.5 3\.75 12 34 56/);
  assert.match(ply, /0 0 0 255 0 128/);
});

test("poseToMatrix preserves translation for identity rotation", () => {
  assert.deepEqual(
    poseToMatrix({ x: 1, y: 2, z: 3 }, { x: 0, y: 0, z: 0, w: 1 }),
    [[1, 0, 0, 1], [0, 1, 0, 2], [0, 0, 1, 3], [0, 0, 0, 1]]
  );
});
