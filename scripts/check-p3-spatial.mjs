import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const pathOf = path => resolve(root, path);
const read = path => readFileSync(pathOf(path), "utf8");
const exists = path => existsSync(pathOf(path));
const assert = (condition, message) => { if (!condition) throw new Error(message); };

// Latest local-media foundation must be present in the P3 tree.
const appliedConfig = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/AppliedConfig.kt");
const mediaServer = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaHttpServer.kt");
const signaling = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestSignalingClient.cs");
assert(appliedConfig.includes("AppliedConfigStore") && appliedConfig.includes("fun apply("), "Latest Android Save/Apply foundation missing");
assert(mediaServer.includes("AppliedConfigStore.apply") && mediaServer.includes("DeviceControlPlane.configure(next.signalingUrl"), "Applied config is not the single control-plane update path");
assert(signaling.includes("SubscriptionCreateRequested") && signaling.includes("SubscriptionCancelRequested") && signaling.includes("SendSubscriptionClosedAsync"), "Latest Spatial subscription lifecycle foundation missing");
assert(exists("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/DeveloperHud.cs") && exists("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestDiagnostics.cs"), "Latest diagnostics foundation missing");

// P3 capability boundaries: no new capability surface, no false provider claims.
const registry = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/CapabilityRegistry.cs");
for (const capability of ["spatial.anchor", "spatial.environment.depth", "spatial.object.interaction", "media.6dof.render", "media.gaussian-splat.render"])
  assert(registry.includes(`\"${capability}\"`), `P3 registry missing ${capability}`);
assert(registry.includes('"session-local"') && !registry.includes('"persistent-anchor"'), "Anchors must remain session-local");
assert(registry.includes('Descriptor("spatial.environment.depth", false, false, false'), "Depth must start unavailable without a runtime provider");
assert(registry.includes('Descriptor("media.6dof.render", false, false, false'), "6DoF must start unavailable without a decoder provider");
assert(registry.includes('"ascii-ply-poc"') && registry.includes('"isotropic"') && registry.includes('"max-50000"'), "3DGS capability must expose explicit POC limits");

// Physical RTCDataChannel configuration must match Spatial subscription declarations.
const channelFactory = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialRtcChannelFactory.cs");
const dataPlane = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialDataPlaneHub.cs");
assert(channelFactory.includes('FastLabel = "spatial-fast"') && channelFactory.includes("ordered = false") && channelFactory.includes("maxRetransmits = 0"), "spatial-fast must be unreliable/unordered");
assert(channelFactory.includes('ReliableLabel = "spatial-reliable"') && channelFactory.includes("ordered = true"), "spatial-reliable must be reliable/ordered");
assert(dataPlane.includes("EnsureFastChannel") && dataPlane.includes("EnsureReliableChannel") && dataPlane.includes("TrySendReliableJson"), "Fast/reliable data planes are not separated");

const anchors = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialAnchorService.cs");
assert(anchors.includes("persistent = false"), "P3 anchors must remain explicitly session-local");
assert(anchors.includes("EnsureReliableChannel") && anchors.includes("IsReliableOpen") && anchors.includes("TrySendReliableJson"), "Anchor lifecycle must use the reliable Spatial channel");
assert(anchors.includes('"reliable_ordered"') && anchors.includes('"snapshot"') && anchors.includes('"created"') && anchors.includes('"updated"') && anchors.includes('"removed"'), "Anchor declaration/lifecycle is incomplete");

// Optional provider discovery is centralized, bounded and cached.
const discovery = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/OptionalProviderDiscovery.cs");
const camera = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestVisionService.cs");
const depth = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestEnvironmentDepthService.cs");
const sixDof = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SixDofMediaService.cs");
assert(discovery.includes("DefaultMaxAttempts = 3") && discovery.includes("DefaultRetrySeconds = 5f") && discovery.includes("entry.attempts"), "Provider discovery must have bounded miss retry/backoff");
assert(discovery.includes("AppDomain.CurrentDomain.GetAssemblies()") && discovery.includes("assembly.GetTypes()"), "Shared provider discovery scan missing");
for (const [name, source] of [["camera", camera], ["depth", depth], ["6DoF", sixDof]]) {
  assert(source.includes("OptionalProviderDiscovery"), `${name} does not use shared provider discovery`);
  assert(!source.includes("AppDomain.CurrentDomain.GetAssemblies()") && !source.includes("assembly.GetTypes()"), `${name} still performs its own reflection scan`);
}
assert(sixDof.includes("providerRefreshSeconds") && !sixDof.includes("private void Update() => _provider?.Refresh()"), "6DoF provider discovery must not scan every frame");
assert(sixDof.includes("external provider required") && !sixDof.includes("VideoPlayer"), "6DoF POC must require an external decoder and never use VideoPlayer");

