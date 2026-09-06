import {
  displayPublishCapability,
  helloPayload,
  spatialEnvelope,
  type SenderConfig,
  type SpatialCapabilityDescriptor,
  type SpatialEnvelope
} from "./protocol";
import { SpatialSubscriptionTracker } from "./subscriptions";

declare global {
  interface Window {
    qps: {
      getConfig(): Promise<SenderConfig>;
      listSources(): Promise<Array<{ id: string; name: string; thumbnail: string }>>;
      setSpatialReady(ready: boolean): void;
    };
  }
}

interface SessionMessage {
  sessionId: string;
  androidDeviceId: string;
  questDeviceId: string;
  negotiationId?: string;
}

interface SubscriptionSpec {
  capability: string;
  rateHz: number;
  format: string;
  reliability: "unreliable_unordered" | "reliable_ordered";
}

let config: SenderConfig;
let socket: WebSocket | null = null;
let reconnectTimer: number | null = null;
let heartbeatTimer: number | null = null;
let subscriptionRetryTimer: number | null = null;
let stream: MediaStream | null = null;
let peer: RTCPeerConnection | null = null;
let fastSpatialChannel: RTCDataChannel | null = null;
let compatSpatialChannel: RTCDataChannel | null = null;
let reliableSpatialChannel: RTCDataChannel | null = null;
let activeSession: SessionMessage | null = null;
let remoteReady = false;
let authorized = false;
let active = false;
let pendingIce: RTCIceCandidateInit[] = [];
let lastCapability: SpatialCapabilityDescriptor | null = null;
let questCapabilities: SpatialCapabilityDescriptor[] = [];
const subscriptions = new SpatialSubscriptionTracker();
const lastSequenceByStream = new Map<string, number>();
let telemetryDropped = 0;
let telemetryLastSequence = -1;

const subscriptionSpecs: SubscriptionSpec[] = [
  { capability: "xr.head.pose", rateHz: 60, format: "qps.spatial.json", reliability: "unreliable_unordered" },
  { capability: "xr.controller.pose", rateHz: 60, format: "qps.spatial.json", reliability: "unreliable_unordered" },
  { capability: "xr.hand.pose", rateHz: 60, format: "qps.spatial.hand+json", reliability: "unreliable_unordered" },
  { capability: "spatial.anchor", rateHz: 1, format: "qps.spatial.anchor+json", reliability: "reliable_ordered" }
];

const status = () => document.getElementById("status") as HTMLElement;
const sourceList = () => document.getElementById("sources") as HTMLSelectElement;
const startButton = () => document.getElementById("start") as HTMLButtonElement;
const stopButton = () => document.getElementById("stop") as HTMLButtonElement;

function setStatus(text: string): void { status().textContent = text; }
function send(payload: unknown): void { if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(payload)); }
function capability(): SpatialCapabilityDescriptor { return displayPublishCapability(authorized, active); }

function publishCapabilityChanged(): void {
  const next = capability();
  if (lastCapability && JSON.stringify(lastCapability.state) === JSON.stringify(next.state)) return;
  lastCapability = next;
  send(spatialEnvelope("device.capabilities.changed", config, config.questDeviceId,
    { capabilities: [next] }, "", activeSession?.sessionId ?? ""));
}

function setRuntimeState(nextAuthorized: boolean, nextActive: boolean): void {
  authorized = nextAuthorized;
  active = nextAuthorized && nextActive;
  publishCapabilityChanged();
}

function connectSignaling(): void {
  if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) return;
  setStatus("Connecting signaling…");
  socket = new WebSocket(config.signalingUrl);
  socket.onopen = () => send({ type: "register", token: config.token, role: "android", deviceId: config.deviceId });
  socket.onmessage = event => {
    let message: any;
    try { message = JSON.parse(String(event.data)); } catch { return; }
    if (typeof message?.type !== "string") return;
    if (message.type.includes(".")) handleSpatial(message as SpatialEnvelope);
    else handleLegacy(message);
  };
  socket.onclose = () => {
    window.qps.setSpatialReady(false);
    clearHeartbeat();
    setRuntimeState(authorized, false);
    closePeer();
    setStatus("Signaling disconnected; retrying…");
    reconnectTimer = window.setTimeout(connectSignaling, 2000);
  };
  socket.onerror = () => setStatus("Signaling error");
}

