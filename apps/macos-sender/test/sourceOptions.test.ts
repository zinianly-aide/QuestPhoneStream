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

  it("registers core UI/ADB actions before async screen enumeration", () => {
    const renderer = readFileSync(
      fileURLToPath(new URL("../src/renderer.ts", import.meta.url)), "utf8"
    );
    const bootCall = renderer.lastIndexOf("void bootstrap().catch");
    const wireCall = renderer.lastIndexOf("wireCoreControls();");
    expect(wireCall).toBeGreaterThan(0);
    expect(wireCall).toBeLessThan(bootCall);
    const bootstrap = renderer.slice(
      renderer.indexOf("async function bootstrap():"),
      renderer.indexOf("function wireCoreControls():")
    );
    expect(bootstrap).not.toContain('el.adbRefresh().addEventListener("click"');
    expect(renderer).toContain('status.textContent = "Refreshing wireless ADB discovery…"');
  });
  it("renders source and bridge input before async enumeration starts", () => {
    const html = readFileSync(
      fileURLToPath(new URL("../src/index.html", import.meta.url)), "utf8"
    );
    expect(html).toContain('id="ai-bridge-url"');
    expect(html).toContain('<option value="qps-ai-video">');
  });
});
