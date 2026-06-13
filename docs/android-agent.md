# Android Agent

## Build

```bash
cd apps/android-agent
./gradlew assembleDebug
```

If Gradle reports `SDK location not found`, create `local.properties`:

```bash
cp local.properties.example local.properties
```

Then update `sdk.dir` to your Android SDK path. For Android Studio on macOS this is usually:

```properties
sdk.dir=/Users/<you>/Library/Android/sdk
```

If `./gradlew` reports that Gradle is missing, run:

```bash
brew install gradle
./gradlew assembleDebug
```

The included `gradlew` bootstrap first uses system Gradle. If Gradle is not installed, it downloads Gradle 8.10.2 into `apps/android-agent/.gradle/local`.

Required SDK packages:

```bash
sdkmanager "platform-tools" "platforms;android-35" "build-tools;35.0.0" "ndk;27.2.12479018"
```

## Install and Run

```bash
adb install -r app/build/outputs/apk/debug/app-debug.apk
adb shell am start -n com.questphonestream.agent/.MainActivity
adb logcat -s QuestPhoneStream WebRTC AndroidRuntime
```

## Runtime Flow

1. Enter signaling URL, token, Android device id, Quest device id, and session id.
2. Tap `Start Screen Stream`.
3. Grant Android screen capture permission.
4. The app starts `ScreenStreamService` as a foreground service.
5. WebRTC creates a screen video track and sends an offer to the Quest.

## Android 14+

Android 14 treats MediaProjection grants as one-shot. The app requests a new consent intent each time streaming starts and runs a foreground service with `mediaProjection` type.

## Control

Enable `QuestPhoneStream Control` in Accessibility Settings for Phase 2 commands.
