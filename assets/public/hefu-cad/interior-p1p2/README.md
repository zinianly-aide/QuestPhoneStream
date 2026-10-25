# HefuVR P1 furniture Blender + P2 mobile lighting preview

### Delivered, verified (2026-10-25)

* `blender/HefuP1_Furniture_Source.blend`: editable Blender 4.5.3 sofa and coffee table scene with rounded high-level geometry, seams, drawers and small trim.
* `blender/HefuP1_SoftSofa.fbx`: one multi-material mesh containing sofa, 3 seat/3 back cushions, piping, arm and 2 scatter pillows. **4 submaterials; 4201 vertices after Unity import.**
* `blender/HefuP1_WalnutCoffee.fbx`: one multi-material rounded walnut dual-drawer table, brass pulls and leg tips. **2 submaterials; 6346 vertices after Unity import.**
* `unity-editor/HefuP1FurnitureFinishBuilder.cs`: attaches visual-only FBX under existing `Move_Sofa` and `Move_Coffee_Table` pooled furniture visuals; disables but retains previous procedural renderer, and rejects asset bounds mismatches. Existing CAD collision and movable roots are untouched.
* `unity-editor/HefuP2InteriorLightBuilder.cs`: a modest unshadowed warm practical light at each of living & dining zones, mobile skybox ambient reflection, **no GI bake and no reflection probe capture**.
* `unity-editor/HefuInteriorRefinementQA.cs`: bounds, imported material slots, shadow flags, duplicate geometry/collider checks, 17 movable records, no-shadows lighting and perspective QA metadata.
* `unity-editor/HefuP1P2RegressionSuite.cs`: runs the above + existing Material/Atmosphere/Kitchen/Furniture/PhotoViews editor QAs (6 checks total).
* `../../../../scripts/hefu/generate_furniture_blender.py`: regenerate models on Mac with Blender 4.5.3 LTS; current output path is the local Unity working directory.

These are openly shareable, synthetic reference furniture assets. They are *not* manufacturer meshes, real leather scans or measured furniture geometry. The local Unity project is separately at `/Volumes/ssd/HefuVR-Android` and is not a standalone part of this repository; import this asset pack into it.

### Integration (do not alter source CAD)

In the mobile Unity `HefuAndroidBuilder.Prepare()`, run:

```csharp
HefuFurnitureDetailBuilder.Apply(house.transform);
HefuP1FurnitureFinishBuilder.Apply(house.transform);
HefuPhotoMaterialFinish.Apply(house.transform);
HefuSceneAtmosphereBuilder.Apply(house.transform);
HefuP2InteriorLightBuilder.Apply(house.transform);
```

Ensure the Blender FBX files are in `Assets/Hefu/Models/InteriorRefinement/`; avoid forcing imported prefab root rotation to identity (this turns the 2.25m sofa upright). Keep the importer root scale (often `100` due to Blender FBX unit transforms). Assign the old `SofaUpholstery`/`WalnutCoffeeTable` base materials to submaterials as the finish builder does. Existing original procedural geometry remains as hidden fallback.

### Quality gates

Local headless Unity 2022.3.62f3c1:

- `HefuAndroidBuilder.Prepare` — PASS, 17 `HefuMovable` objects.
- `HefuP1P2RegressionSuite.Validate` — PASS all six gates; nine fixed photo views include `top_n`, `entry`, `living`, `kitchen`, `balcony`.
- Model bounds sofa `(0.86, 0.91, 2.25)` meters and coffee table `(0.72, 0.53, 1.01)` meters, preserving CAD positions and collisions.
- **Not yet accepted**: real device FPS, actual material quality/opacity, final windows-outdoor exposure, mobile WebRTC/Back to App end-to-end interactions; no connected ADB device at verification time.

For visual comparison, test `living` and `balcony` at eye level, as `top_n` is intended for geometry/layout verification and does **not** show accurate material highlights or exterior views.
