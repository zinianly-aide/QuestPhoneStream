import { afterEach, beforeEach, expect, test, vi } from "vitest";
import WebSocket from "ws";
import { startSignalingServer, type RunningSignalingServer } from "../src/index.js";
import { parseClientMessage } from "../src/protocol.js";

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
  send(message: Record<string, unknown>) {
    this.ws.send(JSON.stringify({ token: "test-secret-token", ...message }));
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
  async close() {
    const closed = new Promise<void>(resolve => this.ws.once("close", () => resolve()));
    this.ws.close();
    await closed;
  }
}

async function connect(role?: "android" | "quest", deviceId?: string) {
  const ws = new WebSocket(url);
  const client = new Client(ws);
  await new Promise<void>((resolve, reject) => { ws.once("open", () => resolve()); ws.once("error", reject); });
  if (role) {
    client.send({ type: "register", role, deviceId });
    expect(await client.next("registered")).toMatchObject({ role, deviceId });
  }
  return client;
}
async function pair() {
  return { android: await connect("android", "android-1"), quest: await connect("quest", "quest-1") };
}
async function create(android: Client, quest: Client, negotiationId?: string, sessionId = "session-1") {
  quest.send({ type: "create_session", sessionId, androidDeviceId: "android-1", questDeviceId: "quest-1", negotiationId });
  const q = await quest.next("session_created");
  const a = await android.next("session_created");
  expect(q).toEqual(a);
  expect(q).toMatchObject({ sessionId, androidDeviceId: "android-1", questDeviceId: "quest-1" });
  return q;
}

test("registered ACK precedes Quest session request and both peers receive the binding", async () => {
  const { android, quest } = await pair();
  const session = await create(android, quest, "attempt-1");
  expect(session.negotiationId).toBe("attempt-1");
  expect(server.sessions.size).toBe(1);
});

test("legacy clients without negotiationId still relay offer, answer and ICE in a bound session", async () => {
  const { android, quest } = await pair();
  await create(android, quest);
  android.send({ type: "offer", sessionId: "session-1", from: "android-1", to: "quest-1", sdp: "fresh-offer" });
  expect(await quest.next("offer")).toMatchObject({ sdp: "fresh-offer" });
  quest.send({ type: "answer", sessionId: "session-1", from: "quest-1", to: "android-1", sdp: "fresh-answer" });
  expect(await android.next("answer")).toMatchObject({ sdp: "fresh-answer" });
  quest.send({ type: "ice", sessionId: "session-1", from: "quest-1", to: "android-1", candidate: { candidate: "candidate:1" } });
  expect(await android.next("ice")).toMatchObject({ candidate: { candidate: "candidate:1" } });
});

test("bad token never receives registration ACK and is not logged", async () => {
  const quest = await connect();
  quest.send({ type: "register", role: "quest", deviceId: "quest-1", token: "wrong-secret" });
  expect(await quest.next("error")).toMatchObject({ code: "unauthorized" });
  expect(quest.messages.some(m => m.type === "registered")).toBe(false);
  expect(server.clients.size).toBe(0);
  expect(JSON.stringify(log.mock.calls)).not.toContain("wrong-secret");
  expect(JSON.stringify(log.mock.calls)).not.toContain("test-secret-token");
});

test("phone offline returns scoped peer_unavailable without creating a half-session", async () => {
  const quest = await connect("quest", "quest-1");
  quest.send({ type: "create_session", sessionId: "session-1", androidDeviceId: "android-1", questDeviceId: "quest-1", negotiationId: "attempt-1" });
  expect(await quest.next("peer_unavailable")).toMatchObject({ sessionId: "session-1", negotiationId: "attempt-1", deviceId: "android-1" });
  expect(server.sessions.size).toBe(0);
});

test("phone starts first; late Quest explicitly requests a fresh offer cycle", async () => {
  const android = await connect("android", "android-1");
  android.send({ type: "create_session", sessionId: "session-1", androidDeviceId: "android-1", questDeviceId: "quest-1" });
  await android.next("peer_unavailable");
  const quest = await connect("quest", "quest-1");
  await create(android, quest, "attempt-2");
  expect(quest.messages.some(m => m.type === "offer")).toBe(false); // Server never replays old SDP.
  android.send({ type: "offer", sessionId: "session-1", negotiationId: "attempt-2", from: "android-1", to: "quest-1", sdp: "new" });
  expect(await quest.next("offer")).toMatchObject({ negotiationId: "attempt-2", sdp: "new" });
});

test("Quest disconnect/re-register on the same session requests fresh negotiation, no old SDP replay", async () => {
  const { android, quest } = await pair();
  await create(android, quest, "old");
  await quest.close();
  expect(await android.next("peer_unavailable")).toMatchObject({ negotiationId: "old" });
  expect(server.sessions.size).toBe(0);
  const replacement = await connect("quest", "quest-1");
  await create(android, replacement, "new");
  for (const type of ["offer", "ice"]) {
    android.send({ type, sessionId: "session-1", negotiationId: "old", from: "android-1", to: "quest-1",
      sdp: "stale", candidate: { candidate: "stale" } });
    expect(await android.next("error")).toMatchObject({ code: "stale_or_invalid_session" });
  }
  expect(replacement.messages.some(m => m.type === "offer" || m.type === "ice")).toBe(false);
  android.send({ type: "offer", sessionId: "session-1", negotiationId: "new", from: "android-1", to: "quest-1", sdp: "fresh" });
  expect(await replacement.next("offer")).toMatchObject({ sdp: "fresh" });
  replacement.send({ type: "answer", sessionId: "session-1", negotiationId: "new", from: "quest-1", to: "android-1", sdp: "fresh-answer" });
  expect(await android.next("answer")).toMatchObject({ negotiationId: "new" });
});

