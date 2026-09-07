import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const schema = name => JSON.parse(read(`protocol/spatial/${name}.schema.json`));
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const envelope = schema("envelope");
const requiredEnvelope = ["v", "id", "type", "source", "target", "sessionId", "streamId", "correlationId", "timestamp", "payload"];
assert(JSON.stringify(envelope.required) === JSON.stringify(requiredEnvelope), "Spatial envelope required fields drifted");
for (const type of [
  "device.hello", "device.capabilities.get", "device.capabilities.result", "device.capabilities.changed",
  "subscription.create", "subscription.created", "subscription.cancel", "subscription.closed", "protocol.error",
]) assert(envelope.properties.type.enum.includes(type), `Missing Spatial message type ${type}`);
assert(envelope.additionalProperties === true, "Unknown envelope fields must remain forward-compatible");

const capability = schema("capability");
for (const field of ["name", "version", "state", "transports", "features", "limits", "permissions"])
  assert(capability.required.includes(field), `Capability descriptor missing ${field}`);
for (const state of ["available", "authorized", "active"])
  assert(capability.properties.state.required.includes(state), `Capability state missing ${state}`);
for (const namespace of ["display", "media", "xr", "camera", "audio", "spatial", "ai", "input"])
  assert(capability.properties.name.pattern.includes(namespace), `Capability vocabulary missing ${namespace}.*`);

const subscription = schema("subscription");
for (const field of ["rateHz", "format", "transport", "reliability"])
  assert(subscription.required.includes(field), `Subscription missing ${field}`);
assert(!subscription.properties.transport.enum.includes("signaling.websocket"), "High-rate subscription data must not use signaling JSON");

const spatial = schema("spatial");
for (const field of ["space", "timestamp", "position", "orientation"])
  assert(spatial.$defs.SpatialPose.required.includes(field), `SpatialPose missing ${field}`);
for (const field of ["spaceFrom", "spaceTo", "timestamp", "translation", "rotation"])
  assert(spatial.$defs.SpatialTransform.required.includes(field), `SpatialTransform missing ${field}`);

const androidRegistry = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/CapabilityRegistry.kt");
for (const name of ["display.publish", "display.control", "media.list", "media.open", "media.publish"])
  assert(androidRegistry.includes(`name = "${name}"`), `Android registry missing ${name}`);
for (const forbidden of ["camera.", "ai.", "xr.hand", "hand.pose"])
  assert(!androidRegistry.includes(`name = "${forbidden}`), `Android registry must not advertise unimplemented ${forbidden}`);

const questRegistry = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/CapabilityRegistry.cs");
for (const name of [
  "display.consume", "display.control", "media.consume", "media.render",
  "xr.head.pose", "xr.controller.pose", "xr.hand.pose", "camera.rgb", "ai.vision"
]) assert(questRegistry.includes(`"${name}"`), `Quest registry missing ${name}`);
for (const forbidden of ["spatial.anchor", "environment.depth", "video.6dof", "gaussian.splatting"])
  assert(!questRegistry.includes(`"${forbidden}"`), `P3 capability leaked into P2 registry: ${forbidden}`);
assert(questRegistry.includes('"xr.head.pose", true, true, false, new[] { "local", "webrtc.datachannel" }'), "XR head pose must expose the Spatial DataChannel transport");
assert(questRegistry.includes('"xr.controller.pose", true, true, false, new[] { "local", "webrtc.datachannel" }'), "XR controller pose must expose the Spatial DataChannel transport");
assert(questRegistry.includes('"xr.hand.pose", false, false, false, new[] { "local", "webrtc.datachannel" }'), "Hand capability must start unavailable until the runtime subsystem exists");
assert(questRegistry.includes('"camera.rgb", false, false, false'), "Camera capability must start permission-gated and unavailable until a provider exists");
assert(questRegistry.includes('"ai.vision", true, false, false'), "AI capability must keep endpoint authorization separate from camera permission");

const nsd = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaNsdRegistration.kt");
assert(nsd.includes("_qps-device._tcp."), "Unified NSD service missing");
assert(nsd.includes("_qps-media._tcp."), "Legacy NSD fallback missing");
assert(nsd.includes('Advertisement(UNIFIED_SERVICE_TYPE, "media,screen,control")'), "Unified caps bootstrap changed unexpectedly");
assert(nsd.includes('setAttribute("capv", "1")'), "Capability bootstrap version attribute missing");
assert(nsd.includes('if (spatialReadyProvider()) setAttribute("spatial", "1")'), "Spatial readiness must not be advertised before control-plane registration");
assert(nsd.includes("refreshUnifiedAdvertisement()") && nsd.includes("requestRefresh(UNIFIED_SERVICE_TYPE)"), "Unified NSD metadata cannot be refreshed safely");
assert(nsd.includes("scheduleRetry(type,") && nsd.includes("registeredTypes.contains(type)"), "NSD registrations must fail and retry independently");
assert(nsd.includes("refreshUnregisterPendingTypes") && nsd.includes("onServiceUnregistered"), "Unified NSD refresh must serialize unregister before re-register");

const controlPlane = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/DeviceControlPlane.kt");
const mediaServer = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaHttpServer.kt");
const screenService = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/ScreenStreamService.kt");
assert(controlPlane.includes("object DeviceControlPlane : StreamSignaling"), "Android device-level Spatial control plane missing");
assert(mediaServer.includes("DeviceControlPlane.acquire(DeviceControlPlane.Owner.MEDIA)"), "Spatial control plane is still tied to MediaProjection lifecycle");
assert(mediaServer.includes("CONFIG_STABLE_MS") && mediaServer.includes("refreshUnifiedAdvertisement"), "NSD signaling metadata changes are not debounced/refreshed");
assert(!screenService.includes("SignalingClient("), "Screen service must not create a second signaling client");
assert(screenService.includes("DeviceControlPlane.release(DeviceControlPlane.Owner.STREAM)"), "Screen lifecycle must release only its control-plane ownership");

