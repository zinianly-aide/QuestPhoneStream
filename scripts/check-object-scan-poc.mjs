import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const models = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanModels.cs");
const calibration = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanCalibrationProvider.cs");
const recorder = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanRecorder.cs");
const uploader = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanUploader.cs");
const resultClient = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanResultClient.cs");
const gaussianRenderer = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/GaussianSplatPocRenderer.cs");
const tests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/ObjectScanPocTests.cs");
const manifest = JSON.parse(read("apps/quest-unity-client/Packages/manifest.json"));
const androidManifestPostProcessor = read("apps/quest-unity-client/Assets/QuestPhoneStream/Editor/AndroidManifestPostProcessor.cs");
const worker = read("apps/object-scan-worker/src/server.mjs");
const workerTests = read("apps/object-scan-worker/test/server.test.mjs");
const reconstruct = read("apps/object-scan-worker/src/reconstruct.mjs");
const reconstructTests = read("apps/object-scan-worker/test/reconstruct.test.mjs");

assert(models.includes('qps-object-scan-poc-v1'), "Object scan manifest must be versioned");
assert(models.includes("fx") && models.includes("fy") && models.includes("cameraPosition") && models.includes("cameraRotation"),
  "Object scan dataset must retain reconstruction intrinsics and camera pose");
assert(calibration.includes('"GetCameraPose"') && calibration.includes('GetProperty("Intrinsics"'),
  "PCA calibration adapter must bind official pose and intrinsics surfaces");
assert(calibration.includes('type.Name == "PassthroughCameraAccess"'),
  "Object scan must reuse the PCA provider instead of inventing a camera source");
assert(recorder.includes('Application.persistentDataPath') && recorder.includes('"ObjectScans"'),
  "Object scan frames must be persisted in an app-owned dataset directory");
assert(recorder.includes('frame.EncodeJpg(jpegQuality)') && recorder.includes('"manifest.json"'),
  "Object scan dataset must persist JPEG frames and manifest metadata");
assert(recorder.includes("ObjectScanSamplingPolicy.ShouldCapture"),
  "Object scan must reject redundant viewpoints through the shared sampling policy");
assert(!recorder.includes("SpatialEnvelope") && !recorder.includes("SendSpatial"),
  "G0 object scan must remain local and must not extend Spatial Protocol yet");
assert(tests.includes("SamplingPolicy_RejectsNearlyDuplicatePose") && tests.includes("Manifest_SerializesReconstructionInputs"),
  "Object scan regression tests are incomplete");

assert(manifest.dependencies?.["com.meta.xr.mrutilitykit"] === "85.0.0",
  "Object scan G1 must pin MRUK 85.0.0 instead of floating/latest");
assert(androidManifestPostProcessor.includes('"horizonos.permission.HEADSET_CAMERA"'),
  "Quest build must declare HEADSET_CAMERA through the generated manifest postprocessor");
assert(androidManifestPostProcessor.includes('"com.oculus.feature.PASSTHROUGH"') &&
       androidManifestPostProcessor.includes('AppendAndroidAttribute(doc, passthroughElement, "required", "true")'),
  "Quest build must declare required passthrough support for PCA");

assert(uploader.includes("UnityWebRequest.Head(url)") && uploader.includes("remoteBytes == localBytes"),
  "G2 uploader must resume by skipping already complete remote frames");
assert(uploader.includes("UnityWebRequest.Put(url, bytes)") && uploader.includes("FinalizeUrl"),
  "G2 uploader must PUT raw dataset files and explicitly finalize the session");
assert(!uploader.includes("SpatialEnvelope") && !uploader.includes("SendSpatial"),
  "Bulk object scan transfer must stay outside Spatial Protocol v1");
assert(worker.includes('FRAME_RE = /^frames\\/[0-9]{6}\\.jpg$/') && worker.includes("invalid_file"),
  "Desktop worker must reject paths outside the dataset contract");
assert(worker.includes('route.kind === "finalize"') && worker.includes("missing_frames"),
  "Desktop worker must verify manifest completeness before marking a session ready");
assert(workerTests.includes("uploads frames idempotently") && workerTests.includes("finalize rejects an incomplete dataset"),
  "Desktop worker transfer regression tests are missing");
assert(tests.includes("TransferPaths_AcceptOnlyDatasetFrames"),
  "Quest transfer path validation regression test is missing");

assert(reconstruct.includes('cameraModel: "PINHOLE"') && reconstruct.includes('"feature_extractor"') &&
       reconstruct.includes('"exhaustive_matcher"') && reconstruct.includes('"mapper"'),
  "G3 must build a deterministic COLMAP sparse reconstruction plan");
assert(reconstruct.includes('qps-object-scan-quest-poses-v1') && reconstruct.includes("cameraToWorldUnity"),
  "G3 must preserve original Quest poses as reconstruction evidence");
assert(reconstruct.includes("mixed_intrinsics_not_supported_g3"),
  "G3 must reject sessions whose camera model changes instead of silently corrupting reconstruction");
assert(reconstruct.includes("colmapPointsTextToAsciiPly") && reconstruct.includes('format ascii 1.0'),
  "G3 must export a Quest-preview-compatible ASCII PLY from COLMAP sparse points");
assert(reconstruct.includes('warnings: ["colmap_not_found"]'),
  "G3 must report missing COLMAP as BLOCKED rather than pretending reconstruction succeeded");
assert(reconstructTests.includes("preserving Quest poses") && reconstructTests.includes("ASCII PLY supported by Quest preview"),
  "G3 reconstruction regression tests are missing");

assert(worker.includes('tail === "result"') && worker.includes('tail === "result/sparse-preview.ply"'),
  "G4 worker must expose only fixed result/preview endpoints");
assert(worker.includes('result.status !== "completed"') && worker.includes('"sparse-preview.ply"'),
  "G4 worker must not expose stale preview files before a completed reconstruction");
assert(workerTests.includes("serves only the fixed completed reconstruction result") &&
       workerTests.includes("does not expose preview while reconstruction is blocked"),
  "G4 worker result-serving regressions are missing");
assert(resultClient.includes("ObjectScanResultPaths.PreviewUrl") && resultClient.includes("previewRenderer.LoadUrl(url)"),
  "G4 Quest client must feed the fixed worker preview URL into the existing Gaussian renderer");
assert(resultClient.includes("not world-aligned"),
  "G4 UI state must not claim sparse preview world registration");
assert(!resultClient.includes("GaussianSplatPlyParser.Parse") && gaussianRenderer.includes("GaussianSplatPlyParser"),
  "G4 must reuse the existing Gaussian PLY renderer instead of adding a duplicate parser");
assert(!resultClient.includes("SpatialEnvelope") && !resultClient.includes("SendSpatial"),
  "G4 result return must stay outside Spatial Protocol v1");
assert(tests.includes("ResultPaths_UseFixedWorkerEndpoints") && tests.includes("ReconstructionResult_ParsesCompletedSparsePreview"),
  "G4 Quest result regression tests are missing");

console.log("Object scan POC G0/G1/G2/G3/G4 source checks passed");
