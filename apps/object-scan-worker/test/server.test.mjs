import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
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
