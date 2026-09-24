import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const transport = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialTelemetry.cs");
assert(transport.includes("private bool _terminal") && transport.includes("HasChannel => _channel != null && !_terminal"),
  "Spatial DataChannel terminal state must make the channel recreatable");
assert(transport.includes("MarkTerminal(attached)"), "Spatial DataChannel close/error terminal handling missing");

const anchors = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialAnchorService.cs");
assert(anchors.includes("dataPlane.ReliableOpenStateChanged += OnReliableOpenChanged"),
  "Anchor service must observe reliable channel lifecycle");
assert(anchors.includes("_anchors.Clear();") && anchors.includes("private void OnNegotiationInvalidated()"),
  "Session-local anchors must be cleared when negotiation/session is invalidated");
assert(anchors.includes('BroadcastTo(subscription, "snapshot"') && anchors.includes("if (dataPlane.IsReliableOpen)"),
  "Anchor snapshots must wait for an open reliable channel");

const depth = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestEnvironmentDepthService.cs");
assert(depth.includes("startNeeded && !StartDepth()"), "Depth subscription must activate the provider on first subscriber");
assert(depth.includes("if (_subscriptions.Count == 0) StopDepth()"), "Depth provider must stop when the last subscriber leaves");
assert(depth.includes("dataPlane.OpenStateChanged += OnFastOpenChanged") && depth.includes("_provider?.StopDepth();"),
  "Depth provider/subscriptions must reset when the fast data plane closes");
assert(depth.includes("sequence = subscription.nextSequence++"), "Depth samples must maintain per-subscription sequence state");

for (const path of [
  "apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialTelemetryService.cs",
  "apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialHandTrackingService.cs",
  "apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialObjectInteractionService.cs"
]) {
  const source = read(path);
  assert(source.includes("OpenStateChanged") && source.includes("_subscriptions.Clear()"),
    `${path} must clear subscriptions when its realtime transport is invalidated`);
}

const mac = read("apps/macos-sender/src/renderer.ts");
for (const label of ["spatial-fast", "spatial", "spatial-reliable"])
  assert(mac.includes(`\"${label}\"`), `macOS Spatial consumer missing ${label}`);
assert(mac.includes('capability: "spatial.anchor"') && mac.includes('reliability: "reliable_ordered"'),
  "macOS must consume session anchors over the reliable Spatial channel");
assert(mac.includes("clearSubscriptionsForTransport") && mac.includes("isCapabilityTransportOpen"),
  "macOS must isolate fast/reliable subscription lifecycle");

const splat = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/GaussianSplatPocRenderer.cs");
assert(splat.includes("DefaultMaxDownloadBytes = 32 * 1024 * 1024") && splat.includes("request.downloadedBytes > byteLimit"),
  "3DGS POC must bound network input before parsing");
assert(splat.includes("Encoding.UTF8.GetByteCount(text)"), "Inline 3DGS input must also enforce the byte limit");
const graphics = read("apps/quest-unity-client/ProjectSettings/GraphicsSettings.asset");
assert(graphics.includes("6f84aaf1fe0b4aefb975a4c21c83d165"),
  "GaussianSplatPoc shader must be retained in Quest player builds");

const media = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/MediaItemDto.cs");
assert(media.includes("UriKind.Absolute") && !media.includes("manifestUrl.TrimStart('/')"),
  "Relative spatial manifests must fall back instead of resolving to an unserved Android /assets path");
const mediaTests = read("apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/SpatialMediaRoutingTests.cs");
assert(mediaTests.includes("RelativeManifestFallsBackToAuthorizedContent"), "Spatial manifest fallback regression test missing");

console.log("P3 lifecycle regression source checks passed");