test("missing negotiationId cannot bypass an upgraded session and old answer is rejected", async () => {
  const { android, quest } = await pair();
  await create(android, quest, "new");
  android.send({ type: "offer", sessionId: "session-1", from: "android-1", to: "quest-1", sdp: "old" });
  expect(await android.next("error")).toMatchObject({ code: "stale_or_invalid_session" });
  quest.send({ type: "answer", sessionId: "session-1", negotiationId: "old", from: "quest-1", to: "android-1", sdp: "old" });
  expect(await quest.next("error")).toMatchObject({ code: "stale_or_invalid_session" });
});

test("late Android bootstrap does not replace an upgraded Quest negotiation", async () => {
  const { android, quest } = await pair();
  await create(android, quest, "new");
  android.send({ type: "create_session", sessionId: "session-1", androidDeviceId: "android-1", questDeviceId: "quest-1" });
  expect(await android.next("session_created")).toMatchObject({ negotiationId: "new" });
  expect(server.sessions.get("session-1")?.negotiationId).toBe("new");
});

test("changing session invalidates previous session relays", async () => {
  const { android, quest } = await pair();
  await create(android, quest, "old");
  await create(android, quest, "new", "session-2");
  expect(server.sessions.has("session-1")).toBe(false);
  android.send({ type: "offer", sessionId: "session-1", negotiationId: "old", from: "android-1", to: "quest-1", sdp: "stale" });
  // There may already be a scoped session_replaced notification in the mailbox.
  expect((await android.next("error")).code).toBe("session_replaced");
  expect((await android.next("error")).code).toBe("stale_or_invalid_session");
});

test("changing Android device removes the old binding", async () => {
  const { android, quest } = await pair();
  const second = await connect("android", "android-2");
  await create(android, quest, "old");
  quest.send({ type: "create_session", sessionId: "session-2", androidDeviceId: "android-2", questDeviceId: "quest-1", negotiationId: "new" });
  expect(await second.next("session_created")).toMatchObject({ androidDeviceId: "android-2" });
  expect(server.sessions.has("session-1")).toBe(false);
});

test("reconnect can switch phones while retaining the session ID", async () => {
  const { android, quest } = await pair();
  const second = await connect("android", "android-2");
  await create(android, quest, "old");
  const replacement = await connect("quest", "quest-1");
  expect(await android.next("peer_unavailable")).toMatchObject({ negotiationId: "old" });
  replacement.send({ type: "create_session", sessionId: "session-1", androidDeviceId: "android-2", questDeviceId: "quest-1", negotiationId: "new" });
  expect(await second.next("session_created")).toMatchObject({ sessionId: "session-1", negotiationId: "new" });
  expect(await replacement.next("session_created")).toMatchObject({ androidDeviceId: "android-2" });
  android.send({ type: "offer", sessionId: "session-1", negotiationId: "old", from: "android-1", to: "quest-1", sdp: "stale" });
  expect(await android.next("error")).toMatchObject({ code: "stale_or_invalid_session" });
  second.send({ type: "offer", sessionId: "session-1", negotiationId: "new", from: "android-2", to: "quest-1", sdp: "fresh" });
  expect(await replacement.next("offer")).toMatchObject({ from: "android-2", sdp: "fresh" });
});

test("socket replacement preserves the newly registered identity", async () => {
  const { android } = await pair();
  const replacement = await connect("quest", "quest-1");
  await create(android, replacement, "replacement");
  expect(server.clients.get("quest-1")?.socket.readyState).toBe(WebSocket.OPEN);
});

test("unregistered and spoofed relay senders are rejected", async () => {
  const { android, quest } = await pair();
  await create(android, quest, "new");
  const unregistered = await connect();
  unregistered.send({ type: "offer", sessionId: "session-1", negotiationId: "new", from: "android-1", to: "quest-1", sdp: "spoof" });
  expect(await unregistered.next("error")).toMatchObject({ code: "not_registered" });
  quest.send({ type: "offer", sessionId: "session-1", negotiationId: "new", from: "android-1", to: "quest-1", sdp: "spoof" });
  expect(await quest.next("error")).toMatchObject({ code: "stale_or_invalid_session" });
});

test("malformed negotiation IDs fail validation", () => {
  expect(() => parseClientMessage(Buffer.from(JSON.stringify({ type: "create_session", negotiationId: {} })))).toThrow("invalid_negotiationId");
});

test("relay diagnostics never contain the token or raw SDP", async () => {
  const { android, quest } = await pair();
  await create(android, quest, "new");
  android.send({ type: "offer", sessionId: "session-1", negotiationId: "new", from: "android-1", to: "quest-1", sdp: "private-sdp-contents" });
  await quest.next("offer");
  const logs = JSON.stringify(log.mock.calls);
  expect(logs).not.toContain("test-secret-token");
  expect(logs).not.toContain("private-sdp-contents");
});
