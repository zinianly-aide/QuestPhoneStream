export interface AiVideoSourceOptions {
  bridgeUrl?: string;
  fps?: number;
}

export interface AiVideoSourceStats {
  receivedFrames: number;
  duplicatePolls: number;
  errors: number;
  lastSequence: number;
  lastPtsMs: number;
}

export interface AiVideoSourceHandle {
  stream: MediaStream;
  stop(): void;
  stats(): AiVideoSourceStats;
}

export const AI_VIDEO_SOURCE_ID = "qps-ai-video";
export const DEFAULT_AI_VIDEO_BRIDGE_URL = "http://127.0.0.1:8765";

export function normalizeBridgeUrl(value: string): string {
  const trimmed = value.trim().replace(/\/+$/, "");
  if (!/^https?:\/\/(127\.0\.0\.1|localhost)(:\d+)?$/i.test(trimmed)) {
    throw new Error("AI video bridge must be a localhost http(s) URL");
  }
  return trimmed;
}

export function frameRequestUrl(baseUrl: string, pollId: number): string {
  return `${normalizeBridgeUrl(baseUrl)}/v1/frame.jpg?poll=${pollId}`;
}

/**
 * Convert LingBot's localhost latest-JPEG bridge into a browser MediaStream.
 *
 * WebRTC/signaling stay unchanged: Chromium receives a normal video track and
 * the existing sender path can add it to RTCPeerConnection exactly like a
 * desktop-capture track.
 */
export async function createAiVideoSource(options: AiVideoSourceOptions = {}): Promise<AiVideoSourceHandle> {
  const bridgeUrl = normalizeBridgeUrl(options.bridgeUrl ?? DEFAULT_AI_VIDEO_BRIDGE_URL);
  const fps = options.fps ?? 30;
  if (!Number.isFinite(fps) || fps <= 0 || fps > 60) throw new Error("fps must be in (0, 60]");

  const health = await fetch(`${bridgeUrl}/healthz`, { cache: "no-store" });
  if (!health.ok) throw new Error(`LingBot bridge unavailable: HTTP ${health.status}`);

  const canvas = document.createElement("canvas");
  canvas.width = 2;
  canvas.height = 2;
  const ctx = canvas.getContext("2d", { alpha: false });
  if (!ctx) throw new Error("2D canvas unavailable");
  ctx.fillStyle = "black";
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  const mediaStream = canvas.captureStream(fps);
  const track = mediaStream.getVideoTracks()[0];
  if (!track) throw new Error("canvas.captureStream produced no video track");
  track.contentHint = "detail";

  let stopped = false;
  let pollId = 0;
  let timer: number | null = null;
  const counters: AiVideoSourceStats = {
    receivedFrames: 0,
    duplicatePolls: 0,
    errors: 0,
    lastSequence: -1,
    lastPtsMs: 0
  };

  const intervalMs = Math.max(16, Math.round(1000 / fps));

  const stopInternal = (): void => {
    if (stopped) return;
    stopped = true;
    if (timer != null) window.clearTimeout(timer);
    timer = null;
  };

  const schedule = (): void => {
    if (stopped || track.readyState === "ended") {
      stopInternal();
      return;
    }
    timer = window.setTimeout(() => void pump(), intervalMs);
  };

  const pump = async (): Promise<void> => {
    if (stopped || track.readyState === "ended") {
      stopInternal();
      return;
    }
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), Math.max(1000, intervalMs * 4));
    try {
      const response = await fetch(frameRequestUrl(bridgeUrl, pollId++), {
        cache: "no-store",
        signal: controller.signal
      });
      if (response.status === 404 || response.status === 204) return;
      if (!response.ok) throw new Error(`frame HTTP ${response.status}`);

      const sequence = Number(response.headers.get("X-QPS-Frame-Seq") ?? "-1");
      const ptsMs = Number(response.headers.get("X-QPS-PTS-Ms") ?? "0");
      if (Number.isSafeInteger(sequence) && sequence <= counters.lastSequence) {
        counters.duplicatePolls++;
        return;
      }

      const bitmap = await createImageBitmap(await response.blob());
      try {
        if (canvas.width !== bitmap.width || canvas.height !== bitmap.height) {
          canvas.width = bitmap.width;
          canvas.height = bitmap.height;
        }
        ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
      } finally {
        bitmap.close();
      }
      counters.receivedFrames++;
      if (Number.isSafeInteger(sequence)) counters.lastSequence = sequence;
      if (Number.isFinite(ptsMs)) counters.lastPtsMs = ptsMs;
    } catch (error) {
      if (!stopped && !(error instanceof DOMException && error.name === "AbortError")) {
        counters.errors++;
        console.warn("[ai-video-source] frame poll failed", error);
      }
    } finally {
      window.clearTimeout(timeout);
      schedule();
    }
  };

  void pump();

  return {
    stream: mediaStream,
    stop(): void {
      stopInternal();
      mediaStream.getTracks().forEach(item => {
        if (item.readyState !== "ended") item.stop();
      });
    },
    stats(): AiVideoSourceStats {
      return { ...counters };
    }
  };
}
