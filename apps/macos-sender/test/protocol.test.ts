import { describe, expect, it } from "vitest";
import { displayPublishCapability, helloPayload, type SenderConfig } from "../src/protocol";

const config: SenderConfig = {
  signalingUrl: "ws://127.0.0.1:8787",
  token: "dev-token",
  deviceId: "mac-test",
  questDeviceId: "quest-test",
  sessionId: "session-test",
  platform: "macos",
  sourceType: "screen",
  width: 1920,
  height: 1080,
  fps: 30
};

describe("mac capability metadata", () => {
  it("advertises only the implemented display publisher", () => {
    const capability = displayPublishCapability(true, false);
    expect(capability.name).toBe("display.publish");
    expect(capability.transports).toEqual(["webrtc.video"]);
    expect(capability.state).toEqual({ available: true, authorized: true, active: false });
  });

  it("keeps platform and source type in device metadata", () => {
    const hello = helloPayload(config) as any;
    expect(hello.device.platform).toBe("macos");
    expect(hello.device.sourceType).toBe("screen");
    expect(hello.device.protocolVersions).toContain("1.0");
  });
});
