# HefuVR v3.3.5 — practical indoor exposure and realistic-scale cabinet finishes

This package is a **public source patch for an existing HefuVR Unity 2022.3 project**, not a stand-alone scene, and does not include the owner's private photo reference source textures. It is visually scoped to the previously aligned north-entry / north-fridge / south-balcony 2.7m CAD floor plan. Original imported CAD, collision, 17 movable furniture IDs and native signaling/controls remain untouched.

## Daylight exposure repair, without an extra real-time light

`unity-editor/HefuDaylightExposureBuilder.cs` executes **after** `HefuP2InteriorLightBuilder.Apply(house.transform)` and after atmospheric sun/sky is configured. It adjusts the two EXISTING nonshadow point-fill lights (`DiningWarmPendant_Unshadowed` .38→1.15, range 3.5→5.2m; `LivingWarmBounce_Unshadowed` .27→.92, range 4.4→5.4m), raises interior neutral ambient (.84/.814/.77), and sets the EXISTING autumn directional sun to 1.04. **Light count remains two fill points plus the original sun, shadowed-light count unchanged and mobile pixelLightCount=3**. The scene's `HefuStaticGiBake.Bake` must then run in a SEPARATE Unity process. It rebakes 1 GI lightmap + 46 dynamic Light Probes; the 17 movable furniture instances still use BlendProbes and never become static. The new illumination is a fixed warm-afternoon illustration, not a live room lux measurement, smart-home lamp setting, HDR exposure controller or physically accurate PBR calibration.

Actual Unity GPU screenshot luma values from the identical Mac Metal 960×640 cameras (ffmpeg signalstats 8-bit mean YAVG; not a subjective photographic quality score):

| Fixed viewpoint | v3.3.4 | v3.3.5 | Increase |
|---|---:|---:|---:|
| Dining | 91.32 | 127.50 | 39.6% |
| Living | 89.13 | 121.64 | 36.5% |
| South window | 74.10 | 99.26 | 34.0% |

These improvements come from adjusted ambient/direct fill and corresponding static GI rebake; room contrast remains visible. To compare use `docs/space-studio/qa/v3.3.5/*-Comparison.png`.

## Cabinet door bevels and woodgrain with meaningful metre scales

`unity-editor/HefuCabinetPBRFinish.cs` runs **after** the dining sideboard prefab, the five fitted maker cabinet instances, and existing PhotoMaterialFinish have been installed. It creates a **single shared draw-call mesh per cabinet root** (6 roots total) with shallow 3mm chamfered door-front geometry. Total is **60 original cabinet/wardrobe/kitchen fronts**, visually bevelled, not re-authored as physics meshes; geometry is attached to the respective root so the movable dining sideboard follows user transforms. On the east-facing sink return, the chamfer faces **-local X** rather than erroneously facing south. All original door meshes, original cabinet colliders and moving furniture IDs remain intact.

Generated `textures/IvoryVeneer_48cm.png` is a repeatable synthetic subtle lacquer/woodgrain at 512px; calculated cabinet door UVs repeat at 0.48m per texture tile. 8 original walnut-coloured dining sideboard niche/worktop objects have their meshes cloned (not their source prefab/CAD modified) and UVs regenerated in world metres at 0.48m per tile, using the existing approved `Assets/Hefu/Textures/PhotoReference/PhotoWalnut.png` and `PhotoWalnutNormal.png`. The latter are **not duplicated** in this pack: the project must contain them. Deterministic source generator is in `scripts/hefu/generate_cabinet_ivory_veneer.py` (local absolute output path, parameterize before using in remote CI). No entire screenshot is projected onto the geometry.

## QA and two-process Android export

`HefuExposureCabinetQA.Validate` PASS: 2 existing lights with bounded range and no shadows, ambient >.83, **6 pooled cabinet meshes / 1,020 new bevel vertices / 8 real-scale walnut meshes**, source collision unmodified, 17 original movable IDs, CAD ceiling retained.
`HefuRoofGiQA.Validate` PASS: 1 baked lightmap, 46 baked Light Probes, roof cutaway at eye level / overhead, no additional collision.
`HefuWallPBRRegression.Validate` PASS after *updating* legacy `HefuInteriorRefinementQA` range cap from 4.6m to 5.6m to reflect the intentional same-light range expansion; the QA still forbids additional lights and non-shadow assumptions remain checked. Existing PBR, glass, doors, sun, furniture, layout IDs remain present.

Integrate into the local `HefuAndroidBuilder.Prepare` after `HefuPhotoMaterialFinish.Apply`:

```csharp
HefuCabinetPBRFinish.Apply(house.transform);
```

And after `HefuP2InteriorLightBuilder.Apply`:

```csharp
HefuDaylightExposureBuilder.Apply(house.transform);
```

**Build as separate Unity processes**: 1) `HefuAndroidBuilder.Prepare`; 2) `HefuStaticGiBake.Bake`; 3) `HefuExposureCabinetQA.Validate` + `HefuRoofGiQA.Validate`; 4) `HefuUnityLibraryExport.ExportPrepared` (never regenerates the baked scene), then `scripts/prepare-private-unity-library.sh` and Gradle `:app:testDebugUnitTest :app:assembleDebug`. Combining Unity GPU bake with Android BuildPlayer in ONE process reproducibly crashed in v3.3.4 and is unsupported.

### Remaining work

Hardware Android / Quest FPS, exposure on actual OLED/LCD phone, tablet aspect ratio and Kitchen/Sideboard close-up screenshots require ADB connected. The kitchen cabinetry is still a CAD proxy beneath the chamfer overlays; new front bevel is only 3mm and very subtle at room scale. Photorealistic cabinet hardware, wear maps, ceiling design in other rooms, actual fixture light switching, reflection calibrations and calibrated manufacturer texture scans are outside v3.3.5.