// Environment depth remains metadata-only on the network.
assert(depth.includes('format = "metadata-only"') && depth.includes("DepthTexture"), "Depth metadata/local texture contract missing");
assert(depth.includes("TrySendFastJson(packet.ToJson()") && !depth.includes("EncodeToJPG") && !depth.includes("EncodeToPNG"), "Depth texture must not be serialized into network JSON");

// Android spatial media metadata must round-trip through catalog + item metadata APIs.
const androidItem = read("apps/android-agent/app/src/main/java/com/questphonestream/agent/MediaItem.kt");
for (const field of ["spatialFormat", "manifestUrl", "referenceSpace", "spatialBounds"])
  assert(androidItem.includes(field), `Android MediaItem missing ${field}`);
for (const field of ["spatialFormat", "manifestUrl", "referenceSpace", "spatialBounds"])
  assert(mediaServer.includes(`put(\"${field}\"`), `Android media API missing ${field}`);
assert(mediaServer.includes("sendCatalog") && mediaServer.includes("sendMetadata") && mediaServer.includes("metadataJson(item)"), "Catalog and item metadata must share the same serializer");

const mediaDto = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/MediaItemDto.cs");
const mediaUi = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/MediaLibraryUI.cs");
assert(mediaDto.includes("MediaRouteKind") && mediaDto.includes("GaussianSplat") && mediaDto.includes("SixDof"), "Spatial media route classifier missing");
assert(mediaUi.includes("case MediaRouteKind.SixDof") && mediaUi.includes("_sixDof.TryPlay") && mediaUi.includes("case MediaRouteKind.GaussianSplat") && mediaUi.includes("_splat.LoadUrl"), "MediaLibrary spatial routing missing");
assert(mediaUi.includes("_playback?.PlayUrl(sourceUrl, profile)") && mediaUi.indexOf("_playback?.PlayUrl(sourceUrl, profile)") > mediaUi.indexOf("default:"), "Normal VideoPlayer route must remain isolated to default video path");

// 3DGS remains a bounded POC with safe load state/cancellation.
const splats = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/GaussianSplatPocRenderer.cs");
assert(splats.includes("DefaultMaxSplats = 50000") && splats.includes("GaussianSplatLoadState"), "3DGS POC bounds/state missing");
assert(splats.includes("CancelLoad") && splats.includes("TryValidateSource") && splats.includes("ClearAsset") && splats.includes("LastError"), "3DGS load cancellation/validation/error state missing");
assert(splats.includes('StartsWith("format ascii"') && splats.includes("isotropic billboard"), "3DGS POC must remain ASCII/isotropic only");

// Object interaction is opt-in and cleans destroyed/disabled targets/subscribers.
const interaction = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SpatialObjectInteractionService.cs");
assert(interaction.includes("SpatialInteractable") && interaction.includes("Physics.Raycast") && interaction.includes("remoteEnabled"), "Object interaction must remain opt-in collider/raycast based");
assert(interaction.includes("RemoteTargetRemoved") && interaction.includes("RemoveTarget") && interaction.includes("select.end") && interaction.includes("hover.exit"), "Destroyed target hover/select cleanup missing");
assert(interaction.includes("_subscriptions.Clear()") && interaction.includes("_tracker.Reset()"), "Interaction subscriber/state cleanup missing");

// One DeveloperHud only; P2/P3 metrics live in QuestDiagnosticsSnapshot.
assert(!exists("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestDeveloperHud.cs"), "Legacy QuestDeveloperHud must be removed");
const hud = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/DeveloperHud.cs");
const diagnostics = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestDiagnostics.cs");
const settingsFactory = read("apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/SettingsUIFactory.cs");
assert(!hud.includes("QuestDeveloperHud") && !settingsFactory.includes("QuestDeveloperHud"), "Developer HUD still creates/depends on the legacy adapter");
for (const label of ["Spatial Data Plane", "Vision / AI", "Anchors:", "Depth:", "Interaction:", "6DoF:", "3DGS:"])
  assert(diagnostics.includes(label), `QuestDiagnostics missing ${label}`);
for (const metric of ["spatialFastOpen", "spatialReliableOpen", "anchorCount", "depthState", "interactionState", "sixDofState", "gaussianSplatCount", "gaussianLoadMs"])
  assert(diagnostics.includes(metric), `QuestDiagnostics missing metric ${metric}`);

console.log("P3 closeout source checks passed");