const controlCommand = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/ControlCommand.kt");
const streamer = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/WebRtcStreamer.kt");
assert(controlCommand.includes("DeviceControlPlane.setControlAuthorized(true)") && controlCommand.includes("DeviceControlPlane.setControlAuthorized(false)"),
  "Accessibility authorization is not wired to display.control");
assert(streamer.includes("DeviceControlPlane.setControlTransportActive(open)") && streamer.includes("DeviceControlPlane.setControlTransportActive(false)"),
  "Android DataChannel state is not wired to display.control.active");

const serverProtocol = read("apps/signaling-server/src/protocol.ts");
const serverIndex = read("apps/signaling-server/src/index.ts");
assert(serverProtocol.includes("isSpatialMessageType(parsed.type)"), "Signaling parser does not recognize Spatial envelopes");
assert(serverIndex.includes("message.source !== sender.deviceId"), "Spatial source identity is not bound to the registered socket");
assert(serverIndex.includes("!isSpatialEnvelope(message) && message.token !== token"), "Legacy token must not become a Spatial envelope credential");

const questSignaling = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestSignalingClient.cs");
const controlChannel = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/ControlChannel.cs");
const questReceiver = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestWebRtcReceiver.cs");
const bootstrapCall = questSignaling.indexOf('await SendSpatialBootstrapAsync(epoch);');
const sessionIndex = questSignaling.indexOf('type = "create_session"');
const helloIndex = questSignaling.indexOf('SpatialWire.Create("device.hello"');
const getIndex = questSignaling.indexOf('SpatialWire.Create("device.capabilities.get"');
assert(bootstrapCall >= 0 && sessionIndex > bootstrapCall, "Quest must bootstrap Spatial discovery before the legacy session request");
assert(helloIndex >= 0 && getIndex > helloIndex, "Spatial bootstrap must send hello before capabilities.get");
assert(questSignaling.includes('case "device.capabilities.changed"'), "Quest does not handle runtime capability changes");
assert(questSignaling.includes("source != _activeAndroid"), "Quest Spatial messages are not isolated to the selected Android peer");
assert(questSignaling.includes('message.type == "protocol.error" && source == "signaling"'), "Quest peer isolation must preserve signaling-server protocol errors");
assert(questSignaling.includes("PeerCapabilitiesChanged?.Invoke(source, capabilities)"), "Quest capability change events must preserve source device identity");
assert(controlChannel.includes('ReportCapabilityState("display.control", active: true)') && controlChannel.includes('ReportCapabilityState("display.control", active: false)'),
  "Quest DataChannel state is not wired to display.control.active");
assert(questReceiver.includes('CreateDataChannel("spatial"') && !questReceiver.includes('!IsControlConnected) return null'),
  "Spatial DataChannel must be independent from the legacy control channel");

const telemetry = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialTelemetryService.cs");
const telemetryWire = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialTelemetry.cs");
assert(telemetry.includes('payload.capability == "xr.head.pose"') || telemetry.includes('payload.capability != "xr.head.pose"'), "Head telemetry subscription missing");
assert(telemetry.includes('"xr.controller.pose"'), "Controller telemetry subscription missing");
assert(telemetry.includes("NormalizeRate") && telemetry.includes("webrtc.datachannel"), "XR telemetry rate/transport negotiation missing");
assert(telemetryWire.includes("SpatialSequenceGate") && telemetryWire.includes("sequence <= previous"), "Stale telemetry sequence handling missing");
assert(telemetryWire.includes("ToCanonicalPosition") && telemetryWire.includes("ToCanonicalRotation"), "Canonical coordinate conversion missing");

const vision = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestVisionService.cs");
assert(vision.includes("horizonos.permission.HEADSET_CAMERA") && vision.includes("QuestVisionPermissionGate"), "Camera permission gating missing");
assert(vision.includes("CaptureSingleFrame") && vision.includes("SetSampledPreview"), "Camera single-frame/sample preview missing");

const ai = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestAiClient.cs");
assert(ai.includes("QuestAiResponseParser") && ai.includes("AiVisionResult"), "Structured AI response parser missing");
assert(ai.includes("LastLatencyMs") && ai.includes("endpointUrl") && ai.includes("model"), "AI endpoint abstraction/latency missing");

const hand = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/HandTrackingProvider.cs");
const handService = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialHandTrackingService.cs");
assert(hand.includes("XRHandSubsystem") && hand.includes("JointIds") && hand.includes("XRHandJointID.Wrist"), "OpenXR hand provider missing");
assert(handService.includes('"xr.hand.pose"') && handService.includes("qps.spatial.hand+json"), "Hand Spatial subscription missing");

const macSender = read("apps/macos-sender/src/renderer.ts");
assert(macSender.includes('createDataChannel("spatial-bootstrap"') && macSender.includes("current.ondatachannel"), "Mac must negotiate SCTP without advertising display.control");
assert(macSender.includes('"subscription.create"') && macSender.includes('"xr.head.pose"') && macSender.includes('"xr.controller.pose"'), "Mac telemetry subscription consumer missing");
assert(macSender.includes("sequence <= previous") && macSender.includes("telemetryDropped"), "Mac stale telemetry handling missing");

const hud = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestDeveloperHud.cs");
for (const metric of ["PoseStreamHz", "DroppedFrames", "LastSequence", "CameraState", "LastLatencyMs", "HandTrackingState"])
  assert(hud.includes(metric), `Developer HUD missing ${metric}`);

console.log("Spatial Protocol v1 + P2 source checks passed");
