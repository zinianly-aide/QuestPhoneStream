import { app, BrowserWindow, desktopCapturer, ipcMain } from "electron";
import path from "node:path";
import fs from "node:fs";
import net from "node:net";
import os from "node:os";
import { randomUUID } from "node:crypto";
import { Bonjour } from "bonjour-service";

interface SenderConfig {
  signalingUrl: string;
  token: string;
  deviceId: string;
  questDeviceId: string;
  sessionId: string;
  platform: "macos";
  sourceType: "screen";
  width: number;
  height: number;
  fps: number;
}

class UnifiedDeviceAdvertisement {
  private readonly bonjour = new Bonjour();
  private readonly server = net.createServer(socket => socket.end());
  private service: ReturnType<Bonjour["publish"]> | null = null;
  private port = 0;
  private spatialReady = false;

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
      name: os.hostname(),
      caps: "screen",
      capv: "1",
      streamId: this.config.deviceId,
      signalingUrl: this.config.signalingUrl,
      platform: this.config.platform,
      sourceType: this.config.sourceType
    };
    if (this.spatialReady) txt.spatial = "1";
    this.service = this.bonjour.publish({
      name: "QuestPhoneStream Mac",
      type: "qps-device",
      protocol: "tcp",
      port: this.port,
      txt
    });
  }

  stop(): void {
    this.service?.stop();
    this.service = null;
    this.bonjour.destroy();
    this.server.close();
  }
}

function persistentDeviceId(): string {
  const file = path.join(app.getPath("userData"), "device-id.txt");
  try {
    const existing = fs.readFileSync(file, "utf8").trim();
    if (existing) return existing;
  } catch {}
  const id = `mac-${randomUUID()}`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, id, "utf8");
  return id;
}

let advertisement: UnifiedDeviceAdvertisement | null = null;

app.whenReady().then(async () => {
  const config: SenderConfig = {
    signalingUrl: process.env.QPS_SIGNALING_URL ?? "ws://192.168.1.9:8787",
    token: process.env.QPS_SIGNALING_TOKEN ?? "dev-token",
    deviceId: process.env.QPS_DEVICE_ID ?? persistentDeviceId(),
    questDeviceId: process.env.QPS_QUEST_DEVICE_ID ?? "quest-3s-001",
    sessionId: process.env.QPS_SESSION_ID ?? "local-session-001",
    platform: "macos",
    sourceType: "screen",
    width: 1920,
    height: 1080,
    fps: 30
  };

  advertisement = new UnifiedDeviceAdvertisement(config);
  await advertisement.start();

  ipcMain.handle("qps:get-config", () => config);
  ipcMain.handle("qps:list-sources", async () => {
    const sources = await desktopCapturer.getSources({ types: ["screen"], thumbnailSize: { width: 320, height: 180 } });
    return sources.map(source => ({ id: source.id, name: source.name, thumbnail: source.thumbnail.toDataURL() }));
  });
  ipcMain.on("qps:spatial-ready", (_event, ready: boolean) => advertisement?.setSpatialReady(Boolean(ready)));

  const window = new BrowserWindow({
    width: 720,
    height: 640,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false
    }
  });
  await window.loadFile(path.join(__dirname, "index.html"));
});

app.on("before-quit", () => advertisement?.stop());
app.on("window-all-closed", () => app.quit());
