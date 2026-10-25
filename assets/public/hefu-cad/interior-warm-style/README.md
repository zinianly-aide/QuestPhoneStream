# HefuVR v3.3.2 — warm interior reference finish

This is a **source pack for the local HefuVR Unity project**, not a standalone project and not a photorealistic reconstruction of the user-supplied design illustration.

### Delivered geometry and material

- `unity-editor/HefuWarmReferenceStyleBuilder.cs` generates a non-interactive additive room dressing `Hefu_WarmReferenceStyle_v1` with **two cream pendant lamps** centered above the actual CAD dining table, brass suspension rods, ivory procedural lathe shade and low-cost diffuser, without spawning any new realtime lights.
- Two small **ivory linen folded side panels** and a thin curtain rail are placed on the INNER side of the south living window, aligned with CAD 21/22/23 x [-10.33,-5.53], y~[.74,2.48]. The centre of the south window remains clear; original glazing meshes/materials and balcony functionality remain intact. All decor is collider-free, visual-only and can be removed as one group.
- `textures/Linen_IvoryWeave_512.png` is deterministic synthetic weave, not a fabric product scan or full-image screenshot texture. Regenerate using `scripts/hefu/generate_warm_linen.py` (currently uses local Mac project path; parameterize before generic CI).
- `unity-runtime/HefuDecorSwitcher.cs` keeps all 3 existing Style choices but changes neutral from grey-white to **warm ivory**, warm-wood preset to warm cream; cool-gray preset unchanged. No additional furniture selection/control or saved layout schema changes.
- `unity-editor/HefuWarmReferenceStyleQA.cs` verifies shade UV/geometry, placement above table, side curtain distance from glass centre, 0 extra lights/colliders, all 17 movable furniture root nodes and working original Style switching.
- `unity-editor/HefuWarmReferenceRegression.cs` invokes the above plus `HefuLuoyangAutumnRegression.Validate` (solar/sky/ground plus six prior Editor QA routines).
- `unity-editor/HefuWarmReferenceStyleRender.cs` makes actual **960×640 Mac Unity GPU** pictures from dining/living/south-window fixed viewpoint. Preset `Neutral` is explicitly applied before capturing to mimic runtime `HefuDecorSwitcher.Start`.

### Integration

Copy the `.cs` files under `unity-editor/` to local `Assets/Hefu/Editor`, replace the existing runtime `Assets/Hefu/Scripts/HefuDecorSwitcher.cs` with the shipped version, and put linen PNG under `Assets/Hefu/Textures/WarmInterior/`. Then in existing `HefuAndroidBuilder.Prepare` immediately AFTER `HefuP2InteriorLightBuilder.Apply(house.transform);`, add:

```csharp
HefuWarmReferenceStyleBuilder.Apply(house.transform);
```

Run `HefuWarmReferenceRegression.Validate` (headless) and `HefuWarmReferenceStyleRender.Run` (Metal/GPU, **without** `-nographics`). The local design-lab source and CAD base FBX are not modified. Editing `HefuDecorSwitcher` is limited to two literal colours.

No real-time illumination bake, light probe generation, capture reflection probes, physically accurate curtains, material manufacturer scans or new device FPS testing are claimed. Existing metal/glass/Blender furniture/autumn sun from v3.3.1 remain. Additional renderers have a mobile cost that still needs device profiling.
