import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { createScanWorker } from "../src/server.mjs";

async function withWorker(run) {
  const root = await mkdtemp(join(tmpdir(), "qps-scan-worker-"));
  const server = createScanWorker({ root });
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  const base = `http://127.0.0.1:${address.port}`;
  try { await run({ root, base }); }
  finally {
    await new Promise(resolve => server.close(resolve));
    await rm(root, { recursive: true, force: true });
  }
}

test("uploads frames idempotently and finalizes a complete manifest", async () => {
  await withWorker(async ({ root, base }) => {
    const session = "scan-test-01";
    const frame = Buffer.from([0xff, 0xd8, 0xff, 0xd9]);
    let response = await fetch(`${base}/v1/scans/${session}/files/frames/000000.jpg`, {
      method: "PUT",
      body: frame
    });
    assert.equal(response.status, 201);

    response = await fetch(`${base}/v1/scans/${session}/files/frames/000000.jpg`, { method: "HEAD" });
    assert.equal(response.status, 200);
    assert.equal(response.headers.get("content-length"), String(frame.length));

    const manifest = {
      version: "qps-object-scan-poc-v1",
      sessionId: session,
      frameCount: 1,
      frames: [{ image: "frames/000000.jpg" }]
    };
    response = await fetch(`${base}/v1/scans/${session}/files/manifest.json`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(manifest)
    });
    assert.equal(response.status, 201);

    response = await fetch(`${base}/v1/scans/${session}/finalize`, { method: "POST" });
    assert.equal(response.status, 200);
    const result = await response.json();
    assert.equal(result.ready, true);
    assert.equal(result.presentFrames, 1);
    assert.deepEqual(result.missingFrames, []);

    const stored = await readFile(join(root, session, "frames", "000000.jpg"));
    assert.deepEqual(stored, frame);
  });
});

test("finalize rejects an incomplete dataset", async () => {
  await withWorker(async ({ base }) => {
    const session = "scan-incomplete";
    const manifest = {
      version: "qps-object-scan-poc-v1",
      sessionId: session,
      frameCount: 1,
      frames: [{ image: "frames/000000.jpg" }]
    };
    await fetch(`${base}/v1/scans/${session}/files/manifest.json`, {
      method: "PUT",
      body: JSON.stringify(manifest)
    });
    const response = await fetch(`${base}/v1/scans/${session}/finalize`, { method: "POST" });
    assert.equal(response.status, 409);
    const result = await response.json();
    assert.match(result.error, /missing_frames/);
  });
});

test("rejects file paths outside the scan dataset contract", async () => {
  await withWorker(async ({ base }) => {
    const response = await fetch(`${base}/v1/scans/scan-safe/files/%2e%2e%2fsecret.txt`, {
      method: "PUT",
      body: "nope"
    });
    assert.equal(response.status, 400);
  });
});

test("serves only the fixed completed reconstruction result and sparse preview", async () => {
  await withWorker(async ({ root, base }) => {
    const session = "scan-result";
    const reconstruction = join(root, session, "reconstruction", "colmap");
    await mkdir(reconstruction, { recursive: true });
    const result = {
      version: "qps-object-scan-result-v1",
      sessionId: session,
      backend: "colmap",
      status: "completed",
      inputFrames: 12,
      outputs: { sparsePreviewPly: "reconstruction/colmap/sparse-preview.ply" }
    };
    const ply = "ply\nformat ascii 1.0\nelement vertex 0\nend_header\n";
    await writeFile(join(reconstruction, "result.json"), JSON.stringify(result));
    await writeFile(join(reconstruction, "sparse-preview.ply"), ply);

    let response = await fetch(`${base}/v1/scans/${session}/result`);
    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), result);

    response = await fetch(`${base}/v1/scans/${session}/result/sparse-preview.ply`);
    assert.equal(response.status, 200);
    assert.equal(await response.text(), ply);

    response = await fetch(`${base}/v1/scans/${session}/result/%2e%2e%2fquest-poses.json`);
    assert.equal(response.status, 404);
  });
});

test("does not expose preview while reconstruction is blocked", async () => {
  await withWorker(async ({ root, base }) => {
    const session = "scan-blocked";
    const reconstruction = join(root, session, "reconstruction", "colmap");
    await mkdir(reconstruction, { recursive: true });
    await writeFile(join(reconstruction, "result.json"), JSON.stringify({
      version: "qps-object-scan-result-v1",
      sessionId: session,
      backend: "colmap",
      status: "blocked",
      warnings: ["colmap_not_found"]
    }));
    await writeFile(join(reconstruction, "sparse-preview.ply"), "stale");

    const response = await fetch(`${base}/v1/scans/${session}/result/sparse-preview.ply`);
    assert.equal(response.status, 409);
    const result = await response.json();
    assert.match(result.error, /reconstruction_not_completed/);
  });
});