function handleLegacy(message: any): void {
  switch (message.type) {
    case "registered":
      if (message.deviceId !== config.deviceId) return;
      window.qps.setSpatialReady(true);
      startHeartbeat();
      send(spatialEnvelope("device.hello", config, config.questDeviceId, helloPayload(config)));
      send(spatialEnvelope("device.capabilities.get", config, config.questDeviceId, {}));
      setStatus(stream ? "Ready · screen capture active" : "Ready · choose a screen");
      return;
    case "session_created":
      if (message.androidDeviceId !== config.deviceId || message.questDeviceId !== config.questDeviceId) return;
      activeSession = message as SessionMessage;
      if (stream) void createPeer(activeSession);
      return;
    case "answer":
      if (!matchesSession(message) || !peer || typeof message.sdp !== "string") return;
      void peer.setRemoteDescription({ type: "answer", sdp: message.sdp }).then(async () => {
        remoteReady = true;
        const queued = pendingIce.splice(0);
        for (const candidate of queued) await peer?.addIceCandidate(candidate);
      });
      return;
    case "ice":
      if (!matchesSession(message) || !message.candidate) return;
      if (!remoteReady) pendingIce.push(message.candidate as RTCIceCandidateInit);
      else void peer?.addIceCandidate(message.candidate as RTCIceCandidateInit);
      return;
    case "peer_unavailable":
    case "error":
      if (!message.sessionId || message.sessionId === activeSession?.sessionId) {
        closePeer();
        activeSession = null;
      }
      return;
  }
}

function handleSpatial(message: SpatialEnvelope): void {
  if (message.target !== config.deviceId || (message.source !== config.questDeviceId && message.source !== "signaling")) return;
  if (message.type === "device.hello") {
    const payload = message.payload as any;
    const offered = Array.isArray(payload.supportedVersions) ? payload.supportedVersions : [];
    const selected = payload.selectedVersion === "1.0" || offered.includes("1.0") ? "1.0" : null;
    if (!selected) {
      send(spatialEnvelope("protocol.error", config, message.source, {
        code: "unsupported_version", message: "No compatible Spatial Protocol version", retryable: false
      }, message.id));
      return;
    }
    if (!payload.selectedVersion) send(spatialEnvelope("device.hello", config, message.source, helloPayload(config, selected), message.id));
    return;
  }
  if (message.type === "device.capabilities.get") {
    send(spatialEnvelope("device.capabilities.result", config, message.source,
      { capabilities: [capability()] }, message.id, activeSession?.sessionId ?? ""));
    return;
  }
  if (message.type === "device.capabilities.result" || message.type === "device.capabilities.changed") {
    const capabilities = (message.payload as any)?.capabilities;
    if (Array.isArray(capabilities)) {
      questCapabilities = capabilities as SpatialCapabilityDescriptor[];
      requestTelemetrySubscriptions();
    }
    return;
  }
  if (message.type === "subscription.created") {
    const payload = message.payload as any;
    const created = subscriptions.markCreated(
      message.correlationId,
      String(payload?.subscriptionId ?? ""),
      String(payload?.capability ?? "")
    );
    if (created && isCapabilityTransportOpen(created.capability)) subscriptions.markActive(created.capability);
    return;
  }
  if (message.type === "subscription.closed") {
    const payload = message.payload as any;
    const capabilityName = subscriptions.close(
      String(payload?.subscriptionId ?? ""),
      String(payload?.capability ?? "")
    );
    if (capabilityName) scheduleSubscriptionRetry();
    return;
  }
  if (message.type === "protocol.error") {
    const payload = message.payload as any;
    const capabilityName = subscriptions.fail(message.correlationId);
    if (capabilityName && payload?.retryable === true) scheduleSubscriptionRetry();
    return;
  }
  if (message.type === "subscription.create" || message.type === "subscription.cancel") {
    send(spatialEnvelope("protocol.error", config, message.source, {
      code: "not_implemented", message: "macOS sender is a Spatial data consumer, not publisher", retryable: false
    }, message.id));
  }
}

function requestTelemetrySubscriptions(): void {
  if (peer?.connectionState !== "connected" || socket?.readyState !== WebSocket.OPEN) return;
  for (const spec of subscriptionSpecs) {
    const descriptor = questCapabilities.find(item => item.name === spec.capability);
    if (!descriptor?.state?.available || !descriptor.transports?.includes("webrtc.datachannel") || subscriptions.has(spec.capability)) continue;
    const request = spatialEnvelope("subscription.create", config, config.questDeviceId, {
      capability: spec.capability,
      rateHz: spec.rateHz,
      format: spec.format,
      transport: "webrtc.datachannel",
      reliability: spec.reliability
    }, "", activeSession?.sessionId ?? "");
    if (!subscriptions.begin(spec.capability, request.id)) continue;
    send(request);
  }
}

function scheduleSubscriptionRetry(): void {
  clearSubscriptionRetry();
  if (peer?.connectionState !== "connected" || socket?.readyState !== WebSocket.OPEN) return;
  subscriptionRetryTimer = window.setTimeout(() => {
    subscriptionRetryTimer = null;
    requestTelemetrySubscriptions();
  }, 500);
}

