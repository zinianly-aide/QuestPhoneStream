#!/usr/bin/env bash
# Reconnect an already paired Quest through the current Android TLS mDNS endpoint.
# Android assigns a new port after wireless debugging restarts; never cache ports.
set -euo pipefail
command -v adb >/dev/null || { echo 'adb not found' >&2; exit 2; }
adb start-server >/dev/null
SERVICES="$(adb mdns services)"
IP_FILTER="${QPS_QUEST_IP:-}"
ENDPOINTS="$(printf '%s\n' "$SERVICES" | awk -v ip="$IP_FILTER" '$2 == "_adb-tls-connect._tcp" && (ip == "" || index($3,ip ":") == 1) {print $3}')"
COUNT="$(printf '%s\n' "$ENDPOINTS" | sed '/^$/d' | wc -l | tr -d ' ')"
if [[ "$COUNT" != 1 ]]; then
  echo "Expected one paired wireless ADB mDNS service, found $COUNT." >&2
  echo 'Open Quest system Wireless Debugging, check that it is enabled and both devices share Wi-Fi.' >&2
  if [[ "$COUNT" -gt 1 ]]; then echo 'Set QPS_QUEST_IP to disambiguate the Quest host.' >&2; fi
  exit 3
fi
ENDPOINT="$(printf '%s\n' "$ENDPOINTS" | head -1)"
RESULT="$(adb connect "$ENDPOINT" 2>&1)"
echo "$RESULT"
if [[ "$(adb -s "$ENDPOINT" get-state 2>/dev/null)" != "device" ]]; then
  echo "Quest was discovered but ADB authentication was not established at $ENDPOINT" >&2
  exit 4
fi
printf 'Quest wireless ADB connected: %s\n' "$ENDPOINT"
printf 'For subsequent ADB commands, use: adb -s %s ...\n' "$ENDPOINT"
