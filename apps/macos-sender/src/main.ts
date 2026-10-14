import { app, BrowserWindow, desktopCapturer, ipcMain, screen, systemPreferences, shell } from "electron";
import { execFile } from "node:child_process";
import fs from "node:fs";
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
import { WirelessAdbAutoConnect } from "./adbAutoConnect";
import { displayIdFromSource, mapClickToDisplay, parseClickCommand } from "./remoteControl";

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
let wirelessAdb: WirelessAdbAutoConnect | null = null;
let mainWindow: BrowserWindow | null = null;
let currentConfig: SenderConfig;
let pendingDiscoveredUrl = "";

// Input is disabled by default on every start; Mac user must explicitly opt in.
let remoteClicksEnabled = false;
let lastRemoteClickAt = 0;
const clickHelper = () => app.isPackaged
  ? path.join(process.resourcesPath, "qps-mac-click")
  : path.join(__dirname, "../native/qps-mac-click");

function runClickHelper(args: string[]): Promise<{ ok: boolean; message: string }> {
  return new Promise(resolve => {
    const helper = clickHelper();
    if (!fs.existsSync(helper)) {
      resolve({ ok: false, message: "Native click helper missing; rebuild Mac Sender" });
      return;
    }
    execFile(helper, args, { timeout: args[0] === "drag" ? 5000 : 2500, windowsHide: true }, (error, stdout, stderr) => {
      resolve({
        ok: !error,
        message: error ? (stderr.trim() || error.message) : (stdout.trim() || "Click delivered")
      });
    });
  });
}


function log(...parts: unknown[]): void {
  console.log(`[macos-sender]`, ...parts);
}

