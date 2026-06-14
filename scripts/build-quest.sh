#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="${UNITY:-/Users/anshi/ssd/Applications/Unity/Unity.app/Contents/MacOS/Unity}"
PROJECT="$ROOT/apps/quest-unity-client"
LOG="${UNITY_LOG:-/tmp/QuestPhoneStream-unity-build.log}"
JDK_HOME="${UNITY_JAVA_HOME:-$ROOT/.tools/jdk11/temurin-11.jdk/Contents/Home}"

export UNITY_JAVA_HOME="$JDK_HOME"
export JAVA_HOME="$JDK_HOME"
export JDK_HOME="$JDK_HOME"
export SKIP_JDK_VERSION_CHECK=1

"$UNITY" \
  -batchmode \
  -quit \
  -projectPath "$PROJECT" \
  -executeMethod QuestPhoneStream.Editor.QuestPhoneStreamBuild.BuildAndroid \
  -logFile "$LOG"

echo "Unity build log: $LOG"
echo "APK: $PROJECT/Builds/QuestPhoneStream.apk"
