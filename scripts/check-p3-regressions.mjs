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
const displayGeometry = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/DisplayGeometry.kt");
const displayGeometryTests = read("apps/android-agent/app/src/test/java/com/questphonestream/agent/DisplayGeometryCalculatorTest.kt");
const panelShell = read("apps/quest-unity-client/Assets/QuestPhoneStream/Interaction/Core/SpatialPanelShell.cs");
const phonePanel = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/PhonePanelController.cs");
const screenGeometryTests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/ScreenGeometryTests.cs");
const depth = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestEnvironmentDepthService.cs");
const depthTests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/EnvironmentDepthTests.cs");
const sixDof = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SixDofMediaService.cs");

assert(runtimeAsmdef.includes("UnityEngine.ImageConversionModule"), "Runtime asmdef must reference ImageConversionModule");
assert(vision.includes("ImageConversion.EncodeToJPG(texture") && !vision.includes("GetRawTextureData"), "Vision frames must be encoded JPEG bytes");
assert(ai.includes("ImageConversion.EncodeToJPG(source, 85)") && !ai.includes("GetRawTextureData"), "AI requests must send encoded JPEG bytes, not raw texture data");
assert(visionTests.includes("0xFF") && visionTests.includes("0xD8") && visionTests.includes("bytes[2]"), "JPEG magic-byte regression test missing");
assert(webRtc.includes("DeviceControlPlane.setControlTransportActive(state == DataChannel.State.OPEN)"), "DataChannel OPEN must drive display.control.active");
assert(webRtc.includes("DeviceControlPlane.setControlTransportActive(false)"), "DataChannel teardown must clear display.control.active");

assert(displayGeometry.includes("maxCaptureEdge") && displayGeometry.includes("captureWidth") && displayGeometry.includes("captureHeight"), "Android capture geometry must preserve the real display aspect instead of a fixed canvas");
assert(webRtc.includes("registerDisplayListener") && webRtc.includes("changeCaptureFormat"), "Screen capture must react to runtime display geometry changes without rebuilding the peer");
assert(webRtc.includes("VideoResolutionHolder.update(next.captureWidth, next.captureHeight)"), "Control-space resolution must follow capture geometry changes");
assert(displayGeometryTests.includes("rotationSwapsCaptureOrientationWithoutChangingLongEdgeBudget"), "Android orientation geometry regression coverage missing");
assert(panelShell.includes("SetSurfaceAspect(int pixelWidth, int pixelHeight)"), "SpatialPanel shell must support video-driven surface aspect updates");
assert(phonePanel.includes("_shell.SetSurfaceAspect(width, height)"), "Phone panel must apply decoded video dimensions to the spatial surface");
assert(screenGeometryTests.includes("SurfaceAspect_SwapsPortraitAndLandscapeWithoutRotatingPanelRoot"), "Quest portrait/landscape surface regression coverage missing");

assert(depth.includes("type == typeof(QuestEnvironmentDepthService)") && depth.includes("type.Assembly == typeof(QuestEnvironmentDepthService).Assembly"), "Environment depth discovery must reject self/runtime assembly providers");
assert(!depth.includes("AddComponent(_componentType)"), "Environment depth reflection discovery must not auto-add unknown provider components");
assert(depth.includes("private IEnvironmentDepthProvider EnsureProvider()") && !depth.includes("_provider = new MetaEnvironmentDepthProvider(gameObject)"), "Environment depth provider discovery must be lazy at startup");
assert(depthTests.includes("DepthServiceCannotBeDiscoveredAsItsOwnProvider") && depthTests.includes("AssemblyScanCount"), "Environment depth self-recursion/lazy-start regression tests missing");
assert(!sixDof.includes("AddComponent(_componentType)"), "6DoF reflection discovery must not auto-add unknown provider components");
assert(sixDof.includes("type.Assembly == typeof(SixDofMediaService).Assembly"), "6DoF provider discovery should reject QuestPhoneStream runtime types");

console.log("P3 startup/vision/control/screen-geometry regression source checks passed");
