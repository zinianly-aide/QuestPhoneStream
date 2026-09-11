export interface SenderConfig {
  signalingUrl: string;
  token: string;
  deviceId: string;
  questDeviceId: string;
  sessionId: string;
  platform: "macos";
  sourceType: "screen";
  width: number;
  height: number;
  fps: number;
}

export interface SpatialCapabilityDescriptor {
  name: string;
  version: string;
  state: { available: boolean; authorized: boolean; active: boolean };
  transports: string[];
  features: string[];
  limits: Array<{ name: string; value: string }>;
  permissions: string[];
}

export interface SpatialEnvelope {
  v: "1.0";
  id: string;
  type: string;
  source: string;
  target: string;
  sessionId: string;
  streamId: string;
  correlationId: string;
  timestamp: number;
  payload: Record<string, unknown>;
}

export function displayPublishCapability(authorized: boolean, active: boolean): SpatialCapabilityDescriptor {
  return {
    name: "display.publish",
    version: "1.0",
    state: { available: true, authorized, active },
    transports: ["webrtc.video"],
    features: ["screen.capture", "video-only"],
    limits: [
      { name: "width", value: "1920" },
      { name: "height", value: "1080" },
      { name: "fps", value: "30" }
    ],
    permissions: ["macos.screen_recording"]
  };
}

export function spatialEnvelope(
  type: string,
  config: SenderConfig,
  target: string,
  payload: Record<string, unknown>,
  correlationId = "",
  sessionId = "",
  streamId = ""
): SpatialEnvelope {
  return {
    v: "1.0",
    id: crypto.randomUUID().replaceAll("-", ""),
    type,
    source: config.deviceId,
    target,
    sessionId,
    streamId,
    correlationId,
    timestamp: Date.now(),
    payload
  };
}

export function helloPayload(config: SenderConfig, selectedVersion?: string): Record<string, unknown> {
  return {
    supportedVersions: selectedVersion ? undefined : ["1.0"],
    selectedVersion,
    device: {
      id: config.deviceId,
      name: config.deviceId,
      platform: config.platform,
      sourceType: config.sourceType,
      protocolVersions: ["1.0"]
    }
  };
}
