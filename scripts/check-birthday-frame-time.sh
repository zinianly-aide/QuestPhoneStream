#!/usr/bin/env bash
# Read-only Quest 3S birthday-scene performance audit. Never clears logcat,
# wakes the headset or changes app, scene, permissions, saved data or settings.
set -euo pipefail
SERIAL="${1:-340YC10G7J021L}"
ADB="${ADB:-adb}"
PKG='com.questphonestream.quest'
if ! "$ADB" -s "$SERIAL" get-state >/dev/null 2>&1; then
  echo "QUEST_OFFLINE serial=$SERIAL";exit 2
fi
FOCUS="$($ADB -s "$SERIAL" shell dumpsys activity activities | grep 'mFocusedApp=' | head -1 || true)"
if [[ "$FOCUS" == *vrlockscreen* ]]; then
  echo 'QUEST_LOCKED: system FPS is not QuestPhoneStream FPS. Unlock Quest and open the birthday room.'
  exit 3
fi
PID="$($ADB -s "$SERIAL" shell pidof "$PKG" | tr -d '\r' || true)"
if [[ -z "$PID" ]]; then
  echo 'QPS_APP_NOT_RUNNING: open QuestPhoneStream > Spaces > HefuVR Birthday (ZUK synced) > Explore room.'
  exit 4
fi
printf 'QPS_PROCESS pid=%s; Scene-only [BirthdayPerf] UnityUpdateFPS, NOT compositor/GPU FPS\n' "$PID"
REPORTS="$($ADB -s "$SERIAL" logcat -d -v time --pid="$PID" 2>/dev/null | grep '\[BirthdayPerf\]' | tail -n 12 || true)"
if [[ -z "$REPORTS" ]]; then
  echo 'NO_SCENE_STATS: leave the birthday room open in the foreground for at least 10 seconds.'
  exit 5
fi
printf '%s\n' "$REPORTS"
