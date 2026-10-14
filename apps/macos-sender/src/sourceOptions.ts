import { AI_VIDEO_SOURCE_ID } from "./aiVideoSource";

export const AI_VIDEO_SOURCE_LABEL = "LingBot AI video · localhost bridge";

export interface CaptureSourceOption {
  id: string;
  name: string;
}

/** Preserve the LingBot pseudo source after desktopCapturer refreshes the list. */
export function captureSourceOptions<T extends CaptureSourceOption>(screens: T[]): CaptureSourceOption[] {
  return [
    ...screens.filter(source => source.id !== AI_VIDEO_SOURCE_ID),
    { id: AI_VIDEO_SOURCE_ID, name: AI_VIDEO_SOURCE_LABEL }
  ];
}
