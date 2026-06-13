# Test Plan

## Signaling

```bash
cd apps/signaling-server
pnpm install
pnpm test
pnpm build
```

Expected:

- Registration succeeds for Android and Quest.
- Invalid tokens are rejected.
- SDP and ICE relay to the target device id.

## Android

```bash
cd apps/android-agent
./gradlew assembleDebug
adb install -r app/build/outputs/apk/debug/app-debug.apk
adb shell am start -n com.questphonestream.agent/.MainActivity
adb logcat -s QuestPhoneStream WebRTC AndroidRuntime
```

Expected:

- Permission prompt appears.
- Foreground service notification appears.
- Logcat shows WebSocket connected, registered, session created, SDP offer sent, ICE state connected.

## Quest

```bash
scripts/adb-install-quest.sh
adb logcat -s QuestPhoneStream Unity WebRTC
```

Expected:

- Quest receives offer and sends answer.
- Panel material displays Android screen.
- ICE state reaches connected.

## Control

1. Enable Android Accessibility Service.
2. Click the Quest panel.
3. Confirm Android receives a `click` JSON command and performs a tap.

## Latency

Display a stopwatch on Android and observe the Quest panel through passthrough or recording. MVP target is below 300ms on local Wi-Fi.

