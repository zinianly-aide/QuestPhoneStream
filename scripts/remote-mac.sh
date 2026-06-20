#!/usr/bin/env bash
#=============================================================================
# QuestPhoneStream Remote Viewer — Mac 一键启动脚本
#=============================================================================
# 用法:
#   chmod +x scripts/remote-mac.sh
#   ./scripts/remote-mac.sh
#
# 前置条件:
#   - Node.js >= 18 (brew install node 或 https://nodejs.org)
#   - Tailscale 已安装并运行 (https://tailscale.com)
#   - 本仓库已 git clone 到 Mac
#=============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SIGNALING_DIR="$REPO_DIR/apps/signaling-server"
VIEWER_DIR="$REPO_DIR/apps/web-viewer"

# ---- 配置 (可修改) ----
SIGNALING_PORT="${SIGNALING_PORT:-8787}"
SIGNALING_TOKEN="${SIGNALING_TOKEN:-dev-token}"
HEARTBEAT_TIMEOUT_MS="${HEARTBEAT_TIMEOUT_MS:-45000}"
PING_INTERVAL_MS="${PING_INTERVAL_MS:-15000}"
# ---- WSS 配置 (可选, 手机无 cleartext 权限时启用) ----
CERT_FILE="${CERT_FILE:-}"
KEY_FILE="${KEY_FILE:-}"

# ---- 颜色 ----
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; NC='\033[0m'

banner() {
  echo ""
  echo -e "${BLUE}╔══════════════════════════════════════════╗${NC}"
  echo -e "${BLUE}║   QuestPhoneStream Remote Viewer        ║${NC}"
  echo -e "${BLUE}╚══════════════════════════════════════════╝${NC}"
  echo ""
}

check_prereqs() {
  local missing=0

  if ! command -v node &>/dev/null; then
    echo -e "${RED}[错误] 未找到 Node.js, 请安装: brew install node${NC}"
    missing=1
  else
    echo -e "${GREEN}[OK]${NC} Node.js $(node --version)"
  fi

  if ! command -v tailscale &>/dev/null; then
    echo -e "${YELLOW}[警告] 未找到 tailscale, 非局域网访问将不可用${NC}"
    echo -e "${YELLOW}       安装: https://tailscale.com/download${NC}"
  else
    local ts_ip=$(tailscale ip -4 2>/dev/null || echo "unknown")
    echo -e "${GREEN}[OK]${NC} Tailscale $(tailscale version --short 2>/dev/null || echo OK), IPv4: ${BLUE}${ts_ip}${NC}"
  fi

  return $missing
}

install_deps() {
  echo ""
  echo -e "${BLUE}[1/4] 安装信令服务器依赖...${NC}"
  cd "$SIGNALING_DIR"
  npm install --no-fund --no-audit 2>&1 | tail -1
  echo -e "${GREEN}  ✓ 依赖就绪${NC}"
}

gen_certs() {
  # 仅在 CERT_FILE 和 KEY_FILE 都已设置且文件存在时跳过
  if [ -n "${CERT_FILE:-}" ] && [ -n "${KEY_FILE:-}" ] && [ -f "$CERT_FILE" ] && [ -f "$KEY_FILE" ]; then
    echo -e "${GREEN}  ✓ 使用已有证书${NC}"
    return
  fi

  # 检查是否需要 WSS (通过 SIGNALING_USE_WSS 环境变量控制)
  if [ "${SIGNALING_USE_WSS:-0}" != "1" ]; then
    echo -e "${YELLOW}  ℹ WSS 未启用 (设置 SIGNALING_USE_WSS=1 以启用)${NC}"
    return
  fi

  echo ""
  echo -e "${BLUE}[2/4] 生成自签名证书...${NC}"

  local cert_dir="$REPO_DIR/certs"
  mkdir -p "$cert_dir"

  if ! command -v openssl &>/dev/null; then
    echo -e "${RED}[错误] 未找到 openssl${NC}"
    return 1
  fi

  # 获取本机 IP 用于 SAN
  local local_ip=$(tailscale ip -4 2>/dev/null || ifconfig en0 2>/dev/null | grep 'inet ' | awk '{print $2}' || echo "127.0.0.1")
  local alt_ips="IP.1 = 127.0.0.1"

  if [ "$local_ip" != "127.0.0.1" ]; then
    alt_ips="$alt_ips\nIP.2 = $local_ip"
  fi

  cat > "$cert_dir/san.cnf" << EOF
[req]
distinguished_name = req_distinguished_name
req_extensions = v3_req
prompt = no
[req_distinguished_name]
CN = $local_ip
[v3_req]
subjectAltName = @alt_names
[alt_names]
$alt_ips
EOF

  openssl req -x509 -newkey rsa:2048 \
    -keyout "$cert_dir/server.key" \
    -out "$cert_dir/server.crt" \
    -days 365 -nodes \
    -config "$cert_dir/san.cnf" 2>/dev/null

  export CERT_FILE="$cert_dir/server.crt"
  export KEY_FILE="$cert_dir/server.key"
  echo -e "${GREEN}  ✓ 证书已生成 ($local_ip)${NC}"
}

