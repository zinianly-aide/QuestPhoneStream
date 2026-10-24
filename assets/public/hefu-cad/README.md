# HefuVR public CAD-derived home whitebox

This folder contains the **public reference geometry** of the HefuVR home scene, released with the model owner's permission. It is a reusable, low-detail layout model, not a construction survey.

## Files

- `Hefu_CAD_v02.fbx`: baseline CAD-derived room / wall geometry for Unity or Blender (FBX 7.4 binary).
- `CAD_wall15_LeftBathOpening_v0311.obj`: corrected wall opening geometry.
- `CAD_wall16_LeftBathOpening_v0311.obj`: alternative corrected wall opening geometry.
- `CAD_wall16_StudyDoorOpening_v0311.obj`: corrected study doorway opening geometry.

Import the FBX into Unity or Blender, applying an appropriate meter scale; the OBJ models are separate patch variants and **should not all be overlaid together**. The HefuVR Unity source project applies scene-specific placement, materials, cabinets and furnishing logic separately; these four files alone are not the entire furnished scene.

CAD horizontal outlines are the reference. Ceiling height (nominally 2.7 m), opening heights, detailed furniture locations, materials and decor need further verification. This model is intended for visualization only.

**Not included:** original DWG, home address, real photos, private user scenes, Wi-Fi / LAN settings, credentials, and runtime files. The phone debug APK is distributed separately via the repository's GitHub Releases.
