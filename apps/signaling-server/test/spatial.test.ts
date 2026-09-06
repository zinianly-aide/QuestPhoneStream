import { afterEach, beforeEach, expect, test, vi } from "vitest";
import WebSocket from "ws";
import { startSignalingServer, type RunningSignalingServer } from "../src/index.js";
import { parseClientMessage } from "../src/protocol.js";
import { negotiateSpatialVersion, SPATIAL_PROTOCOL_VERSION } from "../src/spatial.js";

let server: RunningSignalingServer;
let url: string;
let log: ReturnType<typeof vi.spyOn>;

beforeEach(async () => {
  log = vi.spyOn(console, "log").mockImplementation(() => {});
  server = startSignalingServer({ host: "127.0.0.1", port: 0, token: "test-secret-token", pingIntervalMs: 1000 });
  if (!server.wss.address()) await new Promise<void>(resolve => server.wss.once("listening", resolve));
  const address = server.wss.address();
  if (!address || typeof address === "string") throw new Error("missing address");
  url = `ws://127.0.0.1:${address.port}`;
});

afterEach(async () => {
  await server.close();
  log.mockRestore();
});

class Client {
  readonly messages: any[] = [];
  private notify: (() => void) | undefined;

  constructor(readonly ws: WebSocket) {
    ws.on("message", raw => { this.messages.push(JSON.parse(raw.toString())); this.notify?.(); });
  }

  sendLegacy(message: Record<string, unknown>) {
    this.ws.send(JSON.stringify({ token: "test-secret-token", ...message }));
  }

  sendSpatial(message: Record<string, unknown>) {
    this.ws.send(JSON.stringify(message));
  }

  async next(type: string): Promise<any> {
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.notify = undefined; reject(new Error("Missing " + type)); }, 1000);
      const check = () => {
        const index = this.messages.findIndex(message => message.type === type);
        if (index < 0) return;
        clearTimeout(timer);
        this.notify = undefined;
        resolve(this.messages.splice(index, 1)[0]);
      };
      this.notify = check;
      check();
    });
  }
}

async function connect(role?: "android" | "quest", deviceId?: string) {
  const ws = new WebSocket(url);
  const client = new Client(ws);
  await new Promise<void>((resolve, reject) => { ws.once("open", resolve); ws.once("error", reject); });
  if (role) {
    client.sendLegacy({ type: "register", role, deviceId });
    expect(await client.next("registered")).toMatchObject({ role, deviceId });
  }
  return client;
}

function envelope(type: string, source: string, target: string, id: string, payload: Record<string, unknown> = {}) {
  return {
    v: "1.0",
    id,
    type,
    source,
    target,
    sessionId: "",
    streamId: "",
    correlationId: "",
    timestamp: Date.now(),
    payload,
  };
}

test("device hello then capability discovery relays on the registered control connection", async () => {
  const android = await connect("android", "android-1");
  const quest = await connect("quest", "quest-1");

  quest.sendSpatial(envelope("device.hello", "quest-1", "android-1", "hello-1", {
    supportedVersions: ["1.0"],
  }));
  expect(await android.next("device.hello")).toMatchObject({ source: "quest-1", target: "android-1" });

  android.sendSpatial(envelope("device.hello", "android-1", "quest-1", "hello-2", {
    selectedVersion: "1.0",
  }));
  expect(await quest.next("device.hello")).toMatchObject({ payload: { selectedVersion: "1.0" } });

  quest.sendSpatial(envelope("device.capabilities.get", "quest-1", "android-1", "caps-get"));
  expect(await android.next("device.capabilities.get")).toMatchObject({ id: "caps-get" });

  const result = envelope("device.capabilities.result", "android-1", "quest-1", "caps-result", {
    capabilities: [{ name: "display.publish", version: "1.0" }],
  });
  result.correlationId = "caps-get";
  android.sendSpatial(result);
  expect(await quest.next("device.capabilities.result")).toMatchObject({
    correlationId: "caps-get",
    payload: { capabilities: [{ name: "display.publish" }] },
  });

  expect(server.sessions.size).toBe(0);
});

test("capability changed is a control-plane message and does not alter legacy session binding", async () => {
  const android = await connect("android", "android-1");
  const quest = await connect("quest", "quest-1");
  quest.sendLegacy({ type: "create_session", sessionId: "session-1", androidDeviceId: "android-1", questDeviceId: "quest-1", negotiationId: "n1" });
  await quest.next("session_created");
  await android.next("session_created");

  android.sendSpatial(envelope("device.capabilities.changed", "android-1", "quest-1", "changed-1", {
    capabilities: [{ name: "display.publish", state: { available: true, authorized: true, active: true } }],
  }));
  expect(await quest.next("device.capabilities.changed")).toMatchObject({ id: "changed-1" });
  expect(server.sessions.get("session-1")?.negotiationId).toBe("n1");
});

test("spatial messages require a registered socket even though envelopes contain no legacy token", async () => {
  const client = await connect();
  client.sendSpatial(envelope("device.hello", "quest-1", "android-1", "hello-unregistered"));
  expect(await client.next("protocol.error")).toMatchObject({
    correlationId: "hello-unregistered",
    payload: { code: "not_registered" },
  });
});

test("version negotiation accepts v1 and rejects unsupported offers", () => {
  expect(negotiateSpatialVersion(["2.0", "1.0"])).toBe(SPATIAL_PROTOCOL_VERSION);
  expect(negotiateSpatialVersion(["2.0"])).toBeNull();
  expect(negotiateSpatialVersion("1.0")).toBeNull();
});

test("unknown envelope fields are forward-compatible while unknown spatial types are rejected", () => {
  const valid = envelope("device.hello", "quest-1", "android-1", "hello-extra");
  const parsed = parseClientMessage(Buffer.from(JSON.stringify({ ...valid, futureField: { enabled: true } })));
  expect(parsed.type).toBe("device.hello");
  expect(() => parseClientMessage(Buffer.from(JSON.stringify({ ...valid, type: "camera.frame" })))).toThrow("unknown_spatial_type");
});
