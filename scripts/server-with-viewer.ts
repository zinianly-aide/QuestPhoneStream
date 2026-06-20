/**
 * 统一服务器: 信令 (WebSocket) + Web Viewer (HTTP)
 *
 * 一个端口同时处理:
 *   - WebSocket upgrade → 信令服务
 *   - HTTP GET /        → web-viewer/index.html
 *   - HTTP GET /health  → ok
 */
import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";
import { startSignalingServer } from "../apps/signaling-server/src/index.js";

const PORT = Number(process.env.SIGNALING_PORT ?? 8787);
const TOKEN = process.env.SIGNALING_TOKEN ?? "dev-token";
const CERT_PATH = process.env.SIGNALING_CERT;
const KEY_PATH = process.env.SIGNALING_KEY;
const VIEWER_DIR = process.env.VIEWER_DIR ?? join(import.meta.dirname, "../apps/web-viewer");
const HOST = process.env.SIGNALING_HOST ?? "0.0.0.0";

const viewerHtml = join(VIEWER_DIR, "index.html");

function getContentType(filePath: string): string {
  if (filePath.endsWith(".html")) return "text/html; charset=utf-8";
  if (filePath.endsWith(".js")) return "application/javascript; charset=utf-8";
  if (filePath.endsWith(".css")) return "text/css; charset=utf-8";
  if (filePath.endsWith(".png")) return "image/png";
  if (filePath.endsWith(".svg")) return "image/svg+xml";
  return "application/octet-stream";
}

function serveFile(res: ServerResponse, filePath: string): void {
  try {
    if (!existsSync(filePath)) {
      res.writeHead(404);
      res.end("Not Found");
      return;
    }
    const content = readFileSync(filePath);
    res.writeHead(200, {
      "Content-Type": getContentType(filePath),
      "Cache-Control": "no-cache",
    });
    res.end(content);
  } catch {
    res.writeHead(500);
    res.end("Internal Server Error");
  }
}

// ---- HTTP Server ----
const httpServer = createServer((req: IncomingMessage, res: ServerResponse) => {
  // Upgrade requests → WebSocket (handled by ws library)
  // Regular HTTP requests
  const url = req.url ?? "/";

  if (url === "/" || url === "/index.html") {
    serveFile(res, viewerHtml);
  } else if (url === "/health") {
    res.writeHead(200, { "Content-Type": "text/plain" });
    res.end("ok");
  } else {
    // Try serving from viewer directory
    const safePath = url.replace(/\.\./g, ""); // basic path traversal protection
    serveFile(res, join(VIEWER_DIR, safePath));
  }
});

httpServer.listen(PORT, HOST, () => {
  const proto = CERT_PATH && KEY_PATH ? "https" : "http";
  console.log(`[viewer] Web Viewer → ${proto}://${HOST}:${PORT}`);
});

// ---- Attach Signaling Server ----
const signalingServer = startSignalingServer({
  token: TOKEN,
  heartbeatTimeoutMs: Number(process.env.HEARTBEAT_TIMEOUT_MS ?? 45_000),
  pingIntervalMs: Number(process.env.PING_INTERVAL_MS ?? 15_000),
  server: httpServer,
});

// Stay alive
process.on("SIGINT", async () => {
  console.log("\nShutting down...");
  await signalingServer.close();
  httpServer.close();
  process.exit(0);
});

process.on("SIGTERM", async () => {
  await signalingServer.close();
  httpServer.close();
  process.exit(0);
});
