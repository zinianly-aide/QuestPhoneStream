import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const models = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanModels.cs");
const calibration = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanCalibrationProvider.cs");
const recorder = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ObjectScanRecorder.cs");
const tests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/ObjectScanPocTests.cs");

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

console.log("Object scan POC G0 source checks passed");
