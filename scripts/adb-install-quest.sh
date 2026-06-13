#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APK="${1:-$ROOT/apps/quest-unity-client/Builds/QuestPhoneStream.apk}"

adb devices
adb install -r "$APK"
adb shell monkey -p com.questphonestream.quest 1

