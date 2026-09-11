import { createReadStream, createWriteStream, existsSync } from "node:fs";
import { mkdir, readFile, rename, rm, stat, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const DEFAULT_MAX_UPLOAD_BYTES = 32 * 1024 * 1024;
const SESSION_RE = /^[A-Za-z0-9_-]{1,80}$/;
const FRAME_RE = /^frames\/[0-9]{6}\.jpg$/;

export function createScanWorker(options = {}) {
  const root = resolve(options.root ?? process.env.QPS_SCAN_ROOT ?? join(process.cwd(), "object-scans"));
  const maxUploadBytes = Number(options.maxUploadBytes ?? process.env.QPS_SCAN_MAX_UPLOAD_BYTES ?? DEFAULT_MAX_UPLOAD_BYTES);

  return createServer(async (req, res) => {
    try {
      setCors(res);
      if (req.method === "OPTIONS") return send(res, 204);
      if (req.method === "GET" && req.url === "/health") return json(res, 200, { ok: true, service: "qps-object-scan-worker" });

      const route = parseRoute(req.url ?? "");
      if (!route) return json(res, 404, { error: "not_found" });
      const sessionPath = join(root, route.sessionId);

      if (route.kind === "file") {
        const target = resolveFile(sessionPath, route.relativePath);
        if (!target) return json(res, 400, { error: "invalid_file" });

        if (req.method === "HEAD") {
          if (!existsSync(target)) return send(res, 404);
          const info = await stat(target);
          res.setHeader("Content-Length", String(info.size));
          return send(res, 200);
        }

        if (req.method === "PUT") {
          await mkdir(dirname(target), { recursive: true });
          const existed = existsSync(target);
          await receiveToFile(req, target, maxUploadBytes);
          const info = await stat(target);
          return json(res, existed ? 200 : 201, { ok: true, bytes: info.size, file: route.relativePath });
        }
        return json(res, 405, { error: "method_not_allowed" });
      }

      if (route.kind === "status" && req.method === "GET") {
        const summary = await inspectSession(sessionPath, route.sessionId);
        return json(res, 200, summary);
      }

      if (route.kind === "finalize" && req.method === "POST") {
        const summary = await inspectSession(sessionPath, route.sessionId, true);
        const ready = { ...summary, ready: true, finalizedUtc: new Date().toISOString() };
        await writeFile(join(sessionPath, "READY.json"), JSON.stringify(ready, null, 2));
        return json(res, 200, ready);
      }

      return json(res, 405, { error: "method_not_allowed" });
    } catch (error) {
      const status = Number(error?.statusCode) || 500;
      if (status >= 500) console.error("[object-scan-worker]", error);
      return json(res, status, { error: error?.message ?? "internal_error" });
    }
  });
}

function parseRoute(rawUrl) {
  const pathname = rawUrl.split("?", 1)[0];
  const prefix = "/v1/scans/";
  if (!pathname.startsWith(prefix)) return null;
  const rest = pathname.slice(prefix.length);
  const slash = rest.indexOf("/");
  if (slash < 1) return null;
  const sessionId = safeDecode(rest.slice(0, slash));
  if (!SESSION_RE.test(sessionId)) return null;
  const tail = rest.slice(slash + 1);
  if (tail === "finalize") return { kind: "finalize", sessionId };
  if (tail === "status") return { kind: "status", sessionId };
  if (!tail.startsWith("files/")) return null;
  const relativePath = safeDecode(tail.slice("files/".length));
  return { kind: "file", sessionId, relativePath };
}

function safeDecode(value) {
  try { return decodeURIComponent(value); }
  catch { return ""; }
}

function resolveFile(sessionPath, relativePath) {
  if (relativePath !== "manifest.json" && !FRAME_RE.test(relativePath)) return null;
  return join(sessionPath, ...relativePath.split("/"));
}

async function receiveToFile(req, target, maxUploadBytes) {
  const declared = Number(req.headers["content-length"] ?? 0);
  if (declared > maxUploadBytes) throw httpError(413, "upload_too_large");

  const temp = `${target}.part-${process.pid}-${Date.now()}`;
  let bytes = 0;
  await new Promise((resolvePromise, reject) => {
    const out = createWriteStream(temp, { flags: "wx" });
    const fail = error => { out.destroy(); reject(error); };
    req.on("data", chunk => {
      bytes += chunk.length;
      if (bytes > maxUploadBytes) {
        req.destroy();
        fail(httpError(413, "upload_too_large"));
      }
    });
    req.on("error", fail);
    out.on("error", reject);
    out.on("finish", resolvePromise);
    req.pipe(out);
  }).catch(async error => {
    await rm(temp, { force: true });
    throw error;
  });
  await rename(temp, target);
}

async function inspectSession(sessionPath, sessionId, requireComplete = false) {
  const manifestPath = join(sessionPath, "manifest.json");
  let manifest;
  try { manifest = JSON.parse(await readFile(manifestPath, "utf8")); }
  catch { throw httpError(requireComplete ? 409 : 404, "manifest_missing_or_invalid"); }

  const frames = Array.isArray(manifest.frames) ? manifest.frames : [];
  const missing = [];
  let bytes = 0;
  for (const frame of frames) {
    const relative = typeof frame?.image === "string" ? frame.image : "";
    const target = resolveFile(sessionPath, relative);
    if (!target || !existsSync(target)) { missing.push(relative || "(invalid)"); continue; }
    bytes += (await stat(target)).size;
  }
  if (requireComplete && missing.length) throw httpError(409, `missing_frames:${missing.join(",")}`);
  return {
    sessionId,
    manifestVersion: manifest.version ?? null,
    expectedFrames: Number(manifest.frameCount ?? frames.length),
    presentFrames: frames.length - missing.length,
    missingFrames: missing,
    bytes
  };
}

function httpError(statusCode, message) {
  const error = new Error(message);
  error.statusCode = statusCode;
  return error;
}

function setCors(res) {
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Methods", "GET,HEAD,PUT,POST,OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type,Content-Length");
}

function send(res, status) {
  res.statusCode = status;
  res.end();
}

function json(res, status, body) {
  const payload = JSON.stringify(body);
  res.statusCode = status;
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  res.setHeader("Content-Length", Buffer.byteLength(payload));
  res.end(payload);
}

export async function startScanWorker(options = {}) {
  const server = createScanWorker(options);
  const host = options.host ?? process.env.QPS_SCAN_HOST ?? "0.0.0.0";
  const port = Number(options.port ?? process.env.QPS_SCAN_PORT ?? 8848);
  await new Promise((resolvePromise, reject) => {
    server.once("error", reject);
    server.listen(port, host, resolvePromise);
  });
  return server;
}

const isMain = process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url));
if (isMain) {
  const server = await startScanWorker();
  const address = server.address();
  console.log(`[object-scan-worker] listening on ${typeof address === "object" ? `${address.address}:${address.port}` : address}`);
}
