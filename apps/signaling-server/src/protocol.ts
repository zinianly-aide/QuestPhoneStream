import type { RawData } from "ws";

export type ClientRole = "android" | "quest";

export interface RTCIceCandidateInitLike {
  candidate: string;
  sdpMid?: string | null;
  sdpMLineIndex?: number | null;
  usernameFragment?: string | null;
}

export type ClientMessage =
  | { type: "register"; token: string; role: ClientRole; deviceId: string }
  | {
      type: "create_session";
      token: string;
      sessionId: string;
      androidDeviceId: string;
      questDeviceId: string;
    }
  | { type: "offer" | "answer"; token: string; sessionId: string; from: string; to: string; sdp: string }
  | {
      type: "ice";
      token: string;
      sessionId: string;
      from: string;
      to: string;
      candidate: RTCIceCandidateInitLike;
    }
  | { type: "heartbeat"; token: string; deviceId: string; timestamp: number };

export type RelayMessage =
  | { type: "offer" | "answer"; sessionId: string; from: string; to: string; sdp: string }
  | {
      type: "ice";
      sessionId: string;
      from: string;
      to: string;
      candidate: RTCIceCandidateInitLike;
    };

export type ServerMessage =
  | { type: "registered"; role: ClientRole; deviceId: string }
  | { type: "session_created"; sessionId: string; androidDeviceId: string; questDeviceId: string }
  | { type: "peer_unavailable"; sessionId?: string; deviceId: string }
  | { type: "error"; code: string; message: string }
  | RelayMessage;

export function parseClientMessage(raw: RawData): ClientMessage {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw.toString("utf8"));
  } catch {
    throw new Error("invalid_json");
  }

  if (!isRecord(parsed) || typeof parsed.type !== "string") {
    throw new Error("invalid_message");
  }

  switch (parsed.type) {
    case "register":
      requireString(parsed, "token");
      requireString(parsed, "deviceId");
      if (parsed.role !== "android" && parsed.role !== "quest") throw new Error("invalid_role");
      return parsed as ClientMessage;
    case "create_session":
      requireString(parsed, "token");
      requireString(parsed, "sessionId");
      requireString(parsed, "androidDeviceId");
      requireString(parsed, "questDeviceId");
      return parsed as ClientMessage;
    case "offer":
    case "answer":
      requireRelayFields(parsed);
      requireString(parsed, "sdp");
      return parsed as ClientMessage;
    case "ice":
      requireRelayFields(parsed);
      if (!isRecord(parsed.candidate) || typeof parsed.candidate.candidate !== "string") {
        throw new Error("invalid_candidate");
      }
      return parsed as ClientMessage;
    case "heartbeat":
      requireString(parsed, "token");
      requireString(parsed, "deviceId");
      if (typeof parsed.timestamp !== "number") throw new Error("invalid_timestamp");
      return parsed as ClientMessage;
    default:
      throw new Error("unknown_type");
  }
}

export function serialize(message: ServerMessage): string {
  return JSON.stringify(message);
}

function requireRelayFields(value: Record<string, unknown>): void {
  requireString(value, "token");
  requireString(value, "sessionId");
  requireString(value, "from");
  requireString(value, "to");
}

function requireString(value: Record<string, unknown>, key: string): void {
  if (typeof value[key] !== "string" || value[key] === "") {
    throw new Error(`invalid_${key}`);
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
