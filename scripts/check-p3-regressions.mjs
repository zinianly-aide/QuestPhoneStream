import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const vision = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestVisionService.cs");
const ai = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestAiClient.cs");
const runtimeAsmdef = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestPhoneStream.Runtime.asmdef");
const visionTests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/QuestVisionTests.cs");
const webRtc = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/WebRtcStreamer.kt");
const depth = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestEnvironmentDepthService.cs");
const depthTests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/EnvironmentDepthTests.cs");
const sixDof = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SixDofMediaService.cs");

assert(runtimeAsmdef.includes("UnityEngine.ImageConversionModule"), "Runtime asmdef must reference ImageConversionModule");
assert(vision.includes("ImageConversion.EncodeToJPG(texture") && !vision.includes("GetRawTextureData"), "Vision frames must be encoded JPEG bytes");
assert(ai.includes("ImageConversion.EncodeToJPG(source, 85)") && !ai.includes("GetRawTextureData"), "AI requests must send encoded JPEG bytes, not raw texture data");
assert(visionTests.includes("0xFF") && visionTests.includes("0xD8") && visionTests.includes("bytes[2]"), "JPEG magic-byte regression test missing");
assert(webRtc.includes("DeviceControlPlane.setControlTransportActive(state == DataChannel.State.OPEN)"), "DataChannel OPEN must drive display.control.active");
assert(webRtc.includes("DeviceControlPlane.setControlTransportActive(false)"), "DataChannel teardown must clear display.control.active");

assert(depth.includes("type == typeof(QuestEnvironmentDepthService)") && depth.includes("type.Assembly == typeof(QuestEnvironmentDepthService).Assembly"), "Environment depth discovery must reject self/runtime assembly providers");
assert(!depth.includes("AddComponent(_componentType)"), "Environment depth reflection discovery must not auto-add unknown provider components");
assert(depth.includes("private IEnvironmentDepthProvider EnsureProvider()") && !depth.includes("_provider = new MetaEnvironmentDepthProvider(gameObject)"), "Environment depth provider discovery must be lazy at startup");
assert(depthTests.includes("DepthServiceCannotBeDiscoveredAsItsOwnProvider") && depthTests.includes("AssemblyScanCount"), "Environment depth self-recursion/lazy-start regression tests missing");
assert(!sixDof.includes("AddComponent(_componentType)"), "6DoF reflection discovery must not auto-add unknown provider components");
assert(sixDof.includes("type.Assembly == typeof(SixDofMediaService).Assembly"), "6DoF provider discovery should reject QuestPhoneStream runtime types");

console.log("P3 startup/vision/control regression source checks passed");
