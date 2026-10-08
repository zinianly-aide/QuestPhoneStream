import {
  AI_VIDEO_SOURCE_ID,
  DEFAULT_AI_VIDEO_BRIDGE_URL,
  createAiVideoSource,
  normalizeBridgeUrl,
  type AiVideoSourceHandle
} from "./aiVideoSource";

const originalGetUserMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
let activeHandle: AiVideoSourceHandle | null = null;

function requestedSourceId(constraints?: MediaStreamConstraints): string {
  const video = constraints?.video;
  if (!video || typeof video === "boolean") return "";
  const mandatory = (video as MediaTrackConstraints & {
    mandatory?: { chromeMediaSourceId?: string };
  }).mandatory;
  return mandatory?.chromeMediaSourceId ?? "";
}

function configuredBridgeUrl(): string {
  const input = document.getElementById("ai-bridge-url") as HTMLInputElement | null;
  return normalizeBridgeUrl(input?.value || DEFAULT_AI_VIDEO_BRIDGE_URL);
}

async function getUserMedia(constraints?: MediaStreamConstraints): Promise<MediaStream> {
  if (requestedSourceId(constraints) !== AI_VIDEO_SOURCE_ID) {
    return originalGetUserMedia(constraints);
  }

  activeHandle?.stop();
  activeHandle = await createAiVideoSource({
    bridgeUrl: configuredBridgeUrl(),
    fps: 30
  });
  return activeHandle.stream;
}

Object.defineProperty(navigator.mediaDevices, "getUserMedia", {
  configurable: true,
  writable: true,
  value: getUserMedia
});

window.addEventListener("beforeunload", () => {
  activeHandle?.stop();
  activeHandle = null;
});
