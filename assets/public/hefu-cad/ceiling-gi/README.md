# HefuVR v3.3.4: CAD-derived interior ceiling and actual baked GI

This is a public reproducible **Unity 2022.3 Built-in Render Pipeline** source pack for the existing, separate HefuVR CAD-based Unity project; it is *not* a standalone Unity project.

## Delivered implementation

- `unity-editor/HefuCadCeilingBuilder.cs` projects the actual imported `00_客餐厅/过道` CAD floor top triangulation (**16 up-facing faces**) at y=2.677m, reversing normals to face the room. A single roof mesh plus 18 perimeter trim strips combined in a separate single mesh represents the existing floor geometry, rather than a made-up oversized ceiling rectangle. The original imported CAD mesh/colliders remain unchanged.
- `unity-runtime/HefuCeilingVisibility.cs` runs on the mobile scene's original camera. With a camera in the CAD dining/living floor footprint under 2.47m it shows the roof and trim; above that height / outside that footprint (including the saved `top_n` overview) it hides both renderers. It never changes furniture position, layout persistence, camera controller or control protocol.
- `unity-editor/HefuStaticGiBake.cs` uses **only the original static architectural shell**: 18 (of 20) walls, the existing dining/living floor, and the new ceiling; excludes exact coincident CAD walls 35/42 *from static baking only*, not from rendering/physics. It creates separate secondary lightmap UVs at high pack margins, bakes static indirect light into **1 small mobile atlas** and produces **46 Light Probes** under the actual CAD floor outline. Existing 17 dynamic furniture IDs remain non-static; renderer light probes are set to BlendProbes. Indoor illumination remains an illustrative model of 2026-10-25 14:30 Luoyang, not an actual live weather feed.
- `unity-editor/HefuRoofGiQA.cs` validates roof topology/height, indoor vs overhead cutaway, actual lightmap and 46 probe payload, 20 ContributeGI flags (10 of those were indexed into the final atlas in local QA), no roof physics, original wall-PBR and 17 movable furniture records. All older PBR/Glass/Kitchen/Style/Autumn regressions were also rerun PASS.
- `preview-lightmap/`: actual Unity GPU lightmaps and `HefuMobileGI.lighting` created by the tested scene. **LightingData.asset and its binding to the generated `.unity` scene are scene-specific and are intentionally not shipped here**. These PNG/EXR assets alone cannot be dropped into a new Unity project and expected to light it; run the bake for your target scene.

## CRITICAL: two-process build workaround on Apple Silicon Unity 2022.3

Attempting to do `HefuAndroidBuilder.Prepare()` + `Lightmapping.Bake()` + `BuildPipeline.BuildPlayer(Android)` **within one Unity process** triggered a native crash in `RadeonRaysJobCompositeLightmap.PostProcessLightmapCPU`, exit code 255. These tasks must run as **separate Unity invocations**.

Correct sequence on the existing HefuVR Unity project:

1. `HefuAndroidBuilder.Prepare` (creates and saves `Hefu_Mobile_Viewer.unity` and CAD-derived roof).
2. `HefuStaticGiBake.Bake` (opens that prepared scene; adds lightmap UV2, marks the 20 static architecture parts, generates probes and bakes GI; saves Unity LightingData and baked scene).
3. `HefuRoofGiQA.Validate` (must show real atlas and probes, otherwise stop).
4. `HefuUnityLibraryExport.ExportPrepared` (new local Export method: **only validates and exports**, MUST NOT call Prepare or Bake). This prevents regenerating the scene and invalidating the lighting atlas. Build the private Unity Library and integrate into native Android host using `QPS_UNITY_LIBRARY_DIR` and `scripts/prepare-private-unity-library.sh`.

For screenshots, use `HefuWarmReferenceStyleRender.Run` without `-nographics` and inspect the generated `Dining/Living/SouthWindow/TopNorth` captures. Do not ship if the Unity log reports overlapping UV islands or missing lightmaps. Live device FPS, thermal behaviour and phone gesture/return features still require an ADB-connected phone.

## Known visual limitations

The ceiling follows only the existing **living/dining/hall common floor**; other enclosed rooms are still open-top in their independent views. This is a deliberately scoped prototype, not a full-house ceiling model. Rooftop cutaway is a renderer change and does not dynamically rebake lighting/shadows; at overview distances baked static shading can remain. Moving furniture is lit by Light Probes, not individually baked. Cabinet geometry, outdoor foliage LODs and photoscanned materials are still stylized; this work does NOT imply "80% photorealism" of the supplied inspiration image.