// Durable control diagnostics; no tokens or actual click coordinates recorded.
function controlLog(message: string): void {
  const line = new Date().toISOString() + " [control] " + message + "\n";
  console.log(line.trim());
  try {
    fs.appendFileSync(path.join(app.getPath("userData"), "control-debug.log"), line);
  } catch (error) {
    console.warn("[control] unable to save diagnostic log:", String(error));
  }
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

  // ADB manages TLS pairing and auto-connect itself; we only start/observe it.
  wirelessAdb = new WirelessAdbAutoConnect(snapshot => {
    if (mainWindow && !mainWindow.isDestroyed())
      mainWindow.webContents.send("qps:adb-state", snapshot);
  });
  wirelessAdb.start();
  ipcMain.handle("qps:adb-status", () => wirelessAdb?.current());
  ipcMain.handle("qps:adb-refresh", async () => {
    controlLog("ADB refresh requested");
    if (!wirelessAdb) return { available: false, services: [], connectedWireless: [],
      message: "ADB discovery not initialized", checkedAt: Date.now() };
    const result = await wirelessAdb.refresh();
    controlLog("ADB refresh completed: available=" + result.available +
      " services=" + result.services.length +
      " connected=" + result.connectedWireless.length +
      " message=" + result.message.slice(0, 150));
    return result;
  });

  ipcMain.handle("qps:control-enable", async (event, enable: unknown) => {
    controlLog("toggle received enabled=" + String(enable));
    if (event.sender !== mainWindow?.webContents) {
      controlLog("toggle rejected: invalid sender");
      return { enabled: false, message: "Invalid sender" };
    }
    if (enable !== true) {
      remoteClicksEnabled = false;
      controlLog("remote clicks disabled by Mac user");
      return { enabled: false, message: "Remote clicks disabled" };
    }
    try {
      const status = await runClickHelper(["status", "--prompt"]);
      remoteClicksEnabled = status.ok;
      controlLog(status.ok ? "Accessibility granted; enabled" : "Accessibility denied: " + status.message);
      return { enabled: status.ok, message: status.ok ? "Remote clicks enabled" : status.message };
    } catch (error) {
      remoteClicksEnabled = false;
      controlLog("helper failed: " + String(error));
      return { enabled: false, message: "Accessibility check failed: " + String(error) };
    }
  });
  ipcMain.handle("qps:control-click", async (event, payload: unknown) => {
    if (event.sender !== mainWindow?.webContents || !remoteClicksEnabled ||
        typeof payload !== "object" || payload === null) return { ok: false, message: "Remote clicks disabled" };
    const request = payload as Record<string, unknown>;
    const sourceId = request.sourceId;
    const w = request.frameWidth, h = request.frameHeight;
    if (typeof sourceId !== "string" || !Number.isInteger(w) || !Number.isInteger(h) ||
        (w as number) < 1 || (h as number) < 1 || (w as number) > 16384 || (h as number) > 16384 ||
        typeof request.command !== "string") return { ok: false, message: "Invalid source" };
    const click = parseClickCommand(request.command, currentConfig.sessionId, w as number, h as number);
    if (!click) return { ok: false, message: "Invalid/expired control command" };
    if (Date.now() - lastRemoteClickAt < (click.type === "scroll" ? 60 : 100))
      return { ok: false, message: "Input rate limit" };
    const displayId = displayIdFromSource(sourceId);
    const displays = screen.getAllDisplays();
    const display = displays.find(d => d.id === displayId) ??
      (displayId === 0 && displays.length === 1 ? displays[0] : null);
    if (!display) return { ok: false, message: "Display not found" };
    const point = mapClickToDisplay(click, w as number, h as number, display.bounds);
    if (!point) return { ok: false, message: "Invalid coordinates" };
    let args = ["click", point.x.toFixed(2), point.y.toFixed(2)];
    if (click.type === "scroll") {
      args = ["scroll", point.x.toFixed(2), point.y.toFixed(2),
        String(click.deltaY), String(click.deltaX)];
    } else if (click.type === "swipe") {
      const end = mapClickToDisplay({ ...click, x: click.endX!, y: click.endY! },
        w as number, h as number, display.bounds);
      if (!end) return { ok: false, message: "Invalid swipe endpoint" };
      args = ["drag", point.x.toFixed(2), point.y.toFixed(2),
        end.x.toFixed(2), end.y.toFixed(2), String(click.durationMs)];
    }
    lastRemoteClickAt = Date.now();
    const outcome = await runClickHelper(args);
    if (click.type !== "scroll" || !outcome.ok)
      controlLog("remote " + click.type + (outcome.ok ? " executed" : " failed: " + outcome.message));
    if (!outcome.ok) {
      remoteClicksEnabled = false;
      log("remote click stopped:", outcome.message);
    }
    return outcome;
  });

  ipcMain.handle("qps:get-config", () => currentConfig);
  // Return explicit screen permission and enumeration status instead of
  // silently leaving the LingBot pseudo-source as the only selection.
  ipcMain.handle("qps:list-sources", async () => {
    const permission = systemPreferences.getMediaAccessStatus("screen");
    const displays = screen.getAllDisplays().length;
    try {
      const sources = await desktopCapturer.getSources({ types: ["screen"], thumbnailSize: { width: 320, height: 180 } });
      const result = sources.map(source => ({ id: source.id, name: source.name, thumbnail: source.thumbnail.toDataURL() }));
      controlLog("screen sources enumerated permission=" + permission +
        " displays=" + displays + " captured=" + result.length);
      return { sources: result, permission, displayCount: displays, error: "" };
    } catch (error) {
      const message = String(error);
      controlLog("screen enumeration failed permission=" + permission +
        " displays=" + displays + " error=" + message.slice(0, 200));
      return { sources: [], permission, displayCount: displays, error: message };
    }
  });
  ipcMain.handle("qps:open-screen-settings", async () => {
    await shell.openExternal("x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");
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
  mainWindow.webContents.on("console-message", (_event, level, message) => {
    if (message.includes("[control-ui]") || message.includes("[adb-ui]") ||
        message.includes("[screen-sources]") || message.includes("[control-channel]") ||
        message.includes("bootstrap failed"))
      controlLog("renderer: " + message.slice(0, 300));
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
  mainWindow.webContents.send("qps:adb-state", wirelessAdb.current());
});

app.on("before-quit", () => {
  wirelessAdb?.stop();
  advertisement?.stop();
  discovery?.stop();
});
app.on("window-all-closed", () => app.quit());

// Graceful teardown on SIGTERM/SIGINT so Bonjour goodbye packets are sent
// and no stale service lingers on the LAN.
process.on("SIGTERM", () => app.quit());
process.on("SIGINT", () => app.quit());
