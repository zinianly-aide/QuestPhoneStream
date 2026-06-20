#!/bin/bash
# QuestPhoneStream — Remote Mac Environment
# Usage: ./scripts/remote-mac.sh
# Starts signaling-server (ws://0.0.0.0:8787) and web-viewer (http://0.0.0.0:3000)

set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TOKEN="dev-token"
PORT=8787
VIEWER_PORT=3000

echo "=== QuestPhoneStream Remote Mac Environment ==="
echo ""

# Kill existing processes
echo "[1] Clearing ports $PORT and $VIEWER_PORT ..."
lsof -ti :$PORT | xargs kill 2>/dev/null || true
lsof -ti :$VIEWER_PORT | xargs kill 2>/dev/null || true
sleep 1

# Detect IP
LAN_IP=$(ifconfig en1 2>/dev/null | grep 'inet ' | awk '{print $2}' || echo "127.0.0.1")
TAILSCALE_IP=$(tailscale ip -4 2>/dev/null || echo "")
echo "[2] Mac LAN IP: $LAN_IP"
if [ -n "$TAILSCALE_IP" ]; then
  echo "    Mac Tailscale IP: $TAILSCALE_IP"
else
  echo "    Tailscale: not configured (use LAN IP for local network)"
fi
echo ""

# Start signaling-server
echo "[3] Starting signaling-server on 0.0.0.0:$PORT ..."
cd "$REPO_ROOT/apps/signaling-server"
SIGNALING_HOST=0.0.0.0 SIGNALING_PORT=$PORT SIGNALING_TOKEN=$TOKEN node dist/index.js &
SIGNAL_PID=$!
sleep 2
if ! lsof -i :$PORT >/dev/null 2>&1; then
  echo "ERROR: signaling-server failed to start!"
  exit 1
fi
echo "    ✓ signaling-server running (PID $SIGNAL_PID)"
echo ""

# Start web-viewer
echo "[4] Starting web-viewer on 0.0.0.0:$VIEWER_PORT ..."
cd "$REPO_ROOT/apps/web-viewer"
npx vite --port $VIEWER_PORT --host 0.0.0.0 &
VIEWER_PID=$!
sleep 3
if ! lsof -i :$VIEWER_PORT >/dev/null 2>&1; then
  echo "ERROR: web-viewer failed to start!"
  exit 1
fi
echo "    ✓ web-viewer running (PID $VIEWER_PID)"
echo ""

# Summary
echo "=== Ready! ==="
echo ""
echo "📱 Android Phone Settings:"
echo "   Signaling URL: ws://$LAN_IP:$PORT"
if [ -n "$TAILSCALE_IP" ]; then
  echo "   (or Tailscale): ws://$TAILSCALE_IP:$PORT"
fi
echo "   Token:          $TOKEN"
echo "   Device ID:      android-phone-001"
echo "   Quest Device ID: quest-3s-001"
echo "   Session ID:     local-session-001"
echo ""
echo "🖥 Mac Viewer:"
echo "   http://$LAN_IP:$VIEWER_PORT"
echo "   (or http://localhost:$VIEWER_PORT)"
echo ""
echo "🛑 To stop: kill $SIGNAL_PID $VIEWER_PID"
echo "   Or run: lsof -ti :8787 :3000 | xargs kill"
