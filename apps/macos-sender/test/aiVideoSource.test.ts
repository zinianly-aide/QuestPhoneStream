import { afterEach, describe, expect, it, vi } from "vitest";
import {
  AI_VIDEO_SOURCE_ID,
  DEFAULT_AI_VIDEO_BRIDGE_URL,
  frameRequestUrl,
  normalizeBridgeUrl,
  createAiVideoSource
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

  afterEach(() => vi.unstubAllGlobals());

  it("primes the real landscape frame before creating the WebRTC canvas track", async () => {
    const drawImage = vi.fn();
    const captureStream = vi.fn(() => ({ getVideoTracks: () => [{
      readyState: "ended", stop: vi.fn(), contentHint: ""
    }], getTracks: () => [] }));
    const canvas = {
      width: 2, height: 2, getContext: () => ({ drawImage }), captureStream
    };
    const imageClose = vi.fn();
    vi.stubGlobal("document", { createElement: () => canvas });
    vi.stubGlobal("createImageBitmap", vi.fn(async () => ({
      width: 672, height: 368, close: imageClose
    })));
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce({ ok: true, status: 200 })
      .mockResolvedValueOnce({
        ok: true, status: 200,
        headers: new Headers({ "X-QPS-Frame-Seq": "38", "X-QPS-PTS-Ms": "7600" }),
        blob: async () => new Blob()
      }));

    const handle = await createAiVideoSource({ bridgeUrl: "http://127.0.0.1:18766" });
    expect(canvas.width).toBe(672);
    expect(canvas.height).toBe(368);
    expect(drawImage).toHaveBeenCalledOnce();
    expect(imageClose).toHaveBeenCalledOnce();
    expect(captureStream).toHaveBeenCalledOnce();
    expect(handle.stats().lastSequence).toBe(38);
    expect(handle.stats().receivedFrames).toBe(1);
    handle.stop();
  });

  it("builds cache-busting frame URLs", () => {
    expect(frameRequestUrl(DEFAULT_AI_VIDEO_BRIDGE_URL, 7))
      .toBe("http://127.0.0.1:8765/v1/frame.jpg?poll=7");
  });
});
