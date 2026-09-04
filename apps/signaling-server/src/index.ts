import https from "node:https";
import { WebSocket, WebSocketServer } from "ws";
import { parseClientMessage, serialize, type ClientMessage, type ClientRole, type RelayMessage, type ServerMessage } from "./protocol.js";

interface RegisteredClient {
  socket: WebSocket;
  role: ClientRole;
  deviceId: string;
  lastSeenAt: number;
}

interface Session {
  sessionId: string;
  androidDeviceId: string;
  questDeviceId: string;
  negotiationId?: string;
}

export interface SignalingServerOptions {
  host?: string;
  port?: number;
  token?: string;
  heartbeatTimeoutMs?: number;
  pingIntervalMs?: number;
  server?: https.Server;
}

export interface RunningSignalingServer {
  wss: WebSocketServer;
  clients: Map<string, RegisteredClient>;
  sessions: Map<string, Session>;
  close: () => Promise<void>;
}

const DEFAULT_TOKEN = "dev-token";

if (import.meta.url === `file://${process.argv[1]}`) {
  // Load dotenv only in standalone mode
  await import("dotenv/config");
  const certPath = process.env.SIGNALING_CERT;
  const keyPath = process.env.SIGNALING_KEY;
  const host = process.env.SIGNALING_HOST ?? "0.0.0.0";
  const port = Number(process.env.SIGNALING_PORT ?? 8787);

  if (certPath && keyPath) {
    import("node:fs").then((fs) => {
      const server = https.createServer({
        cert: fs.readFileSync(certPath),
        key: fs.readFileSync(keyPath),
      });
      server.listen(port, host, () => {
        console.log(`[QuestPhoneStream] WSS signaling server listening on wss://${host}:${port}`);
      });
      startSignalingServer({
        token: process.env.SIGNALING_TOKEN ?? DEFAULT_TOKEN,
        heartbeatTimeoutMs: Number(process.env.HEARTBEAT_TIMEOUT_MS ?? 45_000),
        pingIntervalMs: Number(process.env.PING_INTERVAL_MS ?? 15_000),
        server,
      });
    });
  } else {
    startSignalingServer({
      host,
      port,
      token: process.env.SIGNALING_TOKEN ?? DEFAULT_TOKEN,
      heartbeatTimeoutMs: Number(process.env.HEARTBEAT_TIMEOUT_MS ?? 45_000),
      pingIntervalMs: Number(process.env.PING_INTERVAL_MS ?? 15_000),
    });
  }
}

export function startSignalingServer(options: SignalingServerOptions = {}): RunningSignalingServer {
  const host = options.host ?? "0.0.0.0";
  const port = options.port ?? 8787;
  const token = options.token ?? DEFAULT_TOKEN;
  const heartbeatTimeoutMs = options.heartbeatTimeoutMs ?? 45_000;
  const pingIntervalMs = options.pingIntervalMs ?? 15_000;

  const clients = new Map<string, RegisteredClient>();
  const sessions = new Map<string, Session>();
  const existingServer = options.server;
  const wss = existingServer
    ? new WebSocketServer({ server: existingServer })
    : new WebSocketServer({ host, port });

  wss.on("connection", (socket) => {
    const remoteAddr = (socket as any)._socket?.remoteAddress || "unknown";
    console.log(`[connection] new client from ${remoteAddr}`);

    socket.on("message", (raw) => {
      try {
        const message = parseClientMessage(raw);
        if (message.token !== token) {
          console.log(`[auth] rejected invalid token from ${remoteAddr}`);
          send(socket, { type: "error", code: "unauthorized", message: "Invalid signaling token" });
          socket.close(1008, "unauthorized");
          return;
        }
        handleMessage(socket, message, clients, sessions);
      } catch (error) {
        console.log(`[error] parse failed from ${remoteAddr}:`, error instanceof Error ? error.message : error);
        send(socket, {
          type: "error",
          code: "bad_request",
          message: error instanceof Error ? error.message : "Invalid message"
        });
      }
    });

    socket.on("close", (code) => {
      console.log(`[close] client from ${remoteAddr} disconnected (code=${code})`);
      for (const [deviceId, client] of clients) {
        if (client.socket === socket) {
          console.log(`[close] removed registered client: ${deviceId}`);
          clients.delete(deviceId);
          removeSessions(deviceId, clients, sessions);
        }
      }
    });
  });

  const interval = setInterval(() => {
    const now = Date.now();
    for (const [deviceId, client] of clients) {
      if (now - client.lastSeenAt > heartbeatTimeoutMs) {
        client.socket.terminate();
        clients.delete(deviceId);
        removeSessions(deviceId, clients, sessions);
        continue;
      }
      if (client.socket.readyState === WebSocket.OPEN) client.socket.ping();
    }
  }, pingIntervalMs);

  wss.on("listening", () => {
    const address = wss.address();
    const resolvedPort = typeof address === "object" && address ? address.port : port;
    console.log(`[QuestPhoneStream] signaling server listening on ws://${host}:${resolvedPort}`);
  });

  return {
    wss,
    clients,
    sessions,
    close: () =>
      new Promise((resolve, reject) => {
        clearInterval(interval);
        for (const socket of wss.clients) socket.terminate();
        wss.close((error) => (error ? reject(error) : resolve()));
      })
  };
}

