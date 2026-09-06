import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const signaling = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestSignalingClient.cs");
assert(signaling.includes("private int _attemptGeneration;"),
  "Reconnect attempts need an ownership generation so stale finally blocks cannot clear a newer attempt");
assert(signaling.includes("_activeSignalingUrl") && signaling.includes("ConnectionTargetChanged()"),
  "Reconnect must snapshot and compare the active signaling target");
assert(signaling.includes("if (IsConnecting && !ConnectionTargetChanged())"),
  "Reconnect may reuse an in-flight attempt only when its target is unchanged");
assert(signaling.includes("if (IsConnecting)") && signaling.includes("transportAlreadyStopped = true"),
  "A changed discovered target must invalidate the old in-flight transport before reconnecting");
assert(signaling.includes("attemptGeneration == _attemptGeneration"),
  "Only the owning reconnect attempt may clear IsConnecting");
assert(signaling.includes("Uri.TryCreate(_activeSignalingUrl"),
  "The connection must use the snapshotted endpoint rather than a mutable public field");
assert(signaling.includes('PlayerPrefs.GetString("QuestPhoneStream_SignalingUrl_v2"'),
  "Quest signaling and SettingsUI must use the same persisted signaling URL key");

const receiver = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestWebRtcReceiver.cs");
assert(receiver.includes("_selectedMediaDeviceId = deviceId;"),
  "Selecting a discovered device must retain its selected device identity");
assert(receiver.includes("ApplyDiscoveredSignaling(device.signalingUrl, device.streamId)"),
  "Selecting a discovered device must apply its signaling endpoint and stream identity");
assert(receiver.includes("_ = signaling.ReconnectAsync();"),
  "Selecting a discovered device must enter the signaling/session connection flow");

const home = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestHomeUI.cs");
const deviceClick = home.slice(home.indexOf("private void OnMediaDeviceSelected"), home.indexOf("private void OpenSettings"));
assert(!deviceClick.includes("OpenVideoLibrary"),
  "Selecting a discovered device must not open Media Library automatically");
assert(home.includes("TargetChanged += OnTargetChanged") && home.includes("RefreshMediaDevices();"),
  "Home must refresh device rows on target changes independent of connection state");
assert(signaling.includes("public event Action TargetChanged") && signaling.includes("NotifyTargetChanged()"),
  "Signaling target changes need an explicit UI notification path");
assert(signaling.includes("ResolveSignalingEndpoint") && signaling.includes("persistedEndpoint") && signaling.includes("string.Empty, signalingUrl"),
  "Signaling endpoint resolution must not depend on a fixed LAN fallback");
assert(signaling.includes('public string signalingUrl = ""'),
  "Quest signaling must not contain an environment-specific default endpoint");
assert(!read("apps/quest-unity-client/Assets/QuestPhoneStream/Scenes/QuestPhoneStreamMvp.unity").includes("192.168.1.16"),
  "Quest scene must not contain the development machine signaling address");
assert(!read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SettingsUIFactory.cs").includes("192.168.1.16"),
  "Quest Settings must not contain the development machine signaling address");
assert(!read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MainActivity.kt").includes("192.168.1.16"),
  "Android Settings must not contain the development machine signaling address");

const settings = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SettingsUI.cs");
assert(settings.includes('PlayerPrefs.SetString("QuestPhoneStream_SignalingUrl_v2"'),
  "SettingsUI must persist the same signaling URL key read by QuestSignalingClient");

const androidNsd = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaNsdRegistration.kt");
assert(androidNsd.includes('const val UNIFIED_SERVICE_TYPE = "_qps-device._tcp."'),
  "Android unified NSD service type must match Quest discovery");
assert(androidNsd.includes('const val LEGACY_SERVICE_TYPE = "_qps-media._tcp."'),
  "Android legacy NSD fallback must remain available");
assert(androidNsd.includes('setAttribute("streamId"') && androidNsd.includes('setAttribute("signalingUrl"'),
  "Unified NSD advertisement must carry stream identity and signaling endpoint bootstrap metadata");

const tests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/DeviceSelectConnectTests.cs");
assert(!tests.includes("MediaNsdRegistration.UNIFIED_SERVICE_TYPE is"),
  "Unity tests must not compare expected service types against descriptive placeholder strings");
assert(!tests.includes("GetMethodBody()"),
  "Unity tests must not pretend method existence proves the reconnect call contract");

console.log("Device select -> signaling reconnect source checks passed");
