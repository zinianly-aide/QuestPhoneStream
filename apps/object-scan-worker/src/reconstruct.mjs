import { existsSync } from "node:fs";
import { mkdir, readFile, readdir, writeFile } from "node:fs/promises";
import { spawnSync } from "node:child_process";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SESSION_RE = /^[A-Za-z0-9_-]{1,80}$/;
const FRAME_RE = /^frames\/[0-9]{6}\.jpg$/;

export async function prepareReconstruction({ root, sessionId, outputRoot }) {
  if (!SESSION_RE.test(sessionId ?? "")) throw new Error("invalid_session_id");
  const sessionPath = resolve(root, sessionId);
  if (!existsSync(join(sessionPath, "READY.json"))) throw new Error("session_not_finalized");

  const manifest = JSON.parse(await readFile(join(sessionPath, "manifest.json"), "utf8"));
  const frames = Array.isArray(manifest.frames) ? manifest.frames : [];
  if (frames.length < 3) throw new Error("at_least_three_frames_required");
  if (Number(manifest.frameCount ?? frames.length) !== frames.length) throw new Error("frame_count_mismatch");

  const first = normalizeIntrinsics(frames[0]);
  for (const frame of frames) {
    if (!FRAME_RE.test(frame?.image ?? "")) throw new Error("invalid_frame_path");
    const imagePath = join(sessionPath, ...frame.image.split("/"));
    if (!existsSync(imagePath)) throw new Error(`missing_frame:${frame.image}`);
    assertCompatibleIntrinsics(first, normalizeIntrinsics(frame));
  }

  const runRoot = resolve(outputRoot ?? join(sessionPath, "reconstruction", "colmap"));
  await mkdir(runRoot, { recursive: true });
  const poseSidecar = {
    version: "qps-object-scan-quest-poses-v1",
    sessionId,
    coordinateSystem: manifest.coordinateSystem ?? "unity-world-y-up-z-forward",
    note: "Quest poses are preserved as priors/evidence. G3 COLMAP estimates its own reconstruction poses.",
    frames: frames.map(frame => ({
      image: frame.image,
      timestampMs: frame.timestampMs,
      cameraPosition: frame.cameraPosition,
      cameraRotation: frame.cameraRotation,
      cameraToWorldUnity: poseToMatrix(frame.cameraPosition, frame.cameraRotation),
      intrinsics: frame.intrinsics
    }))
  };
  const posePath = join(runRoot, "quest-poses.json");
  await writeFile(posePath, JSON.stringify(poseSidecar, null, 2));

  const plan = buildColmapPlan({ sessionPath, runRoot, intrinsics: first });
  await writeFile(join(runRoot, "plan.json"), JSON.stringify(plan, null, 2));
  return { manifest, frames, sessionPath, runRoot, intrinsics: first, posePath, plan };
}

export function buildColmapPlan({ sessionPath, runRoot, intrinsics }) {
  const database = join(runRoot, "database.db");
  const imagePath = join(sessionPath, "frames");
  const sparseRoot = join(runRoot, "sparse");
  const cameraParams = [intrinsics.fx, intrinsics.fy, intrinsics.cx, intrinsics.cy]
    .map(value => Number(value).toFixed(8)).join(",");
  return {
    backend: "colmap",
    imagePath,
    database,
    sparseRoot,
    cameraModel: "PINHOLE",
    cameraParams,
    commands: [
      ["feature_extractor", "--database_path", database, "--image_path", imagePath,
        "--ImageReader.single_camera", "1", "--ImageReader.camera_model", "PINHOLE",
        "--ImageReader.camera_params", cameraParams],
      ["exhaustive_matcher", "--database_path", database],
      ["mapper", "--database_path", database, "--image_path", imagePath, "--output_path", sparseRoot]
    ]
  };
}

export async function runColmapReconstruction(prepared, options = {}) {
  const resultPath = join(prepared.runRoot, "result.json");
  if (options.dryRun) {
    const result = baseResult(prepared, "planned", { warnings: ["dry_run_no_colmap_execution"] });
    await writeFile(resultPath, JSON.stringify(result, null, 2));
    return result;
  }

  const command = options.command ?? "colmap";
  const probe = spawnSync(command, ["-h"], { stdio: "ignore", shell: false });
  if (probe.error?.code === "ENOENT") {
    const result = baseResult(prepared, "blocked", { warnings: ["colmap_not_found"] });
    await writeFile(resultPath, JSON.stringify(result, null, 2));
    return result;
  }

  await mkdir(prepared.plan.sparseRoot, { recursive: true });
  const started = Date.now();
  for (const args of prepared.plan.commands) runCommand(command, args);

  const models = (await readdir(prepared.plan.sparseRoot, { withFileTypes: true }))
    .filter(entry => entry.isDirectory())
    .map(entry => entry.name)
    .sort((a, b) => Number(a) - Number(b));
  if (!models.length) throw new Error("colmap_mapper_produced_no_model");
  const modelPath = join(prepared.plan.sparseRoot, models[0]);
  const textPath = join(prepared.runRoot, "sparse-text");
  await mkdir(textPath, { recursive: true });
  runCommand(command, ["model_converter", "--input_path", modelPath, "--output_path", textPath, "--output_type", "TXT"]);

  const pointsPath = join(textPath, "points3D.txt");
  if (!existsSync(pointsPath)) throw new Error("colmap_points3d_missing");
  const previewPly = join(prepared.runRoot, "sparse-preview.ply");
  const preview = colmapPointsTextToAsciiPly(await readFile(pointsPath, "utf8"));
  await writeFile(previewPly, preview);

  const result = baseResult(prepared, "completed", {
    elapsedMs: Date.now() - started,
    outputs: {
      sparseModel: relativeTo(prepared.sessionPath, modelPath),
      sparseText: relativeTo(prepared.sessionPath, textPath),
      sparsePreviewPly: relativeTo(prepared.sessionPath, previewPly),
      questPoses: relativeTo(prepared.sessionPath, prepared.posePath)
    }
  });
  await writeFile(resultPath, JSON.stringify(result, null, 2));
  return result;
}

