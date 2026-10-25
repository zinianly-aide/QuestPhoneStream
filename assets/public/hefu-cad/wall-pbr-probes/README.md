# HefuVR v3.3.3 — PBR plaster and limited baked room reflections

The existing Blender/CAD-based mobile scene felt excessively uniform because **all 20 CAD wall meshes had zero UV coordinates**. Applying realistic-scale wall textures to them had no effect. This asset pack fixes that specific blocker, without changing CAD vertex coordinates, colliders, doors, north orientation, room proportions, movable furniture IDs, outdoor views or WebRTC protocols.

## Delivered

- `unity-editor/HefuPlasterWallBuilder.cs`: projects **20** imported CAD walls into world-unit UV space based on face normals, persists each cloned UV mesh, and supplies a shared built-in Standard matte ivory plaster material (Metallic=0, Smoothness=.18) with synthetic low-contrast plaster BaseColor and tangent-space NormalMap. The original wall MeshColliders and 3 runtime Style palette selections remain intact.
- `textures/IvoryPlaster_BaseColor.png` (512²) and `IvoryPlaster_Normal.png` (512²): author-generated tiling synthetics, no scraped user or manufacturer images. `scripts/hefu/generate_wall_plaster.py` reproduces them in the local Mac project. The script currently has an absolute local output path that should be parameterized for remote CI.
- `unity-editor/HefuArchitecturalTrimBuilder.cs`: generates **one combined mesh of 107 CAD-derived floor–wall skirting segments**, ~10cm tall, thin cream lacquer, no new physical collider, light or individual per-segment draw-call object. Segments arise from mesh triangle boundaries of existing walls, avoiding arbitrary drawn bars through unverified passageways.
- `reflection-cubemaps/Hefu_Living_64.exr` and `Hefu_Dining_64.exr`: 2 actual **Unity Editor Metal baked 64px cubemaps**, captured from existing mobile scene on 2026-10-25; files ~20KB each. `unity-editor/HefuBakedRoomReflections.cs` installs 2 localized **Custom** ReflectionProbes with box projection, no expensive runtime recapture. These cubemaps are NOT global illumination lightmaps and reflect the current roofless design-preview scene.
- `unity-editor/HefuWallPBRQA.cs` + `HefuWallPBRRegression.cs`: tests 20 wall projected UVs/material/normal maps, original mesh colliders, 1 pooled skirting renderer, 2 linked static 64px probes, Style switch and 17 movable objects; then delegates to complete v3.3.2 regression, which covers sun, glass, kitchen orientation and Blender furniture.

## Integrating with the existing private HefuVR Unity project

Copy the `unity-editor` scripts into `Assets/Hefu/Editor`, 2 textures under `Assets/Hefu/Textures/WallPBR`, and `.exr` files to `Assets/Hefu/Generated/Reflections` (Unity should import them as Cubemaps). In `HefuAndroidBuilder.Prepare()` after `HefuWarmReferenceStyleBuilder.Apply(house.transform)` add:

```csharp
HefuPlasterWallBuilder.Apply(house.transform);
HefuArchitecturalTrimBuilder.Apply(house.transform);
HefuBakedRoomReflections.Apply(house.transform);
```

Run `HefuWallPBRRegression.Validate` using Unity batchmode. For real GPU pictures without `-nographics`, run `HefuWarmReferenceStyleRender.Run`. The public source repo intentionally does not duplicate the entire private Unity workspace.

## Limits

This version **does not bake global indirect illumination**, build a physical ceiling, add SSAO, remove room-scale geometry approximations or include physically scanned cabinet/sofa materials. The user-provided concept picture is a target style reference, **not** a texture projected onto the room. Mac Metal screenshots under `docs/space-studio/qa/v3.3.3` are editor render proofs and do not establish phone rendering performance or "80% photorealism".
