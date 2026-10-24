#!/usr/bin/env bash
# Read-only report from a user-connected Android 10+ phone. Not Quest.
set -euo pipefail
SERIAL="${1:-}"
if [[ -z "$SERIAL" ]]; then
  echo 'Usage: scripts/diagnose-android-3d-crash.sh <PHONE_ADB_SERIAL>'
  echo 'Enable phone USB/Wireless debugging, then retry 3D and run this script.'
  exit 2
fi
ADB="${ADB:-adb}"
if ! "$ADB" -s "$SERIAL" get-state >/dev/null 2>&1;then
  echo "Phone ADB unavailable: $SERIAL";exit 3
fi
PRODUCT="$($ADB -s "$SERIAL" shell getprop ro.product.model | tr -d '\r')"
if [[ "$PRODUCT" == Quest* ]];then
  echo 'Refusing to treat Quest headset logs as the phone 3D crash';exit 4
fi
printf 'Android 3D startup crash report from %s (%s)\n' "$SERIAL" "$PRODUCT"
"$ADB" -s "$SERIAL" logcat -d -v time | grep -E \
 '(QPS3D|UnityPlayerActivity|FATAL EXCEPTION|AndroidRuntime|libunity.so|libil2cpp.so|UnsatisfiedLinkError|Fatal signal|Process com.questphonestream.agent|com.questphonestream.agent:spaces)' \
 | tail -n 100 || true
