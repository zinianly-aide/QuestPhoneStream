# Video Quality and WebRTC Lifecycle Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Render the phone stream with source-accurate unlit color and make Android WebRTC teardown safe across reconnects and service shutdown.

**Architecture:** A project-owned unlit, double-sided Unity shader replaces the lit Standard material, while a dedicated end-of-frame coroutine owns video blits. Android peer destruction is serialized through a small deferred-disposal queue; DataChannels are observer-detached and closed, then released by their owning PeerConnection before factory/EGL resources are finalized.

**Tech Stack:** Unity 6/C#, Unity WebRTC, Vulkan/OpenXR, Kotlin/Android, Google WebRTC 1.0.32006, JUnit 4.

---

### Task 1: Lock the video panel rendering contract

**Files:**
- Create: `apps/quest-unity-client/Assets/QuestPhoneStream/Shaders/PhoneVideo.shader`
- Create: `apps/quest-unity-client/Assets/QuestPhoneStream/Shaders/PhoneVideo.shader.meta`
- Modify: `apps/quest-unity-client/Assets/QuestPhoneStream/Materials/PhonePanel.mat`
- Modify: `apps/quest-unity-client/Assets/QuestPhoneStream/Editor/QuestPhoneStreamBuild.cs`
- Modify: `apps/quest-unity-client/Assets/QuestPhoneStream/Tests/PlayMode/XrUiRigTests.cs`

**Steps:**

1. Add a PlayMode test that creates `PhonePanel`, initializes the XR rig, and asserts identity rotation, `renderer.sharedMaterial == receiver.targetMaterial`, shader name `QuestPhoneStream/UnlitVideo`, and `_Cull == 0`.
2. Run Unity PlayMode tests and verify the missing shader/material contract fails.
3. Add a stereo-safe unlit texture shader with `Cull Off` semantics and point `PhonePanel.mat` to it.
4. Update scene generation to create/repair the material with the custom shader and identity panel rotation.
5. Remove the temporary 20-second pose logger while retaining one-shot setup diagnostics.
6. Run PlayMode tests and verify they pass.

### Task 2: Move video copies to the render-safe phase

**Files:**
- Modify: `apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/QuestWebRtcReceiver.cs`

**Steps:**

1. Add a dedicated video-render coroutine field and start it beside `WebRTC.Update()`.
2. Make `OnVideoReceived` retain the source texture and bind the target RenderTexture without an immediate Blit.
3. Implement a loop yielding `WaitForEndOfFrame`, then Blit the current source into the current target.
4. Mark/report the first media frame only after the first successful end-of-frame Blit.
5. Stop the render coroutine during destruction and build the Quest client to verify shader/C# compilation.

### Task 3: Serialize Android peer destruction

**Files:**
- Create: `apps/android-agent/app/src/main/java/com/questphonestream/agent/DeferredDisposalQueue.kt`
- Create: `apps/android-agent/app/src/test/java/com/questphonestream/agent/DeferredDisposalQueueTest.kt`
- Modify: `apps/android-agent/app/src/main/java/com/questphonestream/agent/WebRtcStreamer.kt`
- Modify: `apps/android-agent/app/build.gradle.kts`

**Steps:**

1. Add JUnit tests proving deferred values are disposed only when scheduled, duplicate values are ignored, and final release waits until the queue drains.
2. Run `./gradlew testDebugUnitTest` and verify the new type is missing.
3. Implement the generic main-thread deferred-disposal queue.
4. Replace every standalone `DataChannel.dispose()` with observer unregister/close and PeerConnection ownership.
5. Defer PeerConnection close/dispose through the queue and finalize capture/factory/EGL resources only after it drains.
6. Run `./gradlew testDebugUnitTest assembleDebug` and verify all tests/builds pass.

### Task 4: Device verification

**Files:**
- Build output: `apps/quest-unity-client/Builds/QuestPhoneStream.apk`
- Build output: `apps/android-agent/app/build/outputs/apk/debug/app-debug.apk`

**Steps:**

1. Install the Android APK, restart capture, and confirm MediaProjection is active.
2. Install the Quest APK over wireless ADB and reapply wake/proximity settings.
3. Confirm signaling, ICE, H.264 decoding, first end-of-frame Blit, and DataChannel attachment in logs.
4. Restart the Quest session repeatedly and confirm Android crash buffers contain no new WebRTC SIGSEGV/SIGABRT.
5. Capture a Quest screenshot and confirm the panel is visible in both eyes without scene-light tint.
6. Run signaling tests/build as a regression check and record final Git status.
