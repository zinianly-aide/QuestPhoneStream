import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const manifest = read("apps/quest-unity-client/Packages/manifest.json");
const post = read("apps/quest-unity-client/Assets/QuestPhoneStream/Editor/AndroidManifestPostProcessor.cs");
const vision = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestVisionService.cs");
const ai = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestAiClient.cs");
const ui = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/AiVisionUI.cs");
const settings = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SettingsUI.cs");
const factory = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SettingsUIFactory.cs");

assert(manifest.includes('"com.meta.xr.mrutilitykit": "85.0.0"'), "MRUK passthrough camera dependency missing");
assert(post.includes("horizonos.permission.HEADSET_CAMERA"), "HEADSET_CAMERA manifest permission missing");
assert(post.includes("com.oculus.feature.PASSTHROUGH"), "Quest passthrough feature declaration missing");
assert(vision.includes('type.Name == "PassthroughCameraAccess"') && vision.includes('GetMethod("GetTexture"'), "PCA runtime adapter missing");
assert(vision.includes("RequestUserPermission") && vision.includes("StartCamera") && vision.includes("CaptureSingleFrame"), "Explicit camera lifecycle missing");
assert(ai.includes("OpenAI-compatible") || ai.includes("OpenAiVisionRequest"), "OpenAI-compatible AI client missing");
assert(ai.includes("ImageConversion.EncodeToJPG") && ai.includes("AnalyzeLastFrame"), "AI image request path missing");
for (const label of ["Save AI", "Permission", "Start Camera", "Capture", "Analyze", "Stop"])
  assert(ui.includes(`\"${label}\"`), `AI Vision UI missing ${label}`);
assert(ui.includes("_ai.Configure") && ui.includes("_ai.AnalyzeLastFrame"), "AI Vision UI is not wired to AI client");
assert(ui.includes("_vision.RequestPermission") && ui.includes("_vision.StartCamera") && ui.includes("_vision.CaptureSingleFrame"), "AI Vision UI is not wired to camera lifecycle");
const showBody = ui.slice(ui.indexOf("public void Show()"), ui.indexOf("private void Build()"));
assert(!showBody.includes("StartCamera(") && !showBody.includes("AnalyzeLastFrame("), "Opening AI Vision must not auto-start camera or inference");
assert(settings.includes("ShowAiVision") && settings.includes("HideAiVision"), "Settings AI Vision navigation missing");
assert(factory.includes('"AI Vision"') && factory.includes("AiVisionUI") && factory.includes("QuestVisionService") && factory.includes("QuestAiClient"), "Settings factory AI Vision wiring missing");

console.log("AI Vision source checks passed");
