# HefuVR Realtime Style Upgrade v1 — M2 Material and Daily Soft Furnishing

Incremental source+asset patch for the existing HefuVR Unity 2022.3.62 Built-in Android workspace. **Not a standalone Unity project**, and NOT a migration to URP/HDRP. Existing M0+M1 commit `96f8054` remains the source baseline; north entry, 2.677m actual CAD ceiling (~2.7m nominal), southern windows, room topology, fridge / kitchen orientation, 17 movable furniture IDs and Android controls stay unchanged.

## Implemented

**PBR material tuning** (`HefuM2MaterialFinish.cs`): Existing world-UV floor porcelain remains at 0.78m/tile; reducing excessively polished glossiness to .34, retaining subtle metallic .025 and 0.22 normal bump gives more restrained ceramics. Existing walnut table/coffee/cabinet textures and normals are unchanged as texture assets (world-scale 48cm on fitted cabinetry), but roughness and bump are tuned to .29-.33 gloss and .19-.21 normal. The Blender olive sofa leather shares 0.32 gloss and .30 bump with its new M1 seat cap (rather than a visually unrelated fabric). Original 3mm cabinet door chamfer mesh remains in place with ivory veneer .28 gloss. Brushed brass remains metalness .76 / gloss .55. Glass retains original Transparent blending, alpha and ZWrite OFF, while smoothing values improve its highlights. No global post-effect or fake image projection.

**Soft furnishing** (`HefuM2SoftDecorBuilder.cs`, `HefuM2DailyDecor.cs`): 1 small ivory ceramic planter, olive leaves, and stem cluster anchored to the ACTUAL `DiningSideboard`'s `Worktop` render bounds; 1 folded linen throw with sage-stitch accents parented under movable `Move_Sofa`. Unity primitive geometry is combined into **two render-only pooled meshes** / five reusable Standard materials; no new runtime light, collider, navigation blocker or saved furniture ID. The existing rug, curtain, sofa pillows and birthday balloons/cake remain untouched. Generated resources: `generated/M2Style` and the updated PBR assets in `generated/UpdatedPBR/`.

**Daily/Birthday independence**: Existing Birthday ON/OFF and Day/Night lighting buttons stay intact. `HefuMobileViewer` gains an independent **Daily decor ON/OFF** control on the same button row; `HefuM2DailyDecor` remembers visibility via a *separate* PlayerPrefs key `Hefu_M2_DailyDecorVisible_v1`. Unlike layout reset/save, turning decor on/off does not move sofa, camera, fridge, or other furniture and does NOT rewrite Birthday preference. `HefuM2QualityQA.Validate` builds a temporary Birthday scene and confirms its five anchors and 115+ decorations still work while Daily decor is disabled.

**Two fresh GPU bakes**: Because floor/wood/leather PBR materials changed, two separate Unity GPU sessions baked real Day/Birthday 256×256 GI EXR atlases, 46 LightProbes per mode. Existing three lamp-local lights, 16-toggle lightmap/SH cache, ceiling cutaway and south autumn sun remain unchanged.

## Reproducible integration

Start with `HefuVR_Render_Upgrade_v1.md` and the M0+M1 patch. Copy `unity-runtime/*.cs` to `Assets/Hefu/Scripts/`, `unity-editor/*.cs` to `Assets/Hefu/Editor/`, and copy the updated `.mat`, `.asset`, EXR and `.meta` files **into their original `Assets/Hefu/Generated/...` subdirectories** with their original asset GUIDs (the patch groups them for readability). The original project also needs its existing photo texture and Blender FBX resources. In `HefuAndroidBuilder.Prepare`, order matters:

```csharp
HefuM1StructureFinish.Apply(house.transform); // already immediately after scene atmosphere
// ... existing builder hierarchy ...
HefuPracticalLampBuilder.Apply(house.transform, cam);
HefuM2MaterialFinish.Apply(house.transform);
HefuM2SoftDecorBuilder.Apply(house.transform, viewer);
```

Then run each in a separate native Unity editor process: `HefuAndroidBuilder.Prepare` → `HefuDualLightingBake.BakeDay` → `BakeBirthday` → `FinalizeScene`; run `HefuM2QualityQA.Validate`, `HefuWallPBRRegression.Validate`, `HefuDualLightingQA.Validate`, `HefuUpgradeM0Capture.After`, `HefuM2DailyDecorCapture.Run`; finally `HefuUnityLibraryExport.ExportPrepared`, `scripts/prepare-private-unity-library.sh` and native Android `:app:testDebugUnitTest :app:assembleDebug` with `QPS_UNITY_LIBRARY_DIR`. Avoid combining GPU Lightmapper and Android export/build in one Unity process on Apple M4.

## QA and limits

The original PBR/glass/solar/sofa/CAD regressions passed; M2 QA verified both decor switches are independent, original PlayerPrefs and movable transforms unchanged, two visual batches, zero new colliders/lights, 17 furniture objects, 10 existing camera presets and dual baked GI present. All 5 camera comparisons (`docs/space-studio/qa/m2-material-soft-decor/`) use *precisely the same* positions, FOV, scene mode, resolution and afternoon sunlight as M1. A special OFF/ON cabinet capture demonstrates visual-only decor switching.

**The changes are intentionally nuanced**: same world geometry and mostly shared texture source, so full-frame mean brightness changes <1 gray value and the effect is strongest at closer viewing distances. The still-basic FBX/cabinet and baked reflections mean this is NOT an objective 80% photo reference match. Phone/Quest transparent sorting, actual touch UI, GPU frametime, thermo/performance and day/birthday smooth switching are NOT device-verified (ADB has no connected device). For genuine scene realism, prioritize M3 reflection probes/texture detail/quality tiers **after** a real phone performance baseline. Do not release publicly without separate user confirmation.
