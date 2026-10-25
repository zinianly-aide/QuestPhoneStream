# HefuVR light / glass / outdoor source assets

This directory contains **owner-approved illustrative scene assets** for the HefuVR Unity mobile scene. Nothing here represents an actual geolocated view from the home's windows.

* `sky/Illustrative_DaylightSky.png`: 1280×640 procedural 2:1 panorama for Unity `Skybox/Panoramic`.
* `blender/*.fbx`: three material-grouped low-poly broadleaf trees (~33 KB each), FBX imported by Unity with Y-up.
* `blender/OutdoorTrees_Source.blend`: editable Blender 4.5.3 source.
* `unity-editor/HefuSceneAtmosphereBuilder.cs`: scene-only material / light / outdoor builder; requires the **existing** private HefuVR Unity project (`HefuInteriorDoorBuilder`, `HefuAndroidBuilder`, CAD FBX).
* `unity-editor/HefuSceneAtmosphereQA.cs`: unattended geometry/orientation/glass QA.
* `../../../../scripts/hefu`: procedural sky and Blender regeneration scripts.

The Unity build injects the atmosphere after CAD doors and furnishings are generated. The scripts deliberately do not alter the original CAD FBX, kitchen orientation, application control/signaling protocols, 17 movable-furniture identities, or scene-save records. Outside placeholders have no physics collider. A Unity 2022.3 Standard shader is used for phone compatibility; desktop-quality reflections and prebaked indirect illumination are **not implemented**.

To recreate locally, copy/merge the classes into `Assets/Hefu/Editor`, source tree FBX files into `Assets/Hefu/Models/OutdoorPreview/`, and the sky PNG into `Assets/Hefu/Textures/OutdoorPreview/`. In `HefuAndroidBuilder.Prepare` call `HefuSceneAtmosphereBuilder.Apply(house.transform)` after `HefuPhotoMaterialFinish.Apply(...)`. This asset pack is **not** a complete standalone Unity project or working mobile APK.

Full production plan: [`docs/space-studio/hefu-realtime-realism-plan-2026-10-25.md`](../../../../docs/space-studio/hefu-realtime-realism-plan-2026-10-25.md).

## Late-autumn Luoyang scene preset (v3.3.1)

Additional public assets and reproducible code:

- `unity-editor/HefuLuoyangSolar.cs` calculates NOAA-style approximate solar elevation and azimuth for preset 2026-10-25 14:30 China time at Luoyang (34.6836N,112.4536E). Important: Unity is **+X east, +Z north**, solar rays travel opposite `toSun`.
- `sky/Illustrative_LuoyangAutumnSky.png` is a 1280×640 stylized daytime sky, not an actual sky-photo or astronomical HDRI.
- `sky/Illustrative_AutumnGround.png` provides low-cost autumn grass and soil colours for the **synthetic** scene immediately beyond south glazing, no collider.
- Updated `HefuSceneAtmosphereBuilder.cs` reuses the already-imported Blender tree FBX meshes and colours twenty foliage material slots in restrained late-autumn green, ochre and brown tones (without replacing building geometry or modifying the interior furniture and CAD). The ground is a single non-colliding renderer.
- `unity-editor/HefuLuoyangAutumnRegression.cs` runs full smoke and the existing six safety checks, including solar-vector tests.
- Unity-generated before/after screenshots in `docs/space-studio/qa/v3.3.1/`.

The time of day is a **fixed design preset**; do not interpret these visuals as a current meteorological simulation. A controlled morning/noon/sunset time slider with saved settings and baked-light variants is future work, not delivered in v3.3.1.
