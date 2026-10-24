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
