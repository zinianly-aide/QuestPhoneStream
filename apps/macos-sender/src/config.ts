import { app } from "electron";
import path from "node:path";
import fs from "node:fs";
import os from "node:os";
import { randomUUID } from "node:crypto";

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

export interface PersistedConfig {
  /** Manually applied signaling endpoint; empty means "use discovered/env". */
  signalingUrl?: string;
  /** True when the user explicitly applied a signaling endpoint in the UI. */
  manualSignaling?: boolean;
  token?: string;
  questDeviceId?: string;
  sessionId?: string;
}

export type SignalingSource = "persisted" | "discovered" | "env" | "none";

export interface ResolvedSignaling {
  url: string;
  source: SignalingSource;
}

function configFile(): string {
  return path.join(app.getPath("userData"), "config.json");
}

export function loadPersisted(): PersistedConfig {
  try {
    const raw = fs.readFileSync(configFile(), "utf8");
    const parsed = JSON.parse(raw) as PersistedConfig;
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

export function savePersisted(patch: Partial<PersistedConfig>): PersistedConfig {
  const current = loadPersisted();
  const next: PersistedConfig = { ...current, ...patch };
  fs.mkdirSync(path.dirname(configFile()), { recursive: true });
  fs.writeFileSync(configFile(), JSON.stringify(next, null, 2), "utf8");
  return next;
}

export function persistentDeviceId(): string {
  const file = path.join(app.getPath("userData"), "device-id.txt");
  try {
    const existing = fs.readFileSync(file, "utf8").trim();
    if (existing) return existing;
  } catch {}
  const id = `mac-${randomUUID()}`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, id, "utf8");
  return id;
}

/**
 * Signaling endpoint resolution priority:
 *   1. persisted applied config (manualSignaling && signalingUrl)
 *   2. discovered endpoint (NSD _qps-device._tcp. TXT.signalingUrl)
 *   3. environment variable / manual input (QPS_SIGNALING_URL)
 *   4. none -> Waiting / Configure state
 * There is intentionally NO fallback to any hard-coded LAN IP.
 */
export function resolveSignaling(persisted: PersistedConfig, discoveredUrl = ""): ResolvedSignaling {
  if (persisted.manualSignaling && persisted.signalingUrl?.trim()) {
    return { url: persisted.signalingUrl.trim(), source: "persisted" };
  }
  const discovered = discoveredUrl.trim();
  if (discovered) {
    return { url: discovered, source: "discovered" };
  }
  const fromEnv = (process.env.QPS_SIGNALING_URL ?? "").trim();
  if (fromEnv) {
    return { url: fromEnv, source: "env" };
  }
  if (persisted.signalingUrl?.trim()) {
    return { url: persisted.signalingUrl.trim(), source: "persisted" };
  }
  return { url: "", source: "none" };
}

export function resolveConfig(discoveredUrl = ""): SenderConfig {
  const persisted = loadPersisted();
  const signaling = resolveSignaling(persisted, discoveredUrl);
  return {
    signalingUrl: signaling.url,
    token: (persisted.token || process.env.QPS_SIGNALING_TOKEN || "dev-token").trim(),
    deviceId: process.env.QPS_DEVICE_ID?.trim() || persistentDeviceId(),
    questDeviceId: (persisted.questDeviceId ?? process.env.QPS_QUEST_DEVICE_ID ?? "").trim(),
    sessionId: (persisted.sessionId ?? process.env.QPS_SESSION_ID ?? "").trim(),
    platform: "macos",
    sourceType: "screen",
    width: 1920,
    height: 1080,
    fps: 30
  };
}

export function hostname(): string {
  return os.hostname();
}