start_server() {
  echo ""
  echo -e "${BLUE}[3/4] 启动统一服务器 (信令 + 页面)...${NC}"

  cd "$REPO_DIR"

  SIGNALING_PORT="$SIGNALING_PORT" \
  SIGNALING_TOKEN="$SIGNALING_TOKEN" \
  HEARTBEAT_TIMEOUT_MS="$HEARTBEAT_TIMEOUT_MS" \
  PING_INTERVAL_MS="$PING_INTERVAL_MS" \
  SIGNALING_CERT="${CERT_FILE:-}" \
  SIGNALING_KEY="${KEY_FILE:-}" \
  VIEWER_DIR="$VIEWER_DIR" \
  npx tsx "$SCRIPT_DIR/../scripts/server-with-viewer.ts" &
  SERVER_PID=$!

  sleep 2

  if kill -0 $SERVER_PID 2>/dev/null; then
    echo -e "${GREEN}  ✓ 服务器已启动 (PID: $SERVER_PID)${NC}"
  else
    echo -e "${RED}[错误] 服务器启动失败${NC}"
    exit 1
  fi
}

print_info() {
  local ts_ip=$(tailscale ip -4 2>/dev/null || echo "未运行")
  local lan_ip=$(ifconfig en0 2>/dev/null | grep 'inet ' | awk '{print $2}' || echo "未检测")
  local proto="ws"
  [ "${SIGNALING_USE_WSS:-0}" = "1" ] && proto="wss"

  echo ""
  echo -e "${GREEN}╔══════════════════════════════════════════════════╗${NC}"
  echo -e "${GREEN}║             服务器已就绪                         ║${NC}"
  echo -e "${GREEN}╠══════════════════════════════════════════════════╣${NC}"

  if [ "$lan_ip" != "未检测" ]; then
    echo -e "${GREEN}║${NC} 局域网访问:"
    echo -e "${GREEN}║${NC}   浏览器: ${BLUE}http://${lan_ip}:${SIGNALING_PORT}${NC}"
    echo -e "${GREEN}║${NC}   手机:   ${BLUE}${proto}://${lan_ip}:${SIGNALING_PORT}${NC}"
  fi

  if [ "$ts_ip" != "未运行" ]; then
    echo -e "${GREEN}║${NC}"
    echo -e "${GREEN}║${NC} Tailscale 远程访问:"
    echo -e "${GREEN}║${NC}   手机:   ${BLUE}${proto}://${ts_ip}:${SIGNALING_PORT}${NC}"
  fi

  echo -e "${GREEN}║${NC}"
  echo -e "${GREEN}║${NC} Android App 配置:"
  echo -e "${GREEN}║${NC}   Signaling URL: ${BLUE}${proto}://${ts_ip}:${SIGNALING_PORT}${NC}"
  echo -e "${GREEN}║${NC}   Token:          ${YELLOW}${SIGNALING_TOKEN}${NC}"
  echo -e "${GREEN}║${NC}   Device ID:      android-phone-001"
  echo -e "${GREEN}║${NC}   Quest ID:       quest-3s-001"
  echo -e "${GREEN}║${NC}   Session ID:     local-session-001"
  echo -e "${GREEN}╚══════════════════════════════════════════════════╝${NC}"
}

open_browser() {
  echo ""
  echo -e "${BLUE}[4/4] 打开浏览器...${NC}"

  local view_url="http://127.0.0.1:${SIGNALING_PORT}"
  echo -e "  URL: ${BLUE}${view_url}${NC}"

  sleep 1
  open "$view_url" 2>/dev/null || xdg-open "$view_url" 2>/dev/null || echo -e "${YELLOW}  请手动打开浏览器访问: ${view_url}${NC}"
}

cleanup() {
  echo ""
  echo -e "${YELLOW}正在停止服务器...${NC}"
  kill $SERVER_PID 2>/dev/null || true
  wait $SERVER_PID 2>/dev/null || true
  echo -e "${GREEN}已停止${NC}"
}

# ---- 主流程 ----
banner
check_prereqs || exit 1
install_deps
gen_certs
start_server
print_info
open_browser

trap cleanup EXIT INT TERM

echo ""
echo -e "${GREEN}▶ 运行中, 按 Ctrl+C 停止${NC}"
wait $SERVER_PID
