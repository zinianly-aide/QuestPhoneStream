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
  import("dotenv/config").catch(() => {});
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
      const preview = String(raw).slice(0, 5000);
      console.log(`[message] from ${remoteAddr}: ${preview}`);
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
        for (const client of clients.values()) client.socket.close();
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
  switch (message.type) {
    case "register": {
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
      if (client) client.lastSeenAt = Date.now();
      return;
    }
    case "create_session": {
      const session: Session = {
        sessionId: message.sessionId,
        androidDeviceId: message.androidDeviceId,
        questDeviceId: message.questDeviceId
      };
      sessions.set(message.sessionId, session);
      const payload: ServerMessage = { type: "session_created", ...session };
      sendTo(clients, message.androidDeviceId, payload, socket);
      sendTo(clients, message.questDeviceId, payload, socket);
      return;
    }
    case "offer":
    case "answer":
    case "ice": {
      const { token: _token, ...relay } = message;
      sendTo(clients, message.to, relay as RelayMessage, socket, message.sessionId);
      return;
    }
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
