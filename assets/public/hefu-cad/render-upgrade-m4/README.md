# HefuVR M4 — Five fixed-view 2K captures, exhibition board and device benchmark

Incremental **source patch for the existing Unity 2022.3.62 Built-in HefuVR Android scene**, based on M3 commit `1bed708`. This is not a standalone Unity project or a Quest rebuild. The public source pack intentionally excludes the private working Unity scene and APK. Original CAD topology, nominal 2.7m height (actual ceiling 2.677m), 17 furniture IDs, M1 seats/glass, M2 daily decor, Birthday, and M3 quality tiers / real offline cubemaps are all retained.

## Android functionality (implemented, not hardware-verified yet)

`HefuM4PresentationPlan` declares the **same five** physically located 1.55m cameras as the audited M0 baselines, in the same order (Hero_LivingDining, Entry_North, Dining_Cabinet, Living_BirthdayWall, Balcony_South). They have FOV=60 degrees and 2048x1365 output, independent of existing ten selectable app viewpoints.

`HefuM4Presentation` uses **a temporary offscreen camera**, not the Android touch-orbit camera, to sequentially render five UI-free RGB PNG images. One GPU capture at a time, with RenderTexture and CPU textures released after each. No furniture positions, user orbit coordinates, saved layout or birthday preferences are changed. Exports are saved to `Application.persistentDataPath/Exhibits/M4_<UTC>/` and, on Android 10+ if MediaStore permits it, to **Gallery > Pictures/HefuVR** using the already existing photo module. A `manifest.csv` stores exact eye/target positions, field of view, resolution and quality tier. MediaStore failure leaves local files intact. The global CAD cutaway roof is restored to the user's actual camera after each frame. `Export 5 views` is an explicit UI button, not an automatic capture on startup.

`Bench 3 tiers` is a separate explicit opt-in with 1s warmup + 6s sample **per Battery / Balanced / Quality** (roughly 21s total not guaranteed). It records actual on-device `Time.unscaledDeltaTime` CPU/render-loop frame intervals, mean sampled FPS, p50/p95/p99 frame milliseconds, worst frame milliseconds, Mono and total allocated managed memory, Android device and GPU names. Writes an independent CSV in `Application.persistentDataPath/M4Benchmarks`. It temporarily changes M3 quality tiers but ALWAYS restores the user tier in `finally`, and does not change stored settings, layout or birthday scene. This is a first-pass frame pacing test, **NOT true GPU frame time or power/thermal profiling**. Benchmarks must be run on a connected real device and interpreted there; no fake measurements supplied.

`HefuMobileViewer` adds two buttons in the same existing tools UI without changing the other app controls: **Export 5 views** and **Bench 3 tiers**. Shows transient progress and persistent completion/error status. Existing `HefuPhotoComparison.TrySaveGallery` was made `internal static` for code reuse, preserving the original single-shot photo import and compare features. `HefuAndroidBuilder.Prepare` adds the M4 component after M3 quality; no changes to WebRTC, Android control protocol or Quest assets.

## Reproduce and verify

Copy `unity-runtime/*.cs` into `Assets/Hefu/Scripts` and `unity-editor/*.cs` into `Assets/Hefu/Editor`. The base M0-M3 source, existing CAD scene and Unity generated assets must already exist.

1. In a clean Unity batch process run `HefuAndroidBuilder.Prepare`.
2. In **separate** Unity processes run `HefuDualLightingBake.BakeDay`, `BakeBirthday`, and `FinalizeScene` to avoid the previous Apple M4-chip Unity compositor crash if bake/build share one process.
3. Run `HefuM4PresentationQA.Validate`, `HefuWallPBRRegression.Validate`, `HefuM2QualityQA.Validate`, `HefuM3QualityQA.Validate`, `HefuDualLightingQA.Validate`, and `HefuRoofGiQA.Validate`; all passed in this run.
4. `HefuM4ExhibitEditor.Export` generates five **real Unity GPU captures** 2048×1365 at matching baseline cameras without saving changes back to the source scene. `tools/make-poster.swift <five-shot-directory> <output.png>` uses native macOS AppKit to compose an editorial 3200×1700 collage from the real screenshots; this is **not an AI render**. Assets and proof: `docs/space-studio/qa/m4-presentation-bench`.
5. Run `HefuUnityLibraryExport.ExportPrepared` in another Unity process, then `scripts/prepare-private-unity-library.sh`, and Gradle `:app:testDebugUnitTest :app:assembleDebug` with `QPS_UNITY_LIBRARY_DIR`. Android version code 18 / versionName 3.4.1-m4.

## Device checklist — pending, not a claim of success

- USB/Wi-Fi ADB Android phone; install with `adb install -r <M4.apk>` without clearing existing saved layouts. Test Export5, look for 5 images in gallery and `manifest.csv` at the app persistent path, view all walls/windows/kitchen/sofa and validate correct 2K orientation.
- Set identical starting shot and Day/Birthday mode for each tier. Run `Bench 3 tiers`, then retrieve `M4Benchmarks/*.csv` through `adb` (e.g. under `/sdcard/Android/data/com.questphonestream.agent/files/`). Validate p95 frame time, UI latency, memory growth, and real heating over 10+ minutes; allow thermal stabilization before comparing.
- Verify original Day/Birthday and Daily decoration switches, photo import picker, 17 furniture Save/Load/Reset and Back to App still work. Repeat on Quest separately if user wants Quest M4; mobile M4 does not alter headset XR shaders or frame budget.
- `Application.targetFrameRate` 45/60/72 is a requested ceiling, not measured FPS. Mac screenshots and Editor QA do not verify on-device performance, exact picture similarity or whether the Gallery permission succeeds.
