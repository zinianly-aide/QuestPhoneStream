import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { captureSourceOptions } from "../src/sourceOptions";

describe("Mac sender LingBot source availability", () => {
  it("keeps LingBot selectable when screen enumeration is empty", () => {
    expect(captureSourceOptions([])).toEqual([
      { id: "qps-ai-video", name: "LingBot AI video · localhost bridge" }
    ]);
  });

  it("preserves desktop capture options while appending LingBot exactly once", () => {
    const sources = captureSourceOptions([
      { id: "screen:1", name: "Primary display" },
      { id: "qps-ai-video", name: "Stale LingBot entry" }
    ]);
    expect(sources.map(source => source.id)).toEqual(["screen:1", "qps-ai-video"]);
    expect(sources[1].name).toBe("LingBot AI video · localhost bridge");
  });

  it("renders source and bridge input before async enumeration starts", () => {
    const html = readFileSync(
      fileURLToPath(new URL("../src/index.html", import.meta.url)), "utf8"
    );
    expect(html).toContain('id="ai-bridge-url"');
    expect(html).toContain('<option value="qps-ai-video">');
  });
});
