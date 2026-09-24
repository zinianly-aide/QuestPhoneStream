import { describe, expect, it } from "vitest";
import {
  AI_VIDEO_SOURCE_ID,
  DEFAULT_AI_VIDEO_BRIDGE_URL,
  frameRequestUrl,
  normalizeBridgeUrl
} from "../src/aiVideoSource";

describe("AI video source contract", () => {
  it("uses a stable pseudo source id", () => {
    expect(AI_VIDEO_SOURCE_ID).toBe("qps-ai-video");
  });

  it("accepts only localhost bridge URLs", () => {
    expect(normalizeBridgeUrl("http://127.0.0.1:8765/"))
      .toBe("http://127.0.0.1:8765");
    expect(normalizeBridgeUrl("http://localhost:8765"))
      .toBe("http://localhost:8765");
    expect(() => normalizeBridgeUrl("http://192.168.1.20:8765"))
      .toThrow(/localhost/);
    expect(() => normalizeBridgeUrl("https://example.com"))
      .toThrow(/localhost/);
  });

  it("builds cache-busting frame URLs", () => {
    expect(frameRequestUrl(DEFAULT_AI_VIDEO_BRIDGE_URL, 7))
      .toBe("http://127.0.0.1:8765/v1/frame.jpg?poll=7");
  });
});
