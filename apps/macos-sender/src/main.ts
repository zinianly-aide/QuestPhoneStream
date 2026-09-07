import { app, BrowserWindow, desktopCapturer, ipcMain } from "electron";
import path from "node:path";
import net from "node:net";
import { Bonjour } from "bonjour-service";
import {
  type SenderConfig,
  type PersistedConfig,
  loadPersisted,
  savePersisted,
  resolveConfig,
  persistentDeviceId,
  hostname
} from "./config";
import { SignalingDiscovery } from "./discovery";

class UnifiedDeviceAdvertisement {
  private readonly bonjour = new Bonjour();
  private readonly server = net.createServer(socket => socket.end());
  private service: ReturnType<Bonjour["publish"]> | null = null;
  private port = 0;
  private spatialReady = false;
  private signalingUrl = "";
  private publishSeq = 0;

  constructor(private readonly config: SenderConfig) {}

  async start(): Promise<void> {
    await new Promise<void>((resolve, reject) => {
      this.server.once("error", reject);
      this.server.listen(0, "0.0.0.0", () => {
        const address = this.server.address();
        this.port = typeof address === "object" && address ? address.port : 0;
        resolve();
      });
    });
    this.publish();
  }

  setSpatialReady(ready: boolean): void {
    if (this.spatialReady === ready) return;
    this.spatialReady = ready;
    this.refresh();
  }

  setSignalingUrl(url: string): void {
    const next = url.trim();
    if (this.signalingUrl === next) return;
    this.signalingUrl = next;
    this.refresh();
  }

  private refresh(): void {
    const previous = this.service;
    this.service = null;
    if (previous) previous.stop(() => this.publish());
    else this.publish();
  }

  private publish(): void {
    if (!this.port) return;
    const txt: Record<string, string> = {
      v: "1",
      id: this.config.deviceId,
      name: hostname(),
      caps: "screen",
      capv: "1",
      streamId: this.config.deviceId,
      platform: this.config.platform,
      sourceType: this.config.sourceType
    };
    if (this.signalingUrl) txt.signalingUrl = this.signalingUrl;
    if (this.spatialReady) txt.spatial = "1";
    // Unique per-publish name: mDNS caches stale same-name services from
    // abnormally-exited instances for TTL seconds, which would otherwise
    // trigger "Service name is already in use". Discovery keys by TXT.id,
    // so a unique name is safe.
    const uniqueName = `QuestPhoneStream Mac ${this.config.deviceId.slice(-8)}-${++this.publishSeq}-${Math.random().toString(36).slice(2, 6)}`;
    this.service = this.bonjour.publish({
      name: uniqueName,
      type: "qps-device",
      protocol: "tcp",
      port: this.port,
      txt,
      // Names are unique per publish; the probe is unnecessary and
      // mis-detects other LAN qps-device broadcasts as name collisions.
      probe: false
    });
    this.service.on("error", (err: unknown) => {
      log("bonjour publish error:", err instanceof Error ? err.message : err);
    });
  }

  stop(): void {
    this.service?.stop();
    this.service = null;
    this.bonjour.destroy();
    this.server.close();
  }
}

let advertisement: UnifiedDeviceAdvertisement | null = null;
let discovery: SignalingDiscovery | null = null;
let mainWindow: BrowserWindow | null = null;
let currentConfig: SenderConfig;
let pendingDiscoveredUrl = "";

function log(...parts: unknown[]): void {
  console.log(`[macos-sender]`, ...parts);
}

function applyDiscoveredSignaling(url: string): void {
  const persisted = loadPersisted();
  if (persisted.manualSignaling && persisted.signalingUrl?.trim()) return; // user choice wins
  const next = resolveConfig(url);
  currentConfig = next;
  advertisement?.setSignalingUrl(next.signalingUrl);
  log("signaling endpoint discovered:", next.signalingUrl);
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send("qps:signaling-changed", {
      url: next.signalingUrl,
      source: "discovered"
    });
  } else {
    pendingDiscoveredUrl = next.signalingUrl; // window not ready yet; deliver after load
  }
}

function broadcastConfigState(): void {
  const persisted = loadPersisted();
  mainWindow?.webContents.send("qps:config-state", {
    config: currentConfig,
    persisted,
    source: currentConfig.signalingUrl
      ? (persisted.manualSignaling && persisted.signalingUrl?.trim() ? "persisted" : "discovered-or-env")
      : "none"
  });
}

app.whenReady().then(async () => {
  currentConfig = resolveConfig();
  log("deviceId:", currentConfig.deviceId);
  log("signaling endpoint:", currentConfig.signalingUrl || "(none - waiting for discovery/manual config)");

  advertisement = new UnifiedDeviceAdvertisement(currentConfig);
  await advertisement.start();
  advertisement.setSignalingUrl(currentConfig.signalingUrl);
  log("advertisement started; port:", advertisement["port"]);

  discovery = new SignalingDiscovery();
  discovery.onCandidate = applyDiscoveredSignaling;
  discovery.start();
  log("discovery started");

  ipcMain.handle("qps:get-config", () => currentConfig);
  ipcMain.handle("qps:list-sources", async () => {
    const sources = await desktopCapturer.getSources({ types: ["screen"], thumbnailSize: { width: 320, height: 180 } });
    return sources.map(source => ({ id: source.id, name: source.name, thumbnail: source.thumbnail.toDataURL() }));
  });
  ipcMain.handle("qps:save-config", (_event, patch: Partial<PersistedConfig>) => {
    const persisted = savePersisted(patch ?? {});
    currentConfig = resolveConfig();
    advertisement?.setSignalingUrl(currentConfig.signalingUrl);
    log("config applied:", JSON.stringify({ manualSignaling: persisted.manualSignaling, signalingUrl: currentConfig.signalingUrl }));
    broadcastConfigState();
    return { config: currentConfig, persisted };
  });
  ipcMain.on("qps:spatial-ready", (_event, ready: boolean) => advertisement?.setSpatialReady(Boolean(ready)));

  mainWindow = new BrowserWindow({
    width: 760,
    height: 780,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false
    }
  });
  mainWindow.on("closed", () => { mainWindow = null; });
  await mainWindow.loadFile(path.join(__dirname, "index.html"));
  log("window loaded");
  if (pendingDiscoveredUrl) {
    mainWindow.webContents.send("qps:signaling-changed", {
      url: pendingDiscoveredUrl,
      source: "discovered"
    });
    pendingDiscoveredUrl = "";
  }
  broadcastConfigState();
});

app.on("before-quit", () => {
  advertisement?.stop();
  discovery?.stop();
});
app.on("window-all-closed", () => app.quit());

// Graceful teardown on SIGTERM/SIGINT so Bonjour goodbye packets are sent
// and no stale service lingers on the LAN.
process.on("SIGTERM", () => app.quit());
process.on("SIGINT", () => app.quit());
