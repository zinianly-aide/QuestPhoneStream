import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = process.cwd();
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");
const count = (text, needle) => text.split(needle).length - 1;
const check = (condition, message) => {
  if (!condition) throw new Error(message);
  console.log(`PASS ${message}`);
};

const vision = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestVisionService.cs");
check(vision.includes("DiscoveryRetrySeconds") && vision.includes("_nextProviderRefreshAt"),
  "vision discovery is throttled");
check(!vision.includes("private void Update()\n        {\n            _provider?.Refresh();"),
  "vision Update does not refresh provider every frame");

const mediaServer = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaHttpServer.kt");
const applied = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/AppliedConfig.kt");
const androidPlane = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/AndroidSpatialControlPlane.kt");
const activity = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MainActivity.kt");
check(!mediaServer.includes("CONFIG_POLL_MS") && !mediaServer.includes("metadataMonitor"),
  "Android no longer polls draft config every 250ms");
check(applied.includes("AppliedConfigStore") && androidPlane.includes("AppliedConfigStore.apply(requested)"),
  "Android endpoint identity changes only through AppliedConfig");
check(count(androidPlane, "DeviceControlPlane.configure(") === 1,
  "explicit Apply performs one DeviceControlPlane reconfigure");
const saveBody = activity.slice(activity.indexOf("private fun saveConfigurationAndRefreshNsd"), activity.indexOf("private fun showMediaManager"));
check(count(saveBody, "AndroidSpatialControlPlane.start(") === 1 && count(saveBody, "refreshNsdMetadata(") === 1,
  "Save/Apply invokes one reconfigure path and one NSD refresh path");
check(count(mediaServer.slice(mediaServer.indexOf("fun refreshNsdMetadata"), mediaServer.indexOf("private fun acceptLoop")), "refreshUnifiedAdvertisement()") === 1,
  "Apply refreshes unified NSD advertisement once");

const renderer = read("apps/macos-sender/src/renderer.ts");
const subscriptions = read("apps/macos-sender/src/subscriptions.ts");
check(renderer.includes("subscription.created") && renderer.includes("message.correlationId") && renderer.includes("payload?.retryable === true"),
  "Mac handles created/correlation/retryable subscription responses");
check(renderer.includes("subscriptions.reset()") && subscriptions.includes('"requested" | "created" | "active"'),
  "Mac subscription lifecycle resets for reconnect/resubscribe");

const devHud = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/DeveloperHud.cs");
const legacyHud = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestDeveloperHud.cs");
const settingsFactory = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SettingsUIFactory.cs");
check(devHud.includes('LegacyRuntime.ttf') && devHud.includes("Auto Refresh (1 Hz)"),
  "Developer HUD has refresh controls and legacy runtime font");
check(!legacyHud.includes("RuntimeInitializeOnLoadMethod") && !legacyHud.includes("camera.transform"),
  "Developer HUD is not auto-created as a head-locked overlay");
check(settingsFactory.includes("#if QPS_DEV_TOOLS || DEVELOPMENT_BUILD || UNITY_EDITOR") && settingsFactory.includes("developerHud.Initialize("),
  "Developer HUD entry is dev-only and lives under Developer Tools");

const lifecycle = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaCapabilityLifecycle.kt");
const registry = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/CapabilityRegistry.kt");
check(["media.list", "media.open", "media.publish"].every(name => lifecycle.includes(name)),
  "media capability lifecycle covers list/open/publish");
check(mediaServer.includes("markPairingAuthorized()") && mediaServer.includes("beginRequest(MediaCapabilityLifecycle.MEDIA_PUBLISH)"),
  "media authorization/activity follows pairing and HTTP request lifecycle");
check(count(registry, "available = false, authorized = false, active = false") >= 3,
  "media capabilities are not statically advertised available");
