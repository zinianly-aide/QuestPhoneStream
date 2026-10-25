# HefuVR v3.4.0-m3 — M3 Local Reflection & Three Quality Tiers

Incremental *public source+asset patch* for the pre-existing HefuVR Unity **2022.3.62f3c1 / Built-in Render Pipeline** Android mobile Unity Library, based on M2 commit `42192d9`. This is not a self-contained replacement for the base CAD scenes/models, not an HDRP/URP migration, and not a Quest-specific scene rewrite.

## Discovery and fix: earlier reflection volumes captured identical environments

The old `Hefu_Dining_64.exr` and `Hefu_Living_64.exr` have exactly the same SHA256. Attempting to upscale them with `Lightmapping.BakeReflectionProbe()` yielded two *again identical* `Hefu_M3_{Dining,Living}_128.exr` resources despite successful API return values. Merely increasing the reflection resolution would not improve localized reflections.

`unity-editor/HefuM3LocalCubemapBake.cs` now uses **editor-only actual Unity camera `RenderToCubemap()`** at the two CAD room anchors, with old sky-only reflection probes disabled during capture. The resulting **two scene-specific, different 128px six-face RGBA32 cubemap assets** are `reflections/Hefu_M3_Dining_Local128.asset` and `Hefu_M3_Living_Local128.asset`, with their Unity `.meta` identifiers; QA sampled 3,149 face pixels with meaningful color differences. Capture means: Dining average ~0.36450, Living average ~0.45929 (unitless RGB weighted averages), confirms NOT identical. Each probe remains `ReflectionProbeMode.Custom` with `boxProjection=true`, intensity 0.52, 128px texture and confined original room extents. There is **no mobile realtime cubemap capture camera or render loop**. The deprecated identical EXR candidates are deliberately not shipped in this patch.

## Three runtime visual quality tiers

`HefuM3QualityController.cs` attaches only to the Hefu Android `HefuMobileViewer`; existing Quest XR panel code, signaling, camera tracking and controls are never changed. User controls `Quality: Battery / Balanced / Quality` and `Show FPS` / `Hide FPS` appear above the existing Birthday and Daily toggles. Preference keys (`Hefu_M3_QualityTier_v1`, `Hefu_M3_PerformanceHUD_v1`) are independent from the Birthday and saved furniture positions.

| Option | Battery | Balanced (default) | Quality |
|---|---:|---:|---:|
| Antialiasing | Off | 2× | 4× |
| Realtime shadow distance | Shadows off | 9m | 13m |
| Shadow resolution | Medium (unused) | Medium | High |
| Pixel light budget | 2 | 3 | 4 |
| Custom offline local reflection probes | Off | On | On |
| Runtime realtime probe captures | **Never** | **Never** | **Never** |
| Texture mipmap limit | 1 (half size) | 0 (full) | 0 (full) |
| HDR camera | Off | Off | On |
| Requested framerate cap | 45 | 60 | 72 |

**Caps are maximum requests, not measured FPS**. The live HUD averages `Time.unscaledDeltaTime` frames across ≥0.75 seconds. It is a CPU-frame-time observational aid, *not* a GPU profiler / actual performance claim. Low disables reflection probes by toggling their components, without destroying textures or changing the physical materials or baked 256px GI/light probes. Medium / High re-enable their existing offline maps immediately. No changes to 17 movable furniture, layout IDs, floorplan, 2.677m CAD height (2.7m nominal), north kitchen/fridge, south windows, Birthday, Daily soft decor or Quest protocols.

## Reproducibility / integration

Start from the base CAD+M0+M1+M2 Unity project. Copy `unity-runtime/*.cs` to `Assets/Hefu/Scripts`, `unity-editor/*.cs` to `Assets/Hefu/Editor`, and `reflections/*` with matching `.meta` GUIDs to `Assets/Hefu/Generated/Reflections` (the patch directory is arranged by concern; copy to the original Unity paths). In the existing `HefuAndroidBuilder.Prepare` add after `HefuBakedRoomReflections.Apply(house.transform)`:

```csharp
HefuM3QualityBuilder.Apply(house.transform,viewer,cam);
```

`HefuMobileViewer.Start()` calls `quality.Initialize()` after layout/decor initialization, and the existing `OnGUI()` offers separate Quality + Perf controls. The `HefuBakedRoomReflections` builder uses the **two distinct Local128 `.asset` files**, not the identical EXRs. No Unity project global quality configuration needs to be permanently rewritten.

On Mac M4 avoid doing Unity GPU lightmap Bake and Unity Android Library BuildPipeline export in a *single Unity editor process*; previously this caused a native Unity crash. Run fresh processes for `HefuAndroidBuilder.Prepare` → `HefuDualLightingBake.BakeDay` → `BakeBirthday` → `FinalizeScene` → `HefuM3QualityQA.Validate`, `HefuM2QualityQA.Validate`, `HefuWallPBRRegression.Validate`, `HefuDualLightingQA.Validate`, `HefuRoofGiQA.Validate` → `HefuUpgradeM0Capture.After` and `HefuM3ProfileCapture.Run` → `HefuUnityLibraryExport.ExportPrepared` → `scripts/prepare-private-unity-library.sh` → Android native `:app:testDebugUnitTest :app:assembleDebug` using `QPS_UNITY_LIBRARY_DIR`.

## QA scope / limitations

`HefuM3QualityQA` opens the **actual generated phone scene**, checks two independent six-sided local cubemaps and significant different content (not just separate asset names), tests three profile transitions including OFF/ON reflection components and no realtime captures, confirms 17 movable furniture and unchanged UserPrefs; all tested without an Android connection. Three actual Mac Unity 960×640 screenshots in `docs/space-studio/qa/m3-quality-reflections/profiles/` show the quality modes in the identical `Hero_LivingDining` pose, and five M2→M3 fixed-camera frame comparisons are in the same QA directory. Unity Editor screenshots do NOT establish device FPS or a quantified similarity to the AI reference design. Future M4: capture saved layouts and visual quality on physical Android + Quest, optimize GPU bottlenecks, add high-quality export.

**Important:** the mobile Android renderer and headset Quest renderer have different project pipelines; these three quality presets are explicitly scoped to the **Android Hefu mobile Unity viewer**. Do not infer Quest FPS or Quest APK changes from the mobile changes. The Mac system Data disk is ~2.9GiB free, SSD ~668GiB, so further caches/builds belong on SSD.
