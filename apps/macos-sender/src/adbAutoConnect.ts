import { execFile } from "node:child_process";
import { existsSync } from "node:fs";
import { homedir } from "node:os";
import path from "node:path";
import net from "node:net";

/** ADB itself connects only to devices that were previously TLS-paired.
 * Do not explicitly adb connect to arbitrary untrusted Bonjour announcements. */
export interface AdbDiscoverySnapshot {
  available: boolean;
  services: string[];
  connectedWireless: string[];
  message: string;
  checkedAt: number;
}

export function parseTlsConnectServices(output: string): string[] {
  const addresses = new Set<string>();
  for (const line of output.split(/\r?\n/)) {
    if (!line.includes("_adb-tls-connect._tcp")) continue;
    const match = line.match(/((?:\d{1,3}\.){3}\d{1,3}):(\d{1,5})(?:\s|$)/);
    if (!match || net.isIP(match[1]) !== 4) continue;
    const port = Number(match[2]);
    if (port >= 1 && port <= 65535) addresses.add(match[1] + ":" + port);
  }
  return [...addresses];
}

export function parseWirelessDevices(output: string): string[] {
  const connected = new Set<string>();
  for (const line of output.split(/\r?\n/)) {
    const match = line.match(/^\s*(\S+)\s+device(?:\s|$)/);
    if (!match) continue;
    const serial = match[1];
    const remote = serial.match(/^((?:\d{1,3}\.){3}\d{1,3}):(\d{1,5})$/);
    if (remote && net.isIP(remote[1]) === 4 && Number(remote[2]) >= 1 &&
        Number(remote[2]) <= 65535) connected.add(serial);
  }
  return [...connected];
}

export function findAdbExecutable(): string {
  const bases = [
    process.env.ANDROID_HOME,
    process.env.ANDROID_SDK_ROOT,
    path.join(homedir(), "Library/Android/sdk")
  ].filter((item): item is string => Boolean(item));
  const candidates = [
    ...bases.map(dir => path.join(dir, "platform-tools/adb")),
    "/opt/homebrew/bin/adb",
    "/usr/local/bin/adb"
  ];
  return candidates.find(existsSync) ?? "adb";
}

type AdbRunner = (args: string[]) => Promise<string>;
function makeRunner(binary: string): AdbRunner {
  return args => new Promise<string>((resolve, reject) => {
    execFile(binary, args, { timeout: 8000, maxBuffer: 128 * 1024 }, (error, stdout) => {
      if (error) reject(error);
      else resolve(stdout);
    });
  });
}

export class WirelessAdbAutoConnect {
  private readonly run: AdbRunner;
  private timer: NodeJS.Timeout | null = null;
  private pending: Promise<AdbDiscoverySnapshot> | null = null;
  private stopped = false;
  private snapshot: AdbDiscoverySnapshot = {
    available: false, services: [], connectedWireless: [],
    message: "Waiting for ADB discovery", checkedAt: 0
  };

  constructor(
    private readonly onUpdate: (snapshot: AdbDiscoverySnapshot) => void,
    runner?: AdbRunner
  ) {
    this.run = runner ?? makeRunner(findAdbExecutable());
  }

  start(): void {
    if (this.timer) return;
    this.stopped = false;
    void this.refresh();
    this.timer = setInterval(() => void this.refresh(), 15000);
    this.timer.unref();
  }

  stop(): void {
    this.stopped = true;
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
  }

  current(): AdbDiscoverySnapshot {
    return { ...this.snapshot, services: [...this.snapshot.services],
      connectedWireless: [...this.snapshot.connectedWireless] };
  }

  refresh(): Promise<AdbDiscoverySnapshot> {
    if (this.pending) return this.pending;
    this.pending = this.poll().finally(() => { this.pending = null; });
    return this.pending;
  }

  private async poll(): Promise<AdbDiscoverySnapshot> {
    let next: AdbDiscoverySnapshot;
    try {
      // Starts the ADB server and its native secure mDNS auto-connection.
      await this.run(["start-server"]);
      const [servicesRaw, devicesRaw] = await Promise.all([
        this.run(["mdns", "services"]),
        this.run(["devices", "-l"])
      ]);
      const services = parseTlsConnectServices(servicesRaw);
      const connectedWireless = parseWirelessDevices(devicesRaw);
      next = {
        available: true, services, connectedWireless,
        message: connectedWireless.length
          ? "Wireless ADB connected (Android authentication)"
          : services.length
            ? "TLS ADB found; pair this Mac once in Quest system settings"
            : "Waiting for Quest wireless debugging / mDNS",
        checkedAt: Date.now()
      };
    } catch (error) {
      next = {
        available: false, services: [], connectedWireless: [],
        message: "ADB unavailable: " + (error instanceof Error ? error.message : String(error)),
        checkedAt: Date.now()
      };
    }
    if (!this.stopped) {
      this.snapshot = next;
      this.onUpdate(this.current());
    }
    return next;
  }
}
