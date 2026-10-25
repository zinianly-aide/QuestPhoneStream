# HefuVR M0 + M1 Real-Time Render Style Upgrade v1

**Public patch source** for the existing private local HefuVR Unity 2022.3 Built-in mobile project. This is NOT a self-contained clone of the author's entire original CAD Unity project. Reference plan was read from the user's HefuVR_Render_Upgrade_v1.md Library file and matched to the existing repo's AGENTS.md. No HDRP switch, no URP migration and no Android control-protocol change.

## Implementation / safety

- `unity-editor/HefuUpgradeM0Capture.cs` — before/after capture five exact physical 1.55m cameras (FOV60, 960×640), editor only, no scene writes.
- `unity-editor/HefuM1StructureFinish.cs` — applies after the existing `HefuSceneAtmosphereBuilder.Apply`. It tweaks transparent architectural glass alphas, preserving the two center-visible south CAD sheets and all original structural/collision meshes; increases visibility through kitchen glass without changing hinges or controls. Creates one render-only 3-seat cushion/piping overlay attached to the movable sofa root, preserving its Blender FBX and original collider.
- `unity-runtime/HefuPhotoViewpoints.cs` — appends `hero_living_dining` as the **tenth** optional view without changing the first nine IDs or indices. Actual camera height 1.55m, facing south, no Quest XR camera override.
- `unity-editor/HefuM1AcceptanceQA.cs` and updated `HefuPhotoViewsQA.cs` — dedicated regression gates for physically correct south-day sun direction, transparent kitchen and south panes, moving sofa seat geometry, real pendants + light emission, GI and 10 fixed view IDs.
- `unity-editor/HefuAndroidBuilder.cs` — calls the M1 finish after atmosphere. It generates the *new* `Hefu_Mobile_Viewer.unity` scene, leaving original `Hefu_DesignLab.unity` untouched.
- `generated/` contains resulting 3-seat pooled mesh and updated material assets as reference. They cannot be dropped into an unrelated Unity project without matching upstream scene/asset GUID dependencies.

## Reproduce

In the existing local Unity project, use separate Unity editor processes on Mac M4 to avoid the previous GPU lightmapper compositor native crash:

1. `HefuUpgradeM0Capture.Before` **before** changing any scene assets
2. `HefuAndroidBuilder.Prepare` (new M1 scripts must be in place)
3. `HefuDualLightingBake.BakeDay`
4. `HefuDualLightingBake.BakeBirthday`
5. `HefuDualLightingBake.FinalizeScene`
6. `HefuM1AcceptanceQA.Validate`, `HefuPhotoViewsQA.Validate`, `HefuWallPBRRegression.Validate`, `HefuDualLightingQA.Validate`, `HefuUpgradeM0Capture.After`
7. `HefuUnityLibraryExport.ExportPrepared`, then native host `scripts/prepare-private-unity-library.sh` + Gradle tests/assemble.

For actual proof, inspect `docs/space-studio/qa/m0-m1-render-upgrade-v1`. All modifications are opt-in to generated Unity scenes; the shared original CAD source, Quest project and preexisting XR edits were not changed.

Further work M2: world-scale tile/wood leather PBR and fabrics. M3: quality profiles and mobile/Quest GPU profiling. M4: 5-view PNG export plus complete design board. Explicit user approval is required before any NEW formal GitHub Release; M0+M1 commit itself is not a release.
