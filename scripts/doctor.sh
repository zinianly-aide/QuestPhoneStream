#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "QuestPhoneStream doctor"
echo

check_cmd() {
  local name="$1"
  if command -v "$name" >/dev/null 2>&1; then
    echo "OK   $name: $(command -v "$name")"
  else
    echo "MISS $name"
  fi
}

check_path() {
  local label="$1"
  local path="$2"
  if [ -e "$path" ]; then
    echo "OK   $label: $path"
  else
    echo "MISS $label: $path"
  fi
}

check_cmd adb
check_cmd java
check_cmd gradle

ANDROID_SDK="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if [ -z "$ANDROID_SDK" ] && [ -f "$ROOT/apps/android-agent/local.properties" ]; then
  ANDROID_SDK="$(sed -n 's/^sdk.dir=//p' "$ROOT/apps/android-agent/local.properties" | head -1)"
fi
if [ -z "$ANDROID_SDK" ]; then
  ANDROID_SDK="$HOME/Library/Android/sdk"
fi

echo
check_path "Android SDK" "$ANDROID_SDK"
check_path "Android 36 platform" "$ANDROID_SDK/platforms/android-36/android.jar"
check_path "Android 36.1 platform" "$ANDROID_SDK/platforms/android-36.1/android.jar"
check_path "Android build-tools 36.1" "$ANDROID_SDK/build-tools/36.1.0"

echo
UNITY_PATHS=(
  "/Applications/Unity/Hub/Editor"
  "$HOME/Applications/Unity/Hub/Editor"
  "$HOME/ssd/Applications/Unity"
)
FOUND_UNITY=0
for base in "${UNITY_PATHS[@]}"; do
  if [ -d "$base" ]; then
    while IFS= read -r unity; do
      echo "OK   Unity Editor: $unity"
      FOUND_UNITY=1
    done < <(find "$base" -maxdepth 5 -path "*/Unity.app/Contents/MacOS/Unity" -type f 2>/dev/null)
  fi
done
if [ "$FOUND_UNITY" = "0" ]; then
  echo "MISS Unity Editor under Unity Hub Editor directories"
fi

echo
echo "Next checks:"
echo "  cd apps/signaling-server && pnpm test && pnpm build"
echo "  cd apps/android-agent && ./gradlew assembleDebug"