function handleMessage(
  socket: WebSocket,
  message: ClientMessage,
  clients: Map<string, RegisteredClient>,
  sessions: Map<string, Session>
): void {
  const sender = [...clients.values()].find(client => client.socket === socket);
  if (message.type !== "register" && !sender) {
    send(socket, { type: "error", code: "not_registered", message: "Register first" });
    return;
  }
  // 调试日志:记录所有消息类型和关键字段,帮助排查协商失败问题。
  // 上线前可移除。
  switch (message.type) {
    case "register":
      console.log(`[register] role=${message.role} deviceId=${message.deviceId}`);
      break;
    case "create_session":
      console.log(`[create_session] sessionId=${message.sessionId} android=${message.androidDeviceId} quest=${message.questDeviceId}`);
      break;
    case "offer":
      console.log(`[offer] session=${message.sessionId} from=${message.from} to=${message.to} sdpLen=${message.sdp.length}`);
      break;
    case "answer":
      console.log(`[answer] session=${message.sessionId} from=${message.from} to=${message.to} sdpLen=${message.sdp.length}`);
      break;
    case "ice":
      console.log(`[ice] session=${message.sessionId} from=${message.from} to=${message.to}`);
      break;
    case "heartbeat":
      // 太频繁,不打印
      break;
  }
  switch (message.type) {
    case "register": {
      if (sender && (sender.deviceId !== message.deviceId || sender.role !== message.role)) {
        send(socket, { type: "error", code: "identity_conflict", message: "Reconnect to change identity" });
        return;
      }
      const previous = clients.get(message.deviceId);
      if (previous && previous.socket !== socket) {
        // Invalidate sessions before replacing the socket; old close callbacks cannot delete the new identity.
        removeSessions(message.deviceId, clients, sessions);
        previous.socket.close(1000, "replaced");
      }
      clients.set(message.deviceId, {
        socket,
        role: message.role,
        deviceId: message.deviceId,
        lastSeenAt: Date.now()
      });
      send(socket, { type: "registered", role: message.role, deviceId: message.deviceId });
      return;
    }
    case "heartbeat": {
      const client = clients.get(message.deviceId);
      if (client?.socket === socket) client.lastSeenAt = Date.now();
      return;
    }
    case "create_session": {
      if ((sender!.role === "quest" ? message.questDeviceId : message.androidDeviceId) !== sender!.deviceId) {
        send(socket, { type: "error", code: "invalid_session", message: "Session identity mismatch" });
        return;
      }
      const android = clients.get(message.androidDeviceId);
      const quest = clients.get(message.questDeviceId);
      for (const [peer, role, deviceId] of [[android, "android", message.androidDeviceId], [quest, "quest", message.questDeviceId]] as const) {
        if (!peer || peer.socket.readyState !== WebSocket.OPEN) {
          send(socket, { type: "peer_unavailable", sessionId: message.sessionId, negotiationId: message.negotiationId, deviceId });
          return;
        }
        if (peer.role !== role) {
          send(socket, { type: "error", code: "invalid_session", message: "Peer role mismatch" });
          return;
        }
      }
      const existing = sessions.get(message.sessionId);
      if (existing && (existing.androidDeviceId !== message.androidDeviceId || existing.questDeviceId !== message.questDeviceId)) {
        send(socket, { type: "error", code: "session_conflict", message: "Session belongs to other devices" });
        return;
      }
      // A late Android bootstrap must not replace a Quest-initiated negotiation.
      if (existing && sender!.role === "android") {
        send(socket, { type: "session_created", ...existing });
        return;
      }
      const session: Session = {
        sessionId: message.sessionId,
        androidDeviceId: message.androidDeviceId,
        questDeviceId: message.questDeviceId,
        negotiationId: message.negotiationId
      };
      // One active stream per phone/Quest. Old sessions cannot keep routing after a switch.
      for (const [id, old] of sessions) {
        if (id !== session.sessionId && (old.androidDeviceId === session.androidDeviceId || old.questDeviceId === session.questDeviceId)) {
          sessions.delete(id);
          for (const target of [old.androidDeviceId, old.questDeviceId]) {
            const client = clients.get(target);
            if (client) send(client.socket, { type: "error", code: "session_replaced", message: "Session replaced", sessionId: id, negotiationId: old.negotiationId });
          }
        }
      }
      sessions.set(message.sessionId, session);
      const payload: ServerMessage = { type: "session_created", ...session };
      sendTo(clients, message.questDeviceId, payload, socket);
      sendTo(clients, message.androidDeviceId, payload, socket);
      return;
    }
    case "offer":
    case "answer":
    case "ice": {
      const session = sessions.get(message.sessionId);
      const expectedTo = sender!.role === "android" ? session?.questDeviceId : session?.androidDeviceId;
      const expectedFrom = sender!.role === "android" ? session?.androidDeviceId : session?.questDeviceId;
      if (!session || message.from !== sender!.deviceId || message.from !== expectedFrom || message.to !== expectedTo ||
          message.negotiationId !== session.negotiationId ||
          (message.type === "offer" && sender!.role !== "android") ||
          (message.type === "answer" && sender!.role !== "quest")) {
        send(socket, { type: "error", code: "stale_or_invalid_session", message: "Relay rejected", sessionId: message.sessionId, negotiationId: message.negotiationId });
        return;
      }
      const { token: _token, ...relay } = message;
      sendTo(clients, message.to, relay as RelayMessage, socket, message.sessionId);
      return;
    }
  }
}

function removeSessions(deviceId: string, clients: Map<string, RegisteredClient>, sessions: Map<string, Session>): void {
  for (const [id, session] of sessions) {
    if (session.androidDeviceId !== deviceId && session.questDeviceId !== deviceId) continue;
    sessions.delete(id);
    const peerId = session.androidDeviceId === deviceId ? session.questDeviceId : session.androidDeviceId;
    const peer = clients.get(peerId);
    if (peer) send(peer.socket, { type: "peer_unavailable", deviceId, sessionId: id, negotiationId: session.negotiationId });
  }
}

function sendTo(
  clients: Map<string, RegisteredClient>,
  deviceId: string,
  payload: ServerMessage,
  requester: WebSocket,
  sessionId?: string
): void {
  const target = clients.get(deviceId);
  if (!target || target.socket.readyState !== WebSocket.OPEN) {
    send(requester, { type: "peer_unavailable", sessionId, deviceId });
    return;
  }
  send(target.socket, payload);
}

function send(socket: WebSocket, payload: ServerMessage): void {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(serialize(payload));
  }
}
