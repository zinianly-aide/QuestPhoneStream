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
for (const name of ["display.consume", "display.control", "media.consume", "media.render", "xr.head.pose", "xr.controller.pose"])
  assert(questRegistry.includes(`"${name}"`), `Quest registry missing ${name}`);
for (const forbidden of ["camera.", "ai.", "xr.hand", "hand.pose"])
  assert(!questRegistry.includes(`"${forbidden}`), `Quest registry must not advertise unimplemented ${forbidden}`);
assert(questRegistry.includes('"xr.head.pose", true, true, false, new[] { "local" }'), "XR head pose must stay local until a data transport exists");
assert(questRegistry.includes('"xr.controller.pose", true, true, false, new[] { "local" }'), "XR controller pose must stay local until a data transport exists");

const nsd = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaNsdRegistration.kt");
assert(nsd.includes("_qps-device._tcp."), "Unified NSD service missing");
assert(nsd.includes("_qps-media._tcp."), "Legacy NSD fallback missing");
assert(nsd.includes('Advertisement(UNIFIED_SERVICE_TYPE, "media,screen,control")'), "Legacy caps bootstrap changed unexpectedly");
assert(nsd.includes('setAttribute("capv", "1")') && nsd.includes('setAttribute("spatial", "1")'), "Capability bootstrap version attributes missing");
assert(nsd.includes("scheduleRetry(type)"), "NSD registrations must fail and retry independently");

const serverProtocol = read("apps/signaling-server/src/protocol.ts");
const serverIndex = read("apps/signaling-server/src/index.ts");
assert(serverProtocol.includes("isSpatialMessageType(parsed.type)"), "Signaling parser does not recognize Spatial envelopes");
assert(serverIndex.includes("message.source !== sender.deviceId"), "Spatial source identity is not bound to the registered socket");
assert(serverIndex.includes("!isSpatialEnvelope(message) && message.token !== token"), "Legacy token must not become a Spatial envelope credential");

const questSignaling = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestSignalingClient.cs");
const bootstrapCall = questSignaling.indexOf('await SendSpatialBootstrapAsync(epoch);');
const sessionIndex = questSignaling.indexOf('type = "create_session"');
const helloIndex = questSignaling.indexOf('SpatialWire.Create("device.hello"');
const getIndex = questSignaling.indexOf('SpatialWire.Create("device.capabilities.get"');
assert(bootstrapCall >= 0 && sessionIndex > bootstrapCall, "Quest must bootstrap Spatial discovery before the legacy session request");
assert(helloIndex >= 0 && getIndex > helloIndex, "Spatial bootstrap must send hello before capabilities.get");
assert(questSignaling.includes('case "device.capabilities.changed"'), "Quest does not handle runtime capability changes");

console.log("Spatial Protocol v1 source checks passed");
