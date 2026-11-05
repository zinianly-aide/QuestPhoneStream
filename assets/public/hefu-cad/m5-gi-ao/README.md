# HefuVR M5 · independent baked GI / AO test

Unity 2022.3 Built-in Render Pipeline source patch, NOT a standalone Unity project. Designed for the owner's existing HefuVR CAD-derived scene. Changes no Quest spatial protocol, phone control, XR packages or private workspace data.

The patch lives in unity-editor/HefuM5GiAoExperiment.cs and was exercised on the isolated local M5 Built-in project. It does not replace the M4 scene, original M4 lighting assets, or any original mesh/lightmap UV2. The two test scenes have their own LightingSettings and separately generated LightingData.

## Controlled comparison

| Version | Actual density | Atlas result | Baked AO | AO max distance | Baked probes |
|---|---:|---:|---|---:|---:|
| M4 baseline lighting | 12 texels/m | 256x256 (stable M4) | Off | 1m field (disabled) | 46 |
| M5 GI20 NoAO | 20 texels/m | 1 x 512x512 | Off | 0.65m | 46 |
| M5 GI20 AO | 20 texels/m | 1 x 512x512 | On | 0.65m | 46 |

Baked AO indirect/direct contribution is set to 0.55/0.05; disabling AO sets both to zero. Samples remain 48/128/48, max atlas 512px, padding 5. The max atlas is a CAP, not a promise that later 30/40 texels/m settings will remain one atlas. Dynamic furniture remains non-static, and the existing 46 probes are held constant to isolate the GI/AO variable.

## Steps in an isolated HefuVR editor project

Copy the sole C# script into Assets/Hefu/Editor/ inside an *isolated* Built-in HefuVR copy with the prepared Hefu_Mobile_Viewer.unity scene and complete M4 script dependencies. Never copy it directly into the Quest XR Unity project (the Hefu CAD scene and HefuVR runtime are separate).

Execute these methods via independent Unity processes, without -nographics for capture:

1. HefuVR.Editor.HefuM5GiAoExperiment.CaptureOriginal
2. HefuVR.Editor.HefuM5GiAoExperiment.Stage
3. HefuVR.Editor.HefuM5GiAoExperiment.BakeNoAO
4. HefuVR.Editor.HefuM5GiAoExperiment.CaptureNoAO
5. HefuVR.Editor.HefuM5GiAoExperiment.BakeAO
6. HefuVR.Editor.HefuM5GiAoExperiment.CaptureAO

Each method can be invoked with Unity -batchmode -quit -projectPath <isolated project> -executeMethod <method> -logFile <separate log>. Avoid bundling Prepare, Bake and Android Build in one Unity process, following the previously observed RadeonRays native failure.

