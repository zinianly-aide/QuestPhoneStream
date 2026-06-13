import { afterEach, beforeEach, expect, test } from "vitest";
import WebSocket from "ws";
import { startSignalingServer, type RunningSignalingServer } from "../src/index.js";

let server: RunningSignalingServer;
let url: string;

beforeEach(async () => {
  server = startSignalingServer({ host: "127.0.0.1", port: 0, token: "test-token", pingIntervalMs: 1000 });
  await onceListening(server.wss);
  const address = server.wss.address();
  if (!address || typeof address === "string") throw new Error("missing address");
  url = `ws://127.0.0.1:${address.port}`;
});

afterEach(async () => {
  await server.close();
});

test("registers devices and relays session creation", async () => {
  const android = await connect(url);
  const quest = await connect(url);

  android.send(JSON.stringify({ type: "register", token: "test-token", role: "android", deviceId: "android-1" }));
  quest.send(JSON.stringify({ type: "register", token: "test-token", role: "quest", deviceId: "quest-1" }));

  expect(await nextJson(android)).toMatchObject({ type: "registered", deviceId: "android-1" });
  expect(await nextJson(quest)).toMatchObject({ type: "registered", deviceId: "quest-1" });

  android.send(
    JSON.stringify({
      type: "create_session",
      token: "test-token",
      sessionId: "session-1",
      androidDeviceId: "android-1",
      questDeviceId: "quest-1"
    })
  );

  expect(await nextJson(android)).toMatchObject({ type: "session_created", sessionId: "session-1" });
  expect(await nextJson(quest)).toMatchObject({ type: "session_created", sessionId: "session-1" });

  android.close();
  quest.close();
});

test("forwards offer answer and ice candidates", async () => {
  const android = await connect(url);
  const quest = await connect(url);

  android.send(JSON.stringify({ type: "register", token: "test-token", role: "android", deviceId: "android-1" }));
  quest.send(JSON.stringify({ type: "register", token: "test-token", role: "quest", deviceId: "quest-1" }));
  await nextJson(android);
  await nextJson(quest);

  android.send(
    JSON.stringify({
      type: "offer",
      token: "test-token",
      sessionId: "session-1",
      from: "android-1",
      to: "quest-1",
      sdp: "v=0"
    })
  );
  expect(await nextJson(quest)).toMatchObject({ type: "offer", from: "android-1", sdp: "v=0" });

  quest.send(
    JSON.stringify({
      type: "ice",
      token: "test-token",
      sessionId: "session-1",
      from: "quest-1",
      to: "android-1",
      candidate: { candidate: "candidate:1", sdpMid: "0", sdpMLineIndex: 0 }
    })
  );
  expect(await nextJson(android)).toMatchObject({ type: "ice", candidate: { candidate: "candidate:1" } });

  android.close();
  quest.close();
});

test("rejects invalid token", async () => {
  const client = await connect(url);
  client.send(JSON.stringify({ type: "register", token: "wrong", role: "android", deviceId: "android-1" }));
  expect(await nextJson(client)).toMatchObject({ type: "error", code: "unauthorized" });
  client.close();
});

function connect(target: string): Promise<WebSocket> {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(target);
    ws.once("open", () => resolve(ws));
    ws.once("error", reject);
  });
}

function nextJson(ws: WebSocket): Promise<Record<string, unknown>> {
  return new Promise((resolve) => {
    ws.once("message", (data) => resolve(JSON.parse(data.toString("utf8"))));
  });
}

function onceListening(wss: RunningSignalingServer["wss"]): Promise<void> {
  return new Promise((resolve) => {
    if (wss.address()) resolve();
    else wss.once("listening", () => resolve());
  });
}

