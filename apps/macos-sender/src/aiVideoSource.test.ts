import { describe, expect, it } from "vitest";
import { frameRequestUrl, normalizeBridgeUrl } from "./aiVideoSource";

describe("aiVideoSource bridge URL", () => {
  it("accepts localhost endpoints and removes trailing slash", () => {
    expect(normalizeBridgeUrl("http://127.0.0.1:8765/")).toBe("http://127.0.0.1:8765");
    expect(normalizeBridgeUrl("http://localhost:8765///")).toBe("http://localhost:8765");
  });

  it("rejects non-local bridge endpoints", () => {
    expect(() => normalizeBridgeUrl("http://192.168.1.20:8765")).toThrow(/localhost/);
    expect(() => normalizeBridgeUrl("https://example.com")).toThrow(/localhost/);
  });

  it("builds cache-busting frame URLs", () => {
    expect(frameRequestUrl("http://127.0.0.1:8765", 7))
      .toBe("http://127.0.0.1:8765/v1/frame.jpg?poll=7");
  });
});
