# HefuVR v3.3.0 Unity render QA (macOS Metal, Editor batchmode)

Actual 800×560 Unity 2022.3 camera captures using `HefuVisualCaptureP3.Run` **without** `-nographics` (that flag prevents real camera rendering). They are not AI-generated effect images, and not screenshots from a physical phone.

- `Sofa-v329-v330-UnityComparison.png`: left — v3.2.9 sofa from TV side (showed upholstered BACK, seats missing), right — v3.3.0 (three individual seats and cushions facing TV). Before image was captured from the original Unity visual and stored for the comparison at a similar camera angle; not pixel-perfect registered.
- `SofaFront-v330-Unity.png`: corrected sofa, same Blender mesh kept as original `Move_Sofa` visual child; 180-degree yaw offset without rotating the saved furniture root or colliders.
- `SouthWindow-v330-Unity.png`: from inside the south-side opening, shows *illustrative* sky, now-upright scaled trees and abstract towers through low-alpha transparent CAD glazing. Not an actual view outside the home.
- `Living-v330-Unity.png`: original living camera preset after changes. It is **not** a substitute for device evaluation of transparent sorting, phone exposure, 30/60fps, or actual saved-layout behavior.

### Measured regression root causes

1. **Sofa front/back inversion:** imported Blender axis conversion was correct but the local sofa visual faced away from west-side TV. Rotated only the visual FBX by `Quaternion.AngleAxis(180, Vector3.up)` around the existing origin; enforced in `HefuInteriorRefinementQA`.
2. **Tripled south window tint:** 2 south-facing CAD windows are each 3 panes (`21/22/23`, `27/28/29`) at `Z≈-10m`. Rendering all 3 transparent layers gave a nearly opaque bluish result. Keep only the CENTER visible pane (`22` and `28`) and preserve other meshes/interaction without rendering them; single-pane Fade alpha 0.075, shadow disabled. Other 12 windows retain original clear glass. Enforced in `HefuSceneAtmosphereQA`.
3. **Invisible outdoor trees:** the Unity FBX prefab root uses import scale 100 and Blender axis conversion rotation. Code had overwritten prefab import scale and world rotation, shrinking trees ~100× and tipping them sideways. Now multiply the original prefab scale and compose yaw onto the imported rotation. QA checks world height between 2.3m and 8m and zero outdoor colliders.

### Limits and next phase

This closes defects, not an objective claim of "80% photorealism." The synthetic tree canopy / distant buildings remain visibly low-poly and only approximate the effect illustration. Next: licensed leafy vegetation LODs, measured curtain/glass geometry, *static only* baked GI, light probes, localized baked reflections, phone frame-time QA. No full-city real panorama and no personal scans included.
