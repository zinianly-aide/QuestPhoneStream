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

/** App-level Quest presence, independent of OS-owned secure wireless ADB mDNS. */
export interface QuestPresence {
  id: string;
  name: string;
  addresses: string[];
  port: number;
}

export function parseQuestPresence(service: Pick<Service, "name" | "port" | "txt" | "addresses">): QuestPresence | null {
  const txt = service.txt ?? {};
  if (txt.v !== "1" || txt.platform !== "quest" ||
      typeof txt.id !== "string" || !/^[a-f0-9]{32}$/i.test(txt.id) ||
      typeof service.port !== "number" || service.port < 1 || service.port > 65535)
    return null;
  const addresses = (service.addresses ?? []).filter(a =>
    typeof a === "string" && /^[0-9a-f.:]+$/i.test(a) && a.length <= 45).slice(0, 4);
  return { id: txt.id.toLowerCase(), name: String(service.name).slice(0, 64),
    addresses, port: service.port };
}

export class QuestPresenceDiscovery {
  private readonly bonjour = new Bonjour();
  private browser: ReturnType<Bonjour["find"]> | null = null;
  private readonly visible = new Map<string, QuestPresence>();
  onChange?: (items: QuestPresence[]) => void;
  snapshot(): QuestPresence[] { return [...this.visible.values()]; }
  start(): void {
    if (this.browser) return;
    this.browser = this.bonjour.find({ type: "qps-quest", protocol: "tcp" }, (service: Service) => {
      const parsed = parseQuestPresence(service);
      if (!parsed) return;
      this.visible.set(service.fqdn, parsed);
      this.onChange?.(this.snapshot());
    });
    this.browser.on("down", (service: Service) => {
      if (this.visible.delete(service.fqdn)) this.onChange?.(this.snapshot());
    });
  }
  stop(): void {
    this.browser?.stop(); this.browser = null;
    this.visible.clear(); this.bonjour.destroy();
  }
}
