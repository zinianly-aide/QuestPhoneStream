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

function ensureAiOption(): void {
  const select = document.getElementById("sources") as HTMLSelectElement | null;
  if (!select) return;
  if (Array.from(select.options).some(option => option.value === AI_VIDEO_SOURCE_ID)) return;
  const option = document.createElement("option");
  option.value = AI_VIDEO_SOURCE_ID;
  option.textContent = "LingBot AI video · localhost bridge";
  select.append(option);
}

function ensureBridgeInput(): void {
  const select = document.getElementById("sources");
  const card = select?.parentElement;
  if (!card || document.getElementById("ai-bridge-url")) return;

  const label = document.createElement("label");
  label.setAttribute("for", "ai-bridge-url");
  label.className = "muted";
  label.textContent = "LingBot bridge";

  const input = document.createElement("input");
  input.id = "ai-bridge-url";
  input.value = DEFAULT_AI_VIDEO_BRIDGE_URL;
  input.placeholder = DEFAULT_AI_VIDEO_BRIDGE_URL;
  input.autocomplete = "off";
  input.spellcheck = false;

  card.insertBefore(label, select);
  card.insertBefore(input, select);
}

const observer = new MutationObserver(() => {
  ensureBridgeInput();
  ensureAiOption();
});
observer.observe(document.documentElement, { childList: true, subtree: true });

window.addEventListener("DOMContentLoaded", () => {
  ensureBridgeInput();
  ensureAiOption();
});

window.addEventListener("beforeunload", () => {
  observer.disconnect();
  activeHandle?.stop();
  activeHandle = null;
});
