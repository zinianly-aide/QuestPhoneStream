import { expect, describe, it } from "vitest";
import { CONTROL_CLOCK_SKEW_MS, decodeControlPayload, displayIdFromSource, mapClickToDisplay, parseClickCommand } from "../src/remoteControl";

describe("Remote macOS click contract", () => {
  const command = (overrides: Record<string, unknown> = {}) =>
    JSON.stringify({ version: "1.0", type: "click", sessionId: "qps-01",
      deviceId: "quest-3s-001", x: 960, y: 540, timestamp: 100_000, ...overrides });

  it("accepts a fresh click with session and frame bounds", () => {
    expect(parseClickCommand(command(), "qps-01", 1920, 1080, 100_000)?.x).toBe(960);
    expect(parseClickCommand(command({ x: 1920, y: 1080 }), "qps-01", 1920, 1080, 100_000)).not.toBeNull();
  });
  it("rejects stale, foreign, malformed and out-of-range control commands", () => {
    for (const input of [
      command({ type: "text_input" }), command({ sessionId: "other" }),
      command({ x: -1 }), command({ y: 1100 }), command({ timestamp: 0 }),
      command({ x: 3.3 }), "not-json", command({ version: "2.0" })
    ]) expect(parseClickCommand(input, "qps-01", 1920, 1080, 100_000)).toBeNull();
  });
  it("accepts measured Quest/Mac 222-second clock skew but rejects older packets", () => {
    const questFastBy222s = command({ timestamp: 100_000 + 222_000 });
    expect(parseClickCommand(questFastBy222s, "qps-01", 1920, 1080, 100_000)).not.toBeNull();
    expect(parseClickCommand(command({ timestamp: 100_000 + CONTROL_CLOCK_SKEW_MS + 1 }),
      "qps-01", 1920, 1080, 100_000)).toBeNull();
    expect(parseClickCommand(command({ timestamp: 100_000 - CONTROL_CLOCK_SKEW_MS - 1 }),
      "qps-01", 1920, 1080, 100_000)).toBeNull();
    expect(parseClickCommand(questFastBy222s, "wrong-session", 1920, 1080, 100_000)).toBeNull();
  });
  it("accepts Android-compatible swipe gesture and rejects invalid endpoints", () => {
    const swipe = command({ type: "swipe", startX: 100, startY: 200,
      endX: 600, endY: 320, durationMs: 200 });
    expect(parseClickCommand(swipe, "qps-01", 1920, 1080, 100_000)).toMatchObject({
      type: "swipe", x: 100, y: 200, endX: 600, endY: 320, durationMs: 200
    });
    expect(parseClickCommand(command({ type: "swipe", startX: 0, startY: 0,
      endX: 2000, endY: 200, durationMs: 100 }), "qps-01", 1920, 1080, 100_000)).toBeNull();
    expect(parseClickCommand(command({ type: "swipe", startX: 0, startY: 0,
      endX: 50, endY: 50, durationMs: 10000 }), "qps-01", 1920, 1080, 100_000)).toBeNull();
  });
  it("accepts bounded Mac wheel scroll in both axes and respects skew", () => {
    const up = command({ type: "scroll", deltaX: 0, deltaY: 2 });
    expect(parseClickCommand(up, "qps-01", 1920, 1080, 100_000)).toMatchObject({
      type: "scroll", x: 960, y: 540, deltaX: 0, deltaY: 2
    });
    expect(parseClickCommand(command({ type: "scroll", deltaX: -1, deltaY: -3 }),
      "qps-01", 1920, 1080, 100_000)).toMatchObject({ deltaX: -1, deltaY: -3 });
    expect(parseClickCommand(command({ type: "scroll", deltaX: 1, deltaY: 0,
      timestamp: 322_000 }), "qps-01", 1920, 1080, 100_000)).not.toBeNull();
  });
  it("rejects invalid wheel steps, missing axes, wrong session and bad anchor", () => {
    for (const over of [
      { type: "scroll" }, { type: "scroll", deltaX: 0, deltaY: 0 },
      { type: "scroll", deltaX: 5, deltaY: 1 }, { type: "scroll", deltaX: 1, deltaY: -5 },
      { type: "scroll", deltaX: .5, deltaY: 2 }, { type: "scroll", deltaX: 1 },
      { type: "scroll", deltaX: 0, deltaY: 1, x: -2 },
      { type: "scroll", deltaX: 0, deltaY: 1, sessionId: "foreign" }
    ]) expect(parseClickCommand(command(over), "qps-01", 1920, 1080, 100_000)).toBeNull();
  });
  it("maps video pixels to logical Mac display bounds including retina and origin", () => {
    const click = parseClickCommand(command(), "qps-01", 1920, 1080, 100_000)!;
    expect(mapClickToDisplay(click, 1920, 1080, { x: 1280, y: -400, width: 1440, height: 900 }))
      .toEqual({ x: 2000, y: 50 });
  });
  it("decodes Quest binary UTF-8 DataChannel packets", async () => {
    const utf8 = new TextEncoder().encode(command());
    expect(await decodeControlPayload(utf8.buffer)).toBe(command());
    expect(await decodeControlPayload(new Blob([utf8]))).toBe(command());
    expect(await decodeControlPayload(utf8)).toBe(command());
    expect(await decodeControlPayload("text")).toBe("text");
    expect(await decodeControlPayload(new Uint8Array(1025).buffer)).toBeNull();
    expect(await decodeControlPayload(new Blob([new Uint8Array(1025)]))).toBeNull();
  });
  it("only accepts desktop screen source ids, never model video", () => {
    expect(displayIdFromSource("screen:69732352:0")).toBe(69732352);
    expect(displayIdFromSource("qps-ai-video")).toBeNull();
  });
});
