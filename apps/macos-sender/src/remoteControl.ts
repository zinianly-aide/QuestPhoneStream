export interface IncomingControl {
  type: "click" | "swipe" | "scroll";
  sessionId: string;
  x: number;
  y: number;
  startX?: number;
  startY?: number;
  endX?: number;
  endY?: number;
  durationMs?: number;
  deltaX?: number;
  deltaY?: number;
  timestamp: number;
}

export interface ScreenGeometry {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Parse the existing Quest ControlCommandDto; reject malformed or stale input.
 * Remote control is always OFF until the Mac user explicitly enables it. */
// Android receives commands without a strict wall-clock check. Quest and Mac
// in the same authenticated LAN session can have several minutes of clock
// skew (measured 222s on Quest 3S). Keep bounded 5-minute tolerance, plus
// existing per-session, payload, pixel-range, consent and rate-limit checks.
export const CONTROL_CLOCK_SKEW_MS = 5 * 60_000;

export function parseClickCommand(data: unknown, sessionId: string, width: number, height: number, now = Date.now()): IncomingControl | null {
  if (typeof data !== "string" || data.length > 1024 || width < 1 || height < 1) return null;
  let raw: Record<string, unknown>;
  try { raw = JSON.parse(data); } catch { return null; }
  if (raw.version !== "1.0" || (raw.type !== "click" && raw.type !== "swipe" && raw.type !== "scroll") ||
      raw.sessionId !== sessionId || typeof raw.timestamp !== "number" ||
      (!Number.isSafeInteger(raw.timestamp) || raw.timestamp <= 0) || Math.abs(raw.timestamp - now) > CONTROL_CLOCK_SKEW_MS) return null;
  const pointOK = (x: unknown, y: unknown) =>
    Number.isInteger(x) && Number.isInteger(y) &&
    (x as number) >= 0 && (y as number) >= 0 &&
    (x as number) <= width && (y as number) <= height;
  if (raw.type === "click") {
    if (!pointOK(raw.x, raw.y)) return null;
    return { type: "click", sessionId, timestamp: raw.timestamp, x: raw.x as number, y: raw.y as number };
  }
  if (raw.type === "scroll") {
    if (!pointOK(raw.x, raw.y) || !Number.isInteger(raw.deltaX) || !Number.isInteger(raw.deltaY) ||
        Math.abs(raw.deltaX as number) > 4 || Math.abs(raw.deltaY as number) > 4 ||
        (raw.deltaX === 0 && raw.deltaY === 0)) return null;
    return { type: "scroll", sessionId, timestamp: raw.timestamp, x: raw.x as number,
      y: raw.y as number, deltaX: raw.deltaX as number, deltaY: raw.deltaY as number };
  }
  // Android accepts these same swipe fields. Restrict gesture duration and
  // coordinates before sending to the trusted native helper.
  if (!pointOK(raw.startX, raw.startY) || !pointOK(raw.endX, raw.endY) ||
      !Number.isInteger(raw.durationMs) || (raw.durationMs as number) < 50 ||
      (raw.durationMs as number) > 3000) return null;
  return { type: "swipe", sessionId, timestamp: raw.timestamp, x: raw.startX as number,
    y: raw.startY as number, startX: raw.startX as number, startY: raw.startY as number,
    endX: raw.endX as number, endY: raw.endY as number, durationMs: raw.durationMs as number };
}

/** Quest emits video pixels, macOS display bounds are in logical points.
 * Use ratio to account for Retina scale and configured capture downscaling. */
export function mapClickToDisplay(click: IncomingControl, frameWidth: number, frameHeight: number, display: ScreenGeometry): { x: number; y: number } | null {
  if (!(frameWidth > 0 && frameHeight > 0 && display.width > 0 && display.height > 0)) return null;
  const x = display.x + Math.max(0, Math.min(1, click.x / frameWidth)) * display.width;
  const y = display.y + Math.max(0, Math.min(1, click.y / frameHeight)) * display.height;
  if (!Number.isFinite(x) || !Number.isFinite(y)) return null;
  return { x, y };
}

export function displayIdFromSource(sourceId: string): number | null {
  const m = /^screen:(\d+):\d+$/.exec(sourceId);
  return m ? Number(m[1]) : null;
}

/** Unity WebRTC sends byte[] as binary. Browser data channels may deliver
 * ArrayBuffer or Blob (depending on binaryType); both contain UTF-8 JSON. */
export async function decodeControlPayload(data: unknown): Promise<string | null> {
  if (typeof data === "string") return data.length <= 1024 ? data : null;
  if (data instanceof ArrayBuffer) {
    if (data.byteLength > 1024) return null;
    return new TextDecoder("utf-8", { fatal: true }).decode(new Uint8Array(data));
  }
  if (ArrayBuffer.isView(data)) {
    if (data.byteLength > 1024) return null;
    return new TextDecoder("utf-8", { fatal: true })
      .decode(new Uint8Array(data.buffer, data.byteOffset, data.byteLength));
  }
  if (typeof Blob !== "undefined" && data instanceof Blob) {
    if (data.size > 1024) return null;
    try { return new TextDecoder("utf-8", { fatal: true }).decode(await data.arrayBuffer()); }
    catch { return null; }
  }
  return null;
}