Scenes are created under Assets/Hefu/Scenes/M5GiAoExperiments and lightmap settings under Assets/Hefu/Generated/M5GiAoExperiments. Captures: ABTestReports/M5_GI_AO/*.png, each at 2048x1365 using exact M4 camera positions. Capture DOES NOT call HefuFixtureLighting.ApplyMode because doing so would replace the experiment lightmaps with the stable M4 runtime preset.

## Verified 2026-11-05 local result

- Stage: two independent scene/settings candidates; PASS.
- NoAO bake: 8.4s; 1 x 512px lightmap; 46 probes; PASS.
- AO bake: 6.6s; 1 x 512px lightmap; 46 probes; PASS.
- Both LightingData paths point to independent candidate folders. Original scene and original LightingSettings SHA-256 unchanged.
- Five baseline + five NoAO + five AO actual Unity Metal captures: PASS (15 total).
- Five-view NoAO vs AO SSIM: 0.997622 / 0.997929 / 0.998789 / 0.998331 / 0.998541. Small difference is NOT evidence of a large quality gain.
- M4/NoAO comparisons vary by room: static lighting, geometry and old M4 lightmaps need controlled subjective review. SSIM isn't a photorealism or correctness score.

**Outcome:** M5 GI20 NoAO is a useful higher-resolution controlled baseline. AO 0.65m does not materially fix shadows under the 17 movable furniture roots; do not promote AO as the finished solution. Keep both experiments separate and use visual review first.

## Do not treat as release

- No Quest or phone ADB device available. No GPU frame timing/thermal measurements from hardware.
- Do not replace the shipped M4 Day/Birthday dual-GI presets with only the *daytime* M5 experiment. Before a release, both Day/Birthday require independently rebaked atlases and distinct probe SH data, plus switching QA.
- M5-A/M5-B snapshots are scene-specific artifacts retained locally on the SSD, not checked into this public GitHub repository.
- No changes to main branch, headset protocol, private scene files or unrelated pending voice controls.
- Next: visually inspect corner close-ups, benchmark GPU memory and dynamic contact shadows, decide whether 30 texels/m and AO refinement provide visible gains before trialing 40.

## M5-C: dense light probes + procedural contact blobs (2026-11-05)

EXPERIMENTAL PATCH; not promoted to a production or Quest scene.

Source files:
- unity-editor/HefuM5DenseLightProbeLayout.cs: duplicates the GI20 NoAO scene, preserves the original 46 probes and creates 256 probes near renderable furniture (levels 0.22/0.78/1.55m). Independent Lightmapping.Bake is required.
- unity-editor/HefuM5BlobContactShadows.cs: duplicates the baked dense scene, attaches one 128px feathered transparent quad to each of the 14 renderable movable furniture roots; the 3 empty furniture roots remain empty. All 14 use a shared mesh, material and texture; no collider, static GI, or shadow map.
- unity-runtime/HefuM5BlobShadowToggle.cs: exposes Apply(bool) and does not affect PlayerPrefs, moving furniture IDs or save/load.
- unity-editor/HefuM5DenseContactCapture.cs: five fixed M4 camera poses, 2048x1365, with blob OFF and ON.

In a separate, isolated HefuVR Built-in Unity 2022.3 project, copy editor scripts into Assets/Hefu/Editor/ and the runtime script into Assets/Hefu/Scripts/. Execute these editor entry points in separate Unity processes:
1. HefuVR.Editor.HefuM5DenseLightProbeLayout.StageDense
2. HefuVR.Editor.HefuM5DenseLightProbeLayout.BakeDense
3. HefuVR.Editor.HefuM5BlobContactShadows.Stage
4. HefuVR.Editor.HefuM5BlobContactShadows.QA
5. HefuVR.Editor.HefuM5DenseContactCapture.Run

Actual local results:
- Dense stage PASS (46 -> 256 probes, 14 renderable / 17 movable roots).
- Dense bake PASS (6.4 seconds, 1 x 512px atlas, 256 baked probes, new LightingData).
- Blob stage and toggle QA PASS. The first 81%-width patch was mostly hidden by furniture, so we also tested an expanded 1.42x patch with y=0.016m; the final source width is 1.15x the AABB footprint (0.81 x 1.42). This is visually subtle.
- Ten 2K Unity screenshots of the same baked scene with blob on/off. Wide candidate A/B SSIM: Hero 0.999999, Entry 0.999995, Dining 0.999984, BirthdayWall 1.0, Balcony 1.0. This is NOT evidence of a significant realism improvement; shadows remain largely occluded at low camera angles.
- The existing M4 Day/Birthday runtime SH arrays each have only 46 probes. A 256-probe scene must NOT be shipped until both presets have been rebaked with matching probe counts and switch-tested.
- No Quest hardware connected. No phone GPU/FPS/thermal data. Reflection probe 256px recapture is a later independent test, not part of this patch.
- No private model, screenshot, LightingData or full Unity project is uploaded to GitHub; local artifacts remain in /Volumes/ssd/HefuVR-BiRP-M5-ABTest-20261025/ABTestReports/M5_GI_AO/DenseContact/.

Revert: use the old M5_GI20_NoAO scene, or disable HefuM5BlobShadowToggle.Apply(false) within the experimental dense+blob scene.
