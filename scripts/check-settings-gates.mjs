// Source/contract checks only. These do NOT certify Unity execution or either hardware Gate.
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";

const root = fileURLToPath(new URL("../", import.meta.url));
const read = path => readFileSync(root + path, "utf8");
const scripts = "apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/";
const ui = read(scripts + "SettingsUI.cs");
const factory = read(scripts + "SettingsUIFactory.cs");
const receiver = read(scripts + "QuestWebRtcReceiver.cs");
const signaling = read(scripts + "QuestSignalingClient.cs");
const rig = read(scripts + "QuestXrUiRig.cs");
const keyboard = read(scripts + "QuestKeyboardInputField.cs");

test("settings dependencies are explicit; Awake/Start cannot race initialization", () => {
  assert.match(factory, /Initialize\(QuestSignalingClient signalingClient, Camera xrCamera\)/);
  assert.doesNotMatch(factory, /void (Awake|Start)\(/);
  assert.doesNotMatch(ui, /void (Awake|Start|Update)\(/);
  for (const source of [factory, ui, receiver, rig])
    assert.doesNotMatch(source, /Camera\.main|Find(?:First|Any)?Object(?:s)?(?:OfType|ByType)/);
  assert.match(factory, /_settingsUI\.Initialize\(signalingClient, xrCamera\)/);
});

test("visibility has one source of truth", () => {
  assert.match(ui, /bool IsVisible => canvas != null && canvas\.gameObject\.activeInHierarchy/);
  assert.match(receiver, /_settingsUI\.Toggle\(\)/);
  assert.doesNotMatch(receiver, /_settingsUI\.gameObject\.activeSelf/);
  assert.match(ui, /public void Toggle\(\) \{ if \(IsVisible\) Hide\(\); else Show\(\); \}/);
});

test("canvas uses pixel layout and small world scale with positive input padding", () => {
  const [width, height] = factory.match(/sizeDelta = new Vector2\((\d+), (\d+)\)/).slice(1).map(Number);
  const scale = Number(factory.match(/localScale = Vector3.one \* ([\d.]+)f/)[1]);
  assert.equal(width * scale, 2);
  assert.equal(height * scale, 1.5);
  const fonts = [...factory.matchAll(/fontSize = (\d+)/g)].map(m => Number(m[1]));
  assert.ok(width > Math.max(...fonts) * 30);
  // Canvas -> panel anchors(.8) -> row(.9 x .1) -> input(.63 x .8) -> padding.
  assert.ok(width * .8 * .9 * .63 - 20 > 0);
  assert.ok(height * .8 * .1 * .8 - 10 > 22);
});

test("Quest input fields have a native keyboard fallback", () => {
  assert.match(factory, /AddComponent<QuestKeyboardInputField>/);
  assert.match(factory, /shouldHideMobileInput = false/);
  assert.match(keyboard, /TouchScreenKeyboard\.Open/);
  assert.match(keyboard, /TouchScreenKeyboard\.isSupported/);
  assert.match(keyboard, /Status\.Done/);
});

test("scene explicitly wires camera/rig and XR input uses XRI 3 standard components", () => {
  const scene = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scenes/QuestPhoneStreamMvp.unity");
  assert.match(scene, /xrCamera: \{fileID: 332583113\}/);
  assert.match(scene, /xrUiRig: \{fileID: 1624316271\}/);
  assert.match(read("apps/quest-unity-client/ProjectSettings/ProjectSettings.asset"), /activeInputHandler: 1/);
  for (const component of ["XROrigin", "EventSystem", "XRUIInputModule", "XRRayInteractor", "TrackedPoseDriver"])
    assert.ok(rig.includes("AddComponent<" + component + ">"));
  assert.match(factory, /AddComponent<TrackedDeviceGraphicRaycaster>/);
  assert.doesNotMatch(rig, /Actions.AddAction|new InputActionMap/);
  const asset = JSON.parse(read("apps/quest-unity-client/Assets/QuestPhoneStream/Resources/QuestUi.inputactions"));
  const map = asset.maps.find(m => m.name === "Quest UI");
  for (const name of ["Open Settings", "LeftHand UI Click", "RightHand UI Click", "Head UI Point Position"])
    assert.ok(map.bindings.some(b => b.action === name && b.path));
  assert.match(scene, /actionAsset: \{fileID: -944628639613478452, guid: 7c0a585c07cb4b58bb3fa43a25c55f87/);
  assert.match(rig, /LeftHand/);
  assert.match(rig, /RightHand/);
  assert.doesNotMatch(receiver + rig + read(scripts + "MenuController.cs"), /Input.GetKey/);
});

test("connect is awaited, ACK-gated and receives complete websocket messages", () => {
  assert.match(ui, /await signalingClient\.ReconnectAsync\(\)/);
  assert.match(ui, /StateChanged \+= OnStateChanged/);
  assert.ok(signaling.indexOf("WaitFor(_registered.Task") < signaling.indexOf('type = "create_session"'));
  assert.match(signaling, /while \(!result.EndOfMessage\)/);
  assert.match(signaling, /_sendLock.WaitAsync/);
  assert.match(signaling, /WaitFor\(_mediaReady.Task/);
  assert.doesNotMatch(signaling, /connected successfully|Debug\.Log.*token/);
});

test("media callbacks and ICE are scoped and disposed on invalidation", () => {
  assert.match(receiver, /NegotiationInvalidated \+= ResetPeer/);
  assert.match(receiver, /if \(IsCurrent\(peer, id\) && _videoTrack == track\)/);
  assert.match(receiver, /_pendingIce.Clear\(\)/);
  assert.match(receiver, /controlChannel\?\.ResetChannel\(\)/);
  assert.match(receiver, /private void LateUpdate\(\)/);
  assert.match(read(scripts + "ControlChannel.cs"), /_channel\?\.Dispose\(\)/);
});

test("Android creates fresh peers without repeating MediaProjection startCapture", () => {
  const android = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/WebRtcStreamer.kt");
  const sessionBody = android.slice(android.indexOf("fun startSession("), android.indexOf("private fun createOffer("));
  assert.match(sessionBody, /resetPeer\(\)/);
  assert.match(sessionBody, /createPeerConnection/);
  assert.doesNotMatch(sessionBody, /startCapture|ScreenCapturerAndroid/);
  assert.match(android, /if \(!isCurrent\(epoch\)\) return@post/);
  assert.match(android, /if \(disposed\) return/);
});

test("Android manifest is valid XML", () => {
  execFileSync("python3", ["-c", "import sys, xml.etree.ElementTree as E; E.parse(sys.argv[1])",
    root + "apps/android-agent/app/src/main/AndroidManifest.xml"]);
});
