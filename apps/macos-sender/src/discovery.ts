import { Bonjour, type Service } from "bonjour-service";

const DEVICE_TYPE = "qps-device";

/**
 * Watches the LAN for QuestPhoneStream devices (_qps-device._tcp.) and
 * reports their TXT signalingUrl as candidate signaling endpoints.
 * This lets the sender join the same signaling server the Quest uses,
 * without any hard-coded IP.
 */
export class SignalingDiscovery {
  private readonly bonjour = new Bonjour();
  private readonly seen = new Set<string>();
  private browser: ReturnType<Bonjour["find"]> | null = null;
  onCandidate?: (url: string) => void;

  start(): void {
    if (this.browser) return;
    this.browser = this.bonjour.find({ type: DEVICE_TYPE, protocol: "tcp" }, (service: Service) => {
      const url = service.txt?.signalingUrl ?? service.txt?.signaling_url ?? "";
      if (typeof url !== "string" || !url.trim()) return;
      const key = url.trim();
      if (this.seen.has(key)) return;
      this.seen.add(key);
      this.onCandidate?.(key);
    });
  }

  stop(): void {
    this.browser?.stop();
    this.browser = null;
    this.seen.clear();
    this.bonjour.destroy();
  }
}
