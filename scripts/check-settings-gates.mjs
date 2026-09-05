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
const home = read(scripts + "QuestHomeUI.cs");
const mediaUi = read(scripts + "MediaLibraryUI.cs");
const mediaPlayback = read(scripts + "MediaPlaybackController.cs");
const mediaRenderer = read(scripts + "VrMediaRenderer.cs");
const flatPanel = read(scripts + "FlatMediaPanelController.cs");
const mediaDiscovery = read(scripts + "MediaDeviceDiscovery.cs");
const mediaDto = read(scripts + "MediaItemDto.cs");
const settings = read(scripts + "SettingsUI.cs");
const vrShader = read("apps/quest-unity-client/Assets/QuestPhoneStream/Shaders/VRMediaStereo.shader");
const androidMediaItem = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaItem.kt");
const androidMediaServer = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaHttpServer.kt");
const androidNsd = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaNsdRegistration.kt");
const androidIdentity = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaDeviceIdentity.kt");

test("settings dependencies are explicit; Awake/Start cannot race initialization", () => {
  assert.match(factory, /Initialize\(QuestSignalingClient signalingClient, Camera xrCamera\)/);
  assert.doesNotMatch(factory, /void (Awake|Start)\(/);
  assert.doesNotMatch(ui, /void (Awake|Start|Update)\(/);
  for (const source of [factory, ui])
    assert.doesNotMatch(source, /Camera\.main|Find(?:First|Any)?Object(?:s)?(?:OfType|ByType)/);
  assert.doesNotMatch(rig, /Camera\.main/);
  assert.match(factory, /_settingsUI\.Initialize\(signalingClient, xrCamera\)/);
});

test("visibility has one source of truth", () => {
  assert.match(ui, /bool IsVisible => canvas != null && canvas\.gameObject\.activeInHierarchy/);
  assert.match(receiver, /(_settingsUI\.Toggle\(\)|_receiver\.ToggleHome|_homeUI\?\.Toggle\(\))/);
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
  assert.match(receiver, /RenderVideoAtEndOfFrame/);
  assert.match(read(scripts + "ControlChannel.cs"), /_channel\?\.Dispose\(\)/);
});

test("Android creates fresh peers without repeating MediaProjection startCapture", () => {
  const android = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/WebRtcStreamer.kt");
  const sessionBody = android.slice(android.indexOf("fun startSession("), android.indexOf("private fun createOffer("));
  assert.match(sessionBody, /(resetPeer|teardownPeer)\(\)/);
  assert.match(sessionBody, /createPeerConnection/);
  assert.doesNotMatch(sessionBody, /startCapture|ScreenCapturerAndroid/);
  assert.match(android, /if \(!isCurrent\(epoch\)\) return@post/);
  assert.match(android, /if \(disposed\) return/);
});

test("Android manifest is valid XML", () => {
  execFileSync("python3", ["-c", "import sys, xml.etree.ElementTree as E; E.parse(sys.argv[1])",
    root + "apps/android-agent/app/src/main/AndroidManifest.xml"]);
});

test("media control endpoints require pairing and cleartext false is corrected", () => {
  const mediaServer = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaHttpServer.kt");
  const auth = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaPairingAuth.kt");
  const client = read(scripts + "MediaCatalogClient.cs");
  const manifestProcessor = read("apps/quest-unity-client/Assets/QuestPhoneStream/Editor/AndroidManifestPostProcessor.cs");
  assert.match(mediaServer, /ifAuthorized\(headers, output\) \{ sendCatalog\(output\) \}/);
  assert.match(mediaServer, /ifAuthorized\(headers, output\) \{ sendMetadata\(output/);
  assert.match(mediaServer, /ifAuthorized\(headers, output\) \{ issueToken\(output/);
  assert.match(mediaServer, /sendContent\(output, method == "HEAD"/);
  const streamBody = mediaServer.slice(mediaServer.indexOf("private fun sendContent"), mediaServer.indexOf("private fun openStream"));
  assert.ok(streamBody.indexOf("openStream(item)") < streamBody.indexOf("writeHeaders(output"));
  assert.match(auth, /Bearer /);
  assert.match(client, /SetRequestHeader\("Authorization", "Bearer /);
  assert.match(manifestProcessor, /cleartextAttr\.Value, "true"/);
  assert.doesNotMatch(client, /192\.168\.1\.6/);
});

test("Quest normal flow is compact and keeps engineering fields behind Advanced Settings", () => {
  assert.match(home, /QuestHomeCanvas/);
  assert.match(home, /MakeButton\(panelGo\.transform, "Phone"/);
  assert.match(home, /MakeButton\(panelGo\.transform, "Videos"/);
  assert.match(home, /MakeButton\(panelGo\.transform, "Keyboard"/);
  assert.match(home, /MakeButton\(panelGo\.transform, "Settings"/);
  assert.match(home, /Screen  ·  /);
  assert.match(home, /Control  ·  /);
  assert.match(home, /Media  ·  /);
  assert.match(read(scripts + "QuestWebRtcReceiver.cs"), /EnsureHomeUI\(\)/);
  assert.match(read(scripts + "QuestXrUiRig.cs"), /_receiver\.ToggleHome\(\)/);
  assert.match(read(scripts + "SettingsUIFactory.cs"), /Advanced Settings/);
});

test("Android normal flow exposes readiness and hides engineering controls", () => {
  const android = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MainActivity.kt");
  for (const label of ["READY", "Screen Sharing", "Remote Control", "Media", "Advanced settings"])
    assert.match(android, new RegExp(label));
  assert.match(android, /visibility = View\.GONE/);
  assert.match(android, /private fun updateHomeStatus\(\)/);
  assert.match(android, /Settings\.Secure\.ENABLED_ACCESSIBILITY_SERVICES/);
  assert.match(android, /openAccessibilitySettings\(\)/);
});

test("Quest video library exposes playback controls without closing after play", () => {
  assert.match(mediaUi, /BuildPlaybackControls\(_panel\.transform\)/);
  for (const method of ["Pause", "Resume", "Seek", "SetVolume"])
    assert.match(mediaUi, new RegExp(`\\.${method}\\(`));
  assert.match(mediaUi, /SetStatus\("Playing: " \+ item\.name\)/);
  assert.doesNotMatch(mediaUi, /SetStatus\("Playing: " \+ item\.name\)[\s\S]{0,180}Close\(\)/);
});

test("VR media renderer keeps flat playback and supports projection/stereo switching", () => {
  assert.match(mediaDto, /ProjectionMode \{ Flat, Equirectangular \}/);
  assert.match(mediaDto, /StereoMode \{ Mono, Sbs \}/);
  assert.match(mediaDto, /EyeOrder \{ Lr, Rl \}/);
  assert.match(mediaDto, /projection;[\s\S]*fov;[\s\S]*stereo;[\s\S]*eyeOrder;/);
  assert.match(mediaDto, /projection = ProjectionMode\.Flat, fov = 360, stereo = StereoMode\.Mono, eyeOrder = EyeOrder\.Lr/);
  assert.match(mediaRenderer, /public void Apply\(RenderTexture texture, ProjectionMode projection, int fov, StereoMode stereo, EyeOrder eyeOrder\)/);
  assert.match(mediaRenderer, /PrimitiveType\.Sphere/);
  assert.match(mediaRenderer, /HideVr\(\)/);
  assert.match(mediaRenderer, /VRMediaStereo/);
  assert.match(vrShader, /Cull Front/);
  assert.match(vrShader, /unity_StereoEyeIndex/);
  assert.match(vrShader, /_EyeOrder/);
  assert.match(vrShader, /float lon = atan2\(dir\.x, dir\.z\)/);
  assert.match(vrShader, /is180 && abs\(lon\) > UNITY_PI \/ 2\.0/);
  assert.match(vrShader, /lon \/ UNITY_PI \+ 0\.5/);
  assert.match(vrShader, /lon \/ \(2\.0 \* UNITY_PI\) \+ 0\.5/);
  assert.match(vrShader, /float sampledU = is180 \? saturate\(u\) : frac\(u\)/);
  assert.ok(vrShader.indexOf("float u =") < vrShader.indexOf("if (_Stereo > 0.5)"), "SBS sampling must follow base longitude mapping");
  assert.match(mediaRenderer, /private void LateUpdate\(\)[\s\S]*IsVrVisible[\s\S]*_sphere\.transform\.position = xrCamera\.transform\.position/);
  assert.doesNotMatch(mediaRenderer, /private void LateUpdate\(\)[\s\S]*_sphere\.transform\.rotation/);
  assert.match(mediaPlayback, /PlayUrl\(string url\) => PlayUrl\(url, MediaVideoProfile\.Default\)/);
  assert.match(mediaPlayback, /vrRenderer\?\.Apply\(renderer\.RenderTexture/);
  assert.match(mediaPlayback, /public void ApplyProfile\(MediaVideoProfile profile\)/);
  assert.match(mediaPlayback, /vrRenderer\?\.Release\(\)/);
  assert.match(mediaPlayback, /phoneScreenRenderer != null\) phoneScreenRenderer\.enabled = false/);
  assert.match(mediaPlayback, /phoneScreenRenderer != null\) phoneScreenRenderer\.enabled = true/);
  assert.match(mediaUi, /MediaVideoProfile\.From\(item\)/);
  assert.match(mediaUi, /PlayUrl\(url, profile\)/);
  for (const field of ["projection", "fov", "stereo", "eyeOrder"])
    assert.match(androidMediaItem, new RegExp("\\b" + field + ":"));
  assert.match(androidMediaServer, /put\("projection", item\.projection\)/);
  assert.match(androidMediaServer, /put\("eyeOrder", item\.eyeOrder\)/);
});

test("VR playback preserves overrides and explicitly references the VR shader asset", () => {
  const scene = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scenes/QuestPhoneStreamMvp.unity");
  const vrMaterial = read("apps/quest-unity-client/Assets/QuestPhoneStream/Materials/VRMediaStereo.mat");
  assert.match(mediaUi, /private bool _manualProfileOverride/);
  assert.match(mediaUi, /if \(!_manualProfileOverride\)[\s\S]*MediaVideoProfile\.From\(item\)/);
  assert.match(mediaUi, /ApplySelectedProfile\(true\)/);
  assert.match(mediaRenderer, /public Material vrMaterialTemplate/);
  assert.match(mediaRenderer, /new Material\(vrMaterialTemplate\)/);
  assert.match(mediaRenderer, /Shader\.Find\("QuestPhoneStream\/VRMediaStereo"\)/);
  assert.match(mediaRenderer, /VR media shader is unavailable/);
  assert.match(mediaRenderer, /projection=.*fov=.*stereo=.*eye=.*shader=.*sphereVisible=/);
  assert.match(receiver, /public Material vrMaterialTemplate/);
  assert.match(receiver, /Initialize\(xrCamera, mediaPlayback\.renderer, vrMaterialTemplate\)/);
  assert.match(vrMaterial, /guid: 7f3ef1a3e7c04b1d9a6c8f20e3b64512/);
  assert.match(scene, /vrMaterialTemplate: \{fileID: 2100000, guid: 4a7c8e0d9f214b7ea6c3d8e1f5a902bd, type: 2\}/);
});

test("Flat playback preserves aspect ratio and gates XRI interaction by projection", () => {
  assert.match(flatPanel, /XRGrabInteractable/);
  assert.match(flatPanel, /_aspectRatio = width \/ \(float\)height/);
  assert.match(flatPanel, /_baseLongSide = 1\.6f/);
  assert.match(flatPanel, /minScale = 0\.5f/);
  assert.match(flatPanel, /maxScale = 2\.5f/);
  assert.match(flatPanel, /projection == ProjectionMode\.Flat/);
  assert.match(flatPanel, /grabInteractable\.enabled = IsFlatActive/);
  assert.match(flatPanel, /panelRenderer\.enabled = IsFlatActive/);
  assert.match(flatPanel, /cameraTransform\.position \+ cameraForward\.normalized \* 1\.5f/);
  assert.match(mediaPlayback, /SetVideoDimensions\(\(int\)player\.width, \(int\)player\.height\)/);
  assert.match(mediaPlayback, /flatPanelController\?\.SetProjection\(Profile\.projection\)/);
  for (const label of ['"-"', '"Rotate"', '"Reset"'])
    assert.match(mediaUi, new RegExp(`MakeButton\\(parent, ${label}`));
  assert.match(mediaUi, /MakeButton\(parent, "\+"/);
  assert.match(mediaUi, /flatPanelController\?\.ScaleDown\(\)/);
  assert.match(mediaUi, /flatPanelController\?\.ScaleUp\(\)/);
  assert.match(mediaUi, /flatPanelController\?\.RotateOrientation\(\)/);
  assert.match(mediaUi, /flatPanelController\?\.ResetPose\(\)/);
  assert.match(rig, /ray\.selectInput = new XRInputButtonReader/);
  assert.match(rig, /Reference\(hand \+ " UI Click"\)/);
});

test("Flat panel reset is world-locked and orientation toggles exactly between 0 and 90 degrees", () => {
  assert.doesNotMatch(flatPanel, /ResetPose\(\)[\s\S]*SetParent\(xrCamera/);
  assert.match(flatPanel, /cameraTransform\.position \+ cameraForward\.normalized \* 1\.5f/);
  assert.match(flatPanel, /transform\.SetPositionAndRotation\(worldPosition, _orientationBaseRotation\)/);
  assert.match(flatPanel, /var angle = _rotated \? 90f : 0f/);
  assert.match(flatPanel, /Quaternion\.AngleAxis\(angle/);
  assert.doesNotMatch(flatPanel, /transform\.Rotate\(/);
  assert.match(mediaRenderer, /_sphere\.transform\.SetParent\(transform\.parent, true\)/);
  assert.doesNotMatch(flatPanel, /SetParent\(xrCamera\.transform/);
});

test("Flat controls require active media mode and metadata yields until a manual profile override", () => {
  assert.match(mediaUi, /private bool _manualProfileOverride/);
  assert.doesNotMatch(mediaUi, /_profileInitialized/);
  assert.match(mediaUi, /if \(!_manualProfileOverride\)[\s\S]*MediaVideoProfile\.From\(item\)/);
  assert.match(mediaUi, /ApplySelectedProfile\(true\)/);
  assert.match(mediaUi, /_playback\.IsMediaMode[\s\S]*_playback\.Profile\.projection == ProjectionMode\.Flat/);
  assert.match(mediaUi, /button\.gameObject\.SetActive\(active\)/);
});

test("Android media server advertises a persistent UUID through NSD for its current port", () => {
  assert.match(androidNsd, /NsdManager/);
  assert.match(androidNsd, /SERVICE_TYPE = "_qps-media\._tcp\."/);
  for (const attribute of ["v", "id", "name", "caps"])
    assert.match(androidNsd, new RegExp(`setAttribute\\("${attribute}"`));
  assert.match(androidNsd, /setAttribute\("v", "1"\)/);
  assert.match(androidNsd, /setAttribute\("caps", "media"\)/);
  assert.match(androidNsd, /port = portProvider\(\)/);
  assert.match(androidIdentity, /UUID\.randomUUID\(\)/);
  assert.match(androidIdentity, /getSharedPreferences/);
  assert.doesNotMatch(androidIdentity, /MAC|macAddress|NetworkInterface/);
  assert.match(androidMediaServer, /MediaNsdRegistration\(context\) \{ port \}/);
  assert.match(androidMediaServer, /nsdRegistration\.start\(\)/);
  assert.match(androidMediaServer, /nsdRegistration\.stop\(\)/);
});

test("Quest NSD discovery deduplicates by device id, resolves services, handles loss and brackets IPv6 URLs", () => {
  assert.match(mediaDiscovery, /ServiceType = "_qps-media\._tcp\."/);
  assert.match(mediaDiscovery, /base\("android\.net\.nsd\.NsdManager\$DiscoveryListener"\)/);
  assert.match(mediaDiscovery, /base\("android\.net\.nsd\.NsdManager\$ResolveListener"\)/);
  assert.match(mediaDiscovery, /Dictionary<string, MediaDeviceInfo>/);
  assert.match(mediaDiscovery, /_devices\[deviceId\]/);
  assert.match(mediaDiscovery, /HasReadyDevice/);
  assert.match(mediaDiscovery, /device\.IsReady = false/);
  assert.match(mediaDiscovery, /normalizedHost\.IndexOf\(/);
  assert.match(mediaDiscovery, /normalizedHost = "\[" \+ normalizedHost\.Trim\('\[', '\]'\) \+ "\]"/);
  assert.match(mediaDiscovery, /TryGetReadyDevice/);
  assert.match(mediaDiscovery, /capabilities != "media"/);
  assert.match(read(scripts + "QuestWebRtcReceiver.cs"), /MediaDeviceDiscovery mediaDiscovery/);
  assert.match(read(scripts + "QuestWebRtcReceiver.cs"), /SelectMediaDevice\(string deviceId\)/);
  assert.match(read(scripts + "QuestWebRtcReceiver.cs"), /_settingsUI\.SetMediaBaseUrl\(device\.BaseUrl\)/);
  assert.match(settings, /public MediaCatalogClient mediaCatalogClient/);
  assert.match(settings, /mediaCatalogClient\.baseUrl = normalized/);
  assert.match(home, /Media phones/);
  assert.match(home, /device\.IsReady \? "Ready" : "Lost"/);
  assert.match(home, /button\.interactable = device\.IsReady/);
  assert.match(home, /OnMediaDeviceSelected/);
});

test("UX navigation and readiness states have explicit recovery paths", () => {
  assert.match(receiver, /public void ShowHome\(\)/);
  assert.match(factory, /CreateBackButton\(panel/);
  assert.match(factory, /_settingsUI\.BackToHome\(\)/);
  assert.match(ui, /if \(\(state == ConnectionState\.PeerConnected \|\| state == ConnectionState\.MediaConnected\) && IsVisible\)[\s\S]*BackToHome\(\)/);
  assert.match(home, /_receiver\.IsPeerConnected \? "Connected"/);
  assert.match(home, /_receiver\.IsMediaReady/);
  assert.match(home, /_receiver\.IsMediaStale/);
  assert.match(home, /_receiver\.IsMediaChecking/);
  assert.match(home, /_receiver\.IsMediaFailed/);
  assert.match(home, /_receiver\.ProbeMedia\(\)/);
  assert.match(home, /_receiver\.IsControlConnected/);
  assert.match(home, /_keyboardButton\.interactable = controlReady/);
  assert.match(home, /Connect phone to use Keyboard/);
  assert.match(receiver, /public bool IsMediaStale/);
  assert.match(receiver, /MediaProbeTtlSeconds/);
  assert.match(receiver, /public void ProbeMedia\(\)/);
  assert.match(mediaUi, /public void ProbeAvailability\(\)/);
  const android = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MainActivity.kt");
  assert.match(android, /MEDIA MANAGER/);
  assert.match(android, /private fun showMediaManager\(\)/);
  assert.match(android, /homeScreenStatusView\.text = if \(isStreaming\) "Active" else "Off"/);
  assert.match(android, /homeScreenActionButton\.text = if \(isStreaming\)/);
  assert.match(android, /statusRow\(homeCard, "Signaling",/);
  assert.match(android, /ConnectionState\.CONNECTED -> "Ready"/);
});