function clearSubscriptionRetry(): void {
  if (subscriptionRetryTimer != null) window.clearTimeout(subscriptionRetryTimer);
  subscriptionRetryTimer = null;
}

function isReliableCapability(capabilityName: string): boolean {
  return capabilityName === "spatial.anchor";
}

function isFastOpen(): boolean {
  return fastSpatialChannel?.readyState === "open" || compatSpatialChannel?.readyState === "open";
}

function isCapabilityTransportOpen(capabilityName: string): boolean {
  return isReliableCapability(capabilityName)
    ? reliableSpatialChannel?.readyState === "open"
    : isFastOpen();
}

function activateCreatedSubscriptions(): void {
  for (const state of subscriptions.snapshot()) {
    if (state.phase === "created" && isCapabilityTransportOpen(state.capability)) subscriptions.markActive(state.capability);
  }
}

function clearSubscriptionsForTransport(reliable: boolean): void {
  for (const state of subscriptions.snapshot()) {
    if (isReliableCapability(state.capability) === reliable) subscriptions.close(state.subscriptionId, state.capability);
  }
}

function attachSpatialChannel(channel: RTCDataChannel): void {
  const label = channel.label;
  if (label !== "spatial-fast" && label !== "spatial" && label !== "spatial-reliable") {
    channel.close();
    return;
  }
  if (label === "spatial-fast") {
    if (fastSpatialChannel && fastSpatialChannel !== channel) fastSpatialChannel.close();
    fastSpatialChannel = channel;
  } else if (label === "spatial") {
    if (compatSpatialChannel && compatSpatialChannel !== channel) compatSpatialChannel.close();
    compatSpatialChannel = channel;
  } else {
    if (reliableSpatialChannel && reliableSpatialChannel !== channel) reliableSpatialChannel.close();
    reliableSpatialChannel = channel;
  }

  channel.binaryType = "arraybuffer";
  channel.onopen = () => {
    if (!isCurrentSpatialChannel(channel)) return;
    activateCreatedSubscriptions();
  };
  channel.onmessage = event => { if (isCurrentSpatialChannel(channel)) handleTelemetryData(event.data); };
  channel.onclose = () => handleSpatialChannelLoss(channel);
  channel.onerror = () => handleSpatialChannelLoss(channel);
}

function isCurrentSpatialChannel(channel: RTCDataChannel): boolean {
  return channel === fastSpatialChannel || channel === compatSpatialChannel || channel === reliableSpatialChannel;
}

function handleSpatialChannelLoss(channel: RTCDataChannel): void {
  const wasReliable = channel === reliableSpatialChannel;
  const wasFast = channel === fastSpatialChannel || channel === compatSpatialChannel;
  if (channel === fastSpatialChannel) fastSpatialChannel = null;
  if (channel === compatSpatialChannel) compatSpatialChannel = null;
  if (channel === reliableSpatialChannel) reliableSpatialChannel = null;
  if (wasReliable) clearSubscriptionsForTransport(true);
  if (wasFast && !isFastOpen()) clearSubscriptionsForTransport(false);
  if (wasReliable || (wasFast && !isFastOpen())) scheduleSubscriptionRetry();
}

function handleTelemetryData(data: unknown): void {
  let text: string;
  if (typeof data === "string") text = data;
  else if (data instanceof ArrayBuffer) text = new TextDecoder().decode(new Uint8Array(data));
  else if (ArrayBuffer.isView(data)) text = new TextDecoder().decode(new Uint8Array(data.buffer, data.byteOffset, data.byteLength));
  else return;
  let packet: any;
  try { packet = JSON.parse(text); } catch { telemetryDropped++; return; }
  const streamId = typeof packet?.streamId === "string" ? packet.streamId : "";
  const sequence = Number(packet?.sequence);
  if (!streamId || !Number.isSafeInteger(sequence) || sequence < 0) { telemetryDropped++; return; }
  const previous = lastSequenceByStream.get(streamId);
  if (previous != null && sequence <= previous) { telemetryDropped++; return; }
  lastSequenceByStream.set(streamId, sequence);
  telemetryLastSequence = sequence;
  if (active) setStatus(`Streaming 1080p / 30fps · Spatial seq ${telemetryLastSequence} · drop ${telemetryDropped}`);
}

function matchesSession(message: any): boolean {
  return Boolean(activeSession) && message.sessionId === activeSession?.sessionId &&
    (message.negotiationId ?? null) === (activeSession?.negotiationId ?? null) &&
    message.from === config.questDeviceId && message.to === config.deviceId;
}

