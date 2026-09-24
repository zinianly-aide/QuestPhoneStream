export const SPATIAL_PROTOCOL_VERSION = "1.0" as const;
export const SPATIAL_MESSAGE_TYPES = [
  "device.hello",
  "device.capabilities.get",
  "device.capabilities.result",
  "device.capabilities.changed",
  "subscription.create",
  "subscription.created",
  "subscription.cancel",
  "subscription.closed",
  "protocol.error",
] as const;

export type SpatialMessageType = typeof SPATIAL_MESSAGE_TYPES[number];

export interface SpatialEnvelope {
  v: typeof SPATIAL_PROTOCOL_VERSION;
  id: string;
  type: SpatialMessageType;
  source: string;
  target: string;
  sessionId: string;
  streamId: string;
  correlationId: string;
  timestamp: number;
  payload: Record<string, unknown>;
}

const typeSet = new Set<string>(SPATIAL_MESSAGE_TYPES);

export function isSpatialMessageType(value: unknown): value is SpatialMessageType {
  return typeof value === "string" && typeSet.has(value);
}

export function parseSpatialEnvelope(value: Record<string, unknown>): SpatialEnvelope {
  if (value.v !== SPATIAL_PROTOCOL_VERSION) throw new Error("unsupported_spatial_version");
  if (!isSpatialMessageType(value.type)) throw new Error("unknown_spatial_type");
  for (const key of ["id", "source", "target", "sessionId", "streamId", "correlationId"] as const) {
    if (typeof value[key] !== "string") throw new Error(`invalid_${key}`);
  }
  if (value.id === "" || value.source === "" || value.target === "") throw new Error("invalid_spatial_identity");
  if (typeof value.timestamp !== "number" || !Number.isSafeInteger(value.timestamp) || value.timestamp < 0) {
    throw new Error("invalid_timestamp");
  }
  if (!isRecord(value.payload)) throw new Error("invalid_payload");
  return value as unknown as SpatialEnvelope;
}

export function negotiateSpatialVersion(offered: unknown): typeof SPATIAL_PROTOCOL_VERSION | null {
  if (!Array.isArray(offered)) return null;
  return offered.some(value => value === SPATIAL_PROTOCOL_VERSION) ? SPATIAL_PROTOCOL_VERSION : null;
}

export function makeSpatialError(
  source: string,
  target: string,
  correlationId: string,
  code: string,
  message: string,
): SpatialEnvelope {
  return {
    v: SPATIAL_PROTOCOL_VERSION,
    id: `error-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`,
    type: "protocol.error",
    source,
    target,
    sessionId: "",
    streamId: "",
    correlationId,
    timestamp: Date.now(),
    payload: { code, message, retryable: false },
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
