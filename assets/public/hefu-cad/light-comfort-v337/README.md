# HefuVR Android Spaces v3.3.7 — Source-grounded light comfort and mobile GI optimization

Incremental patch for the existing HefuVR Unity 2022.3 built-in render-pipeline CAD-based mobile project (not a standalone Unity project). Builds on v3.3.6 public `/visible-lights-dual-gi`. These sources and rebaked day/birthday assets are public, per the home floorplan permission. No original CAD geometry, furniture IDs, signaling or XR files changed.

## Changes

1. **Brightness comes from the visible luminaires.** Existing two 79-degree downward pendant SpotLights and one living floor-lamp PointLight are maintained at their actual physical positions. Day spot intensity `0.85→1.14`, day floor point `.72→.90`; Birthday spots `1.80→2.12`, Birthday floor point `1.30→1.52`. Spot range `3.6→4.05m` improves table edge and nearby wall light. Both modes retain actual glowing diffuser/linen shade materials and have **zero detached invisible fill lights**. The existing autumn Luoyang sun remains the only directional shadow caster, with light sun strength `1.04→1.13` on Day.
2. **GI fidelity** increases `lightmapResolution` `7→12` texels/unit, samples `32/96→48/128`, environment samples `32→48`, max atlas stays 512. Result on Mac GPU: **1 genuine 256×256 lightmap per mode** (up from 128×128) and **46 real SH LightProbes per mode**; final bakes showed no overlapping secondary UV warnings. No realtime GI or additional shadowed lamps.
3. **Runtime allocations avoided.** `HefuFixtureLighting` unpacks 46-probe SH arrays and lightmap descriptor arrays exactly **once** on first use, caches an emission material instance per existing luminaire renderer, skips applying the same mode twice, and avoids expensive `LightProbes.Tetrahedralize()` on every button press because positions and tessellation are unchanged. New QA performs 16 Day/Birthday toggles and verifies the expensive cache only builds once and same-mode taps do not reapply lighting.
4. All camera, birthday decoration, persisted layout, CAD-derived ceiling cutaway, kitchen, window transparency and 17 movable-furniture behavior remain as before. No new Unity light component is introduced. `pixelLightCount=4` remains.

## Reproduce, test and build

Copy `unity-runtime/HefuFixtureLighting.cs` into `Assets/Hefu/Scripts/`, the four `unity-editor/` sources into `Assets/Hefu/Editor/`, and the new lighting assets *including identical `.meta` GUIDs* into `Assets/Hefu/Generated/DualLighting/`. The base v3.3.6 project, all other editor scripts, and the original CAD/scene must exist.

The Mac M4 Unity GPU compositor can native-crash if GPU Bake and Android Export run inside one process. **Run one fresh Unity process for each of:**

1. `HefuAndroidBuilder.Prepare`
2. `HefuDualLightingBake.BakeDay`
3. `HefuDualLightingBake.BakeBirthday`
4. `HefuDualLightingBake.FinalizeScene`
5. `HefuDualLightingQA.Validate`, `HefuRoofGiQA.Validate`, `HefuWallPBRRegression.Validate`, `HefuDualLightingRender.Run`
6. `HefuUnityLibraryExport.ExportPrepared`, then `scripts/prepare-private-unity-library.sh`, `:app:testDebugUnitTest :app:assembleDebug`.

`HefuDualLightingQA` uses the **actual saved generated Unity scene**, checks both real 256px atlas textures, SH probe differences, real lamps, raycast visibility, 17 movable objects and repeat mode switching. Proof in `docs/space-studio/qa/v3.3.7`: six 960×640 real Mac Unity screenshots, and six matched before/after comparisons against v3.3.6. The bundled EXR assets alone cannot relink a standalone new scene's UV/lightmap binding: rebake if you regenerate CAD topology.

## Known limitations

A brighter frame does not prove photorealism: floor lamp remains a minimal placeholder, fabric has no subsurface scattering, GI is fixed per mode, HDR bloom and manufacturer-grade cabinets are not in scope. Unity Metal camera YAVG is a brightness metric, not lux. **No Android ADB phone was attached during this run**, so actual Android FPS, thermal performance, GPU light counts, in-app transitions and persistent layouts are not device-verified. The system volume has roughly 3 GiB free; do not put further Unity cache artifacts there.