async function createPeer(session: SessionMessage): Promise<void> {
  const track = stream?.getVideoTracks()[0];
  if (!track) return;
  closePeer(false);
  const current = new RTCPeerConnection({ iceServers: [{ urls: ["stun:stun.l.google.com:19302"] }] });
  peer = current;
  remoteReady = false;
  pendingIce = [];
  subscriptions.reset();
  lastSequenceByStream.clear();
  telemetryDropped = 0;
  telemetryLastSequence = -1;
  current.addTrack(track, stream!);

  // Negotiate SCTP in the original offer. Quest may then open realtime and reliable
  // Spatial channels in-band without another SDP negotiation.
  const bootstrapChannel = current.createDataChannel("spatial-bootstrap", { ordered: false, maxRetransmits: 0, protocol: "qps-spatial-v1" });
  bootstrapChannel.onopen = () => bootstrapChannel.close();
  current.ondatachannel = event => {
    if (current !== peer) { event.channel.close(); return; }
    attachSpatialChannel(event.channel);
  };
  current.onicecandidate = event => {
    if (!event.candidate || current !== peer) return;
    send({ type: "ice", token: config.token, sessionId: session.sessionId, negotiationId: session.negotiationId,
      from: config.deviceId, to: config.questDeviceId, candidate: event.candidate.toJSON() });
  };
  current.onconnectionstatechange = () => {
    if (current !== peer) return;
    const connected = current.connectionState === "connected";
    setRuntimeState(authorized, connected);
    if (connected) {
      setStatus("Streaming 1080p / 30fps · negotiating Spatial telemetry");
      requestTelemetrySubscriptions();
    }
    if (["failed", "disconnected", "closed"].includes(current.connectionState)) {
      subscriptions.reset();
      setRuntimeState(authorized, false);
    }
  };
  const offer = await current.createOffer({ offerToReceiveAudio: false, offerToReceiveVideo: false });
  if (current !== peer) return;
  await current.setLocalDescription(offer);
  send({ type: "offer", token: config.token, sessionId: session.sessionId, negotiationId: session.negotiationId,
    from: config.deviceId, to: config.questDeviceId, sdp: offer.sdp ?? "" });
}

function closePeer(updateState = true): void {
  clearSubscriptionRetry();
  const old = peer;
  peer = null;
  remoteReady = false;
  pendingIce = [];
  subscriptions.reset();
  lastSequenceByStream.clear();
  const oldFast = fastSpatialChannel;
  const oldCompat = compatSpatialChannel;
  const oldReliable = reliableSpatialChannel;
  fastSpatialChannel = null;
  compatSpatialChannel = null;
  reliableSpatialChannel = null;
  oldFast?.close();
  oldCompat?.close();
  oldReliable?.close();
  old?.close();
  if (updateState) setRuntimeState(authorized, false);
}

async function startCapture(): Promise<void> {
  const sourceId = sourceList().value;
  if (!sourceId) return;
  stopCapture();
  try {
    const constraints = { audio: false, video: { mandatory: {
      chromeMediaSource: "desktop", chromeMediaSourceId: sourceId,
      minWidth: config.width, maxWidth: config.width, minHeight: config.height, maxHeight: config.height,
      minFrameRate: config.fps, maxFrameRate: config.fps
    } } } as unknown as MediaStreamConstraints;
    stream = await navigator.mediaDevices.getUserMedia(constraints);
    const track = stream.getVideoTracks()[0];
    if (!track) throw new Error("No screen video track");
    track.contentHint = "detail";
    track.addEventListener("ended", () => {
      stream = null; closePeer(); setRuntimeState(authorized, false); setStatus("Screen sharing stopped");
    }, { once: true });
    setRuntimeState(true, false);
    setStatus("Screen captured · waiting for Quest session");
    if (activeSession) await createPeer(activeSession);
  } catch (error) {
    stream = null; setRuntimeState(false, false);
    setStatus(`Screen capture denied/unavailable: ${error instanceof Error ? error.message : "error"}`);
  }
}

function stopCapture(): void {
  closePeer();
  stream?.getTracks().forEach(track => track.stop());
  stream = null;
  setRuntimeState(authorized, false);
}

function startHeartbeat(): void {
  clearHeartbeat();
  heartbeatTimer = window.setInterval(() => send({ type: "heartbeat", token: config.token, deviceId: config.deviceId, timestamp: Date.now() }), 15000);
}
function clearHeartbeat(): void { if (heartbeatTimer != null) window.clearInterval(heartbeatTimer); heartbeatTimer = null; }

async function bootstrap(): Promise<void> {
  config = await window.qps.getConfig();
  document.getElementById("identity")!.textContent = `${config.deviceId} · ${config.platform} · ${config.width}×${config.height}@${config.fps}`;
  const sources = await window.qps.listSources();
  sourceList().replaceChildren(...sources.map(source => {
    const option = document.createElement("option"); option.value = source.id; option.textContent = source.name; return option;
  }));
  startButton().addEventListener("click", () => void startCapture());
  stopButton().addEventListener("click", stopCapture);
  connectSignaling();
}

void bootstrap();