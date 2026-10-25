# HefuVR M0 + M1 Fixed-Camera Unity Proof (2026-10-25)

The ten original 960×640 PNG captures are **genuine Unity Metal Built-in Render Pipeline frame renders**, not generative mockups. Identical FOV=60°, time of day=Luoyang warm autumn Day, height=1.55m and camera coordinates for each before and after comparison. Test baseline started at the parent `feat/scene-workspace` commit `2e14d6d` (Android Spaces v3.3.7). `HefuUpgradeM0Capture.Before` ran and all five camera images were saved **BEFORE the first art changes**, then `After` ran after original M1 glass+sofa geometry changes and Day/Birthday GPU GI rebake.

| Camera ID | View | Before YAVG | After YAVG |
|---|---|---:|---:|
| Hero_LivingDining | Dining toward south-facing living room | 127.469 | 127.325 |
| Entry_North | North-side foyer into dining | 97.425 | 97.416 |
| Dining_Cabinet | Cabinet/kitchen glass doorway | 120.327 | 120.296 |
| Living_BirthdayWall | Living, sofa/birthday wall | 169.792 | 169.846 |
| Balcony_South | South window/outdoor autumn placeholders | 116.810 | 117.033 |

**YAVG is full-image mean 8-bit luma, not physical lighting lux or perceptual photo-fidelity.** This M1 pass intentionally introduces *small, targeted geometry/transparent-material changes* instead of falsely claiming a dramatic brightness change: the baseline already had real baked GI, correctly oriented solar sun and physical pendant lights.

`compare/*-before-after.png`: before left, after right, both at the EXACT same camera. `before/` and `after/` contain individual source PNGs.

M0 audit: Unity 2022.3.62f3c1, Built-in Render Pipeline (URP asset NOT active/installed), Linear color, Quality 5, MSAA x2, shadow distance 11m; 1 Directional, 2 Point, 2 Spot lights, only 1 shadow caster, 256px prebaked GI atlas, 46 LightProbes, 17 movable furniture. Glass was already Standard/Transparent (`queue=3000`, `ZWrite=0`) so the user-visible wall opacity could not honestly be attributed to Opaque mode. Actual south facade is three CAD panes each in two groups; previous builder had already hidden four duplicates, leaving only the central #22 and #28 layers. South per-sheet alpha changed `.075→.048`; kitchen door/general glazing `.22→.125` while preserving original hinge/door collider and apartment walls. The opaque solid architectural spandrel below the window remains part of the CAD and was intentionally **NOT erased**. Both real southern transparent panes still show the illustrative exterior sky/trees; exact visibility under every phone GPU requires live acceptance.

Sofa baseline is a real Blender FBX, not blank: source visual mesh `Hefu_P1_BlenderVisual_HefuP1_SoftSofa` had 6,184 triangles and occupied ~0.86m wide ×2.26m long ×0.92m tall. M1 adds three separate slightly rounded 0.670m ×0.695m visible leather seating caps at y≈0.606m and a pooled front-edge piping mesh, attached under `Move_Sofa` without changing the existing Blender part, layout transforms, original CAD/colliders or furniture ID. No claims of a physically measured manufacturer's exact cushion CAD.

Full QA: `HefuM1AcceptanceQA.Validate` PASS; 17 movables kept, no new colliders, 2 center south visible +4 duplicate pane renderers off, kitchen leaf interactive and transparent, Luoyang afternoon sun entering from south (+Z incoming direction), 2 downward Spots actually inside the pendants with Emission, 1 day GI atlas and 46 real probes, runtime `hero_living_dining` preset APPENDED after 9 existing view IDs. `HefuPhotoViewsQA.Validate` PASS 10/10, `HefuWallPBRRegression.Validate` PASS legacy PBR/CAD/Glass/Solar/Sofa, `HefuDualLightingQA.Validate` PASS toggle cache and 256px Day/Birthday + 46 probes each. Device FPS/flicker and actual saved-layout touchscreen tests remain pending because ADB shows no device.

**Known risks:** Glass remains alpha-blended Standard shader; without refractive optics and correct mobile render-order tests it may show transparency sorting artifacts. This pass is M0+M1 only, not the full M2 material and P3 soft furnishing stages. Do not confuse exterior placeholder trees with the actual user's location. Existing built-in rendering was retained to avoid breaking the Android/Quest integration, and HDRP/URP migration was NOT performed.