export function colmapPointsTextToAsciiPly(text) {
  const points = [];
  for (const raw of String(text ?? "").replace(/\r/g, "").split("\n")) {
    const line = raw.trim();
    if (!line || line.startsWith("#")) continue;
    const parts = line.split(/\s+/);
    if (parts.length < 8) continue;
    const x = Number(parts[1]), y = Number(parts[2]), z = Number(parts[3]);
    const r = Number(parts[4]), g = Number(parts[5]), b = Number(parts[6]);
    if (![x, y, z, r, g, b].every(Number.isFinite)) continue;
    points.push([x, y, z, clampByte(r), clampByte(g), clampByte(b)]);
  }
  const header = [
    "ply", "format ascii 1.0", `element vertex ${points.length}`,
    "property float x", "property float y", "property float z",
    "property uchar red", "property uchar green", "property uchar blue",
    "end_header"
  ];
  return header.concat(points.map(point => point.join(" "))).join("\n") + "\n";
}

function normalizeIntrinsics(frame) {
  const i = frame?.intrinsics ?? {};
  const normalized = {
    width: Number(frame?.width ?? i.sensorWidth),
    height: Number(frame?.height ?? i.sensorHeight),
    fx: Number(i.fx), fy: Number(i.fy), cx: Number(i.cx), cy: Number(i.cy)
  };
  if (![normalized.width, normalized.height, normalized.fx, normalized.fy, normalized.cx, normalized.cy]
    .every(value => Number.isFinite(value) && value > 0)) throw new Error("invalid_intrinsics");
  return normalized;
}

function assertCompatibleIntrinsics(reference, current) {
  if (reference.width !== current.width || reference.height !== current.height) throw new Error("mixed_image_resolution_not_supported_g3");
  for (const key of ["fx", "fy", "cx", "cy"]) {
    const tolerance = Math.max(0.5, Math.abs(reference[key]) * 0.002);
    if (Math.abs(reference[key] - current[key]) > tolerance) throw new Error("mixed_intrinsics_not_supported_g3");
  }
}

export function poseToMatrix(position = {}, rotation = {}) {
  const x = Number(rotation.x ?? 0), y = Number(rotation.y ?? 0), z = Number(rotation.z ?? 0), w = Number(rotation.w ?? 1);
  const px = Number(position.x ?? 0), py = Number(position.y ?? 0), pz = Number(position.z ?? 0);
  const xx = x * x, yy = y * y, zz = z * z;
  const xy = x * y, xz = x * z, yz = y * z;
  const wx = w * x, wy = w * y, wz = w * z;
  return [
    [1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy), px],
    [2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx), py],
    [2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy), pz],
    [0, 0, 0, 1]
  ];
}

function clampByte(value) { return Math.max(0, Math.min(255, Math.round(value))); }

function runCommand(command, args) {
  const result = spawnSync(command, args, { stdio: "inherit", shell: false });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command}_${args[0]}_failed:${result.status}`);
}

function baseResult(prepared, status, extra = {}) {
  return {
    version: "qps-object-scan-result-v1",
    sessionId: prepared.manifest.sessionId,
    backend: "colmap",
    status,
    generatedUtc: new Date().toISOString(),
    inputFrames: prepared.frames.length,
    cameraModel: prepared.plan.cameraModel,
    ...extra
  };
}

function relativeTo(base, target) { return target.startsWith(base) ? target.slice(base.length + 1) : target; }

async function main() {
  const args = process.argv.slice(2);
  const sessionIndex = args.indexOf("--session");
  if (sessionIndex < 0 || !args[sessionIndex + 1]) throw new Error("usage: reconstruct.mjs --session <id> [--root <dir>] [--dry-run]");
  const rootIndex = args.indexOf("--root");
  const root = resolve(rootIndex >= 0 && args[rootIndex + 1] ? args[rootIndex + 1] : process.env.QPS_SCAN_ROOT ?? join(process.cwd(), "object-scans"));
  const prepared = await prepareReconstruction({ root, sessionId: args[sessionIndex + 1] });
  const result = await runColmapReconstruction(prepared, { dryRun: args.includes("--dry-run") });
  console.log(JSON.stringify(result, null, 2));
  if (result.status === "blocked") process.exitCode = 2;
}

const isMain = process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url));
if (isMain) main().catch(error => { console.error("[object-scan-reconstruct]", error.message); process.exitCode = 1; });
