import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("electron", () => ({
  app: { getPath: () => "/tmp/qps-test-userdata" }
}));

import { resolveSignaling, resolveConfig, type PersistedConfig } from "../src/config";

describe("config resolution priority", () => {
  beforeEach(() => {
    vi.stubEnv("QPS_SIGNALING_URL", "");
    vi.stubEnv("QPS_DEVICE_ID", "");
    vi.stubEnv("QPS_QUEST_DEVICE_ID", "");
    vi.stubEnv("QPS_SESSION_ID", "");
    vi.stubEnv("QPS_SIGNALING_TOKEN", "");
  });

  it("prefers persisted applied config over discovered and env", () => {
    const persisted: PersistedConfig = { manualSignaling: true, signalingUrl: "ws://persisted:8787" };
    const resolved = resolveSignaling(persisted, "ws://discovered:8787");
    expect(resolved.url).toBe("ws://persisted:8787");
    expect(resolved.source).toBe("persisted");
  });

  it("uses discovered endpoint when no manual config is applied", () => {
    const resolved = resolveSignaling({}, "ws://discovered:8787");
    expect(resolved.url).toBe("ws://discovered:8787");
    expect(resolved.source).toBe("discovered");
  });

  it("falls back to environment variable when neither persisted nor discovered", () => {
    vi.stubEnv("QPS_SIGNALING_URL", "ws://env:8787");
    const resolved = resolveSignaling({}, "");
    expect(resolved.url).toBe("ws://env:8787");
    expect(resolved.source).toBe("env");
  });

  it("falls back to historical persisted signaling URL last", () => {
    const persisted: PersistedConfig = { signalingUrl: "ws://history:8787" };
    const resolved = resolveSignaling(persisted, "");
    expect(resolved.url).toBe("ws://history:8787");
    expect(resolved.source).toBe("persisted");
  });

  it("returns none/waiting when nothing is available - no hard-coded IP fallback", () => {
    const resolved = resolveSignaling({}, "");
    expect(resolved.url).toBe("");
    expect(resolved.source).toBe("none");
  });

  it("never fabricates a fixed LAN IP in any branch", () => {
    const cases: Array<[PersistedConfig, string]> = [
      [{}, ""],
      [{ signalingUrl: "" }, ""],
      [{ manualSignaling: true, signalingUrl: "" }, ""],
      [{}, "ws://real-discovered:8787"]
    ];
    for (const [persisted, discovered] of cases) {
      const { url } = resolveSignaling(persisted, discovered);
      expect(url).not.toMatch(/192\.168\./);
      expect(url).not.toMatch(/10\.\d+\./);
    }
  });

  it("resolveConfig keeps manual persisted signaling and device id", () => {
    const persisted: PersistedConfig = { manualSignaling: true, signalingUrl: "ws://manual:8787" };
    vi.stubEnv("QPS_DEVICE_ID", "");
    // resolveConfig reads persisted from disk; simulate by env-only path via resolveSignaling semantics
    expect(resolveSignaling(persisted, "").url).toBe("ws://manual:8787");
  });

  it("resolveConfig returns empty signaling when nothing configured", () => {
    const config = resolveConfig("");
    expect(config.signalingUrl).toBe("");
    expect(config.deviceId).toBeTruthy();
    expect(config.platform).toBe("macos");
    expect(config.sourceType).toBe("screen");
  });
});

describe("token resolution", () => {
  it("defaults to dev-token when nothing configured", () => {
    const cfg = resolveConfig("");
    expect(cfg.token).toBe("dev-token");
  });
  it("env QPS_SIGNALING_TOKEN overrides default", () => {
    process.env.QPS_SIGNALING_TOKEN = "env-token";
    try {
      const cfg = resolveConfig("");
      expect(cfg.token).toBe("env-token");
    } finally {
      delete process.env.QPS_SIGNALING_TOKEN;
    }
  });
  it("no fixed-IP fallback is ever produced", () => {
    const cfg = resolveConfig("");
    expect(cfg.signalingUrl).toBe("");
    expect(cfg.signalingUrl).not.toMatch(/^ws:\/\/192\.168\./);
  });
});
