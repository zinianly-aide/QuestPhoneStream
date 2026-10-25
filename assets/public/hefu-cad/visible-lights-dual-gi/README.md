# HefuVR Android Spaces v3.3.6 — Visible lamp sources and two baked light states

Public Unity 2022.3 Built-in Render Pipeline source patch for the existing HefuVR CAD/mobile scene (not a standalone Unity project or image-based texture projection).

## All six requested features

1. **Dining pendants:** reuse the two existing ivory/brass pendant objects anchored above the actual CAD dining table. Both have a visibly emissive underside.
2. **Living floor lamp:** a physical brass/ivory linen-shade floor lamp by the sofa's SOUTH/WEST living corner, fixed outside the sofa footprint. It has a visible lit fabric shade and protruding diffuser. The first lamp position was actually behind original CAD wall 12; final location was moved to the room side and a `Physics.Raycast` regression ensures the luminaire is visible from the scene test camera.
3. **Emission:** built-in Standard shader with `_EMISSION` on three visible light surfaces, dimmer in Day and brighter in Birthday.
4. **Lighting originates at these fixtures:** 2 downward no-shadow 72° `SpotLight` objects parented to `DiningPendant_0/1`, plus one no-shadow `PointLight` inside the physical floor lamp shade. The previous 2 detached invisible fills are destroyed at generated scene creation. The existing sun is the only extra realtime shadow caster. Mobile `pixelLightCount=4` accounts for the sun + 3 luminaires.
5. **Two actual baked GI states:** separate Unity GPU bake processes produce **two different 128×128 EXR lightmaps**, direction/mask textures, and separately captured **46 LightProbe SH** data sets. `HefuLightingPresets` stores both; `HefuFixtureLighting.ApplyMode` swaps lightmap and probe data, physical fixture emission, lamp power, sun and ambient together at runtime. A QA script checks that both maps differ and >1,200 SH coefficients differ.
6. **Actual fixed-camera comparisons:** `docs/space-studio/qa/v3.3.6/` contains six real 960×640 Unity Metal GPU camera screenshots (Dining/Living/FloorLamp for both Day/Birthday) and three direct side-by-side comparisons. These are NOT AI generated reference images.

## Use existing UI rather than adding extra clutter

`HefuMobileViewer` retains the existing **Birthday ON/OFF**, **Day ambience**, and **Night ambience** controls. With Birthday ON, the theme button selects the warm Birthday mode or the natural Day mode; Birthday OFF returns Day. Restores saved PlayerPrefs; `HefuFixtureLighting` has NO competing `Start()` to override an existing Birthday selection.

## Integration and exact paths

Copy `unity-runtime/*.cs` into `Assets/Hefu/Scripts/`, `unity-editor/*.cs` into `Assets/Hefu/Editor/`, and `generated-dual-lighting/*` (with `.meta` files) into `Assets/Hefu/Generated/DualLighting/`. The base HefuVR CAD scene, Birthday scene, warm pendant, sofa and existing prior PBR/GI scripts must also exist. In local `HefuAndroidBuilder.Prepare`, immediately after `HefuWarmReferenceStyleBuilder.Apply(house.transform)` invoke `HefuPracticalLampBuilder.Apply(house.transform,cam)`.

**Use separate Unity processes** on the Mac M4. Never combine Bake with Android `BuildPipeline.BuildPlayer` in the same process (native Unity RadeonRays compositor crash observed previously). Use:

1. `HefuAndroidBuilder.Prepare`
2. `HefuDualLightingBake.BakeDay`
3. `HefuDualLightingBake.BakeBirthday`
4. `HefuDualLightingBake.FinalizeScene` (sets controller asset, restores Day, marks/saves the generated scene; prevents lost asset-reference bug)
5. `HefuDualLightingQA.Validate`, `HefuRoofGiQA.Validate`, `HefuWallPBRRegression.Validate`, plus GPU `HefuDualLightingRender.Run`
6. `HefuUnityLibraryExport.ExportPrepared` (validates current prebaked scene; must not regenerate it), then native Android host Gradle tests/build.

The `.asset` and `.meta` references in this pack correspond to the current local scene. The bundled EXRs are NOT a full Unity scene/lightmap-index binding replacement; regenerate bakes if geometry/light UV indices change.

## Acceptance / limitations

Actual Editor QA verifies the fixtures, emission, clear ray to floor lamp, zero invisible standalone fill lights, zero extra shadow-casting lights, two separate atlases, distinct light probe coeffs, 17 movable furniture and runtime Day/Birthday switching. Old CAD, sofa, windows, entry/kitchen glass, Luoyang autumn sun, PBR and roof regressions remain mandatory. Device FPS/thermals, Android light-probe upload, transparency sorting and live UI selection require a connected phone. No objective claim of 80% photorealism or measured lux. The floor lamp is a minimal visual placeholder and no new mobile bloom/post-processing is introduced.
