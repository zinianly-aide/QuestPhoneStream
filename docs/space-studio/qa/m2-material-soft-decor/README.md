# HefuVR M2 actual Unity before/after QA

Five BEFORE/AFTER camera pairs captured **in real Unity 2022.3 Metal rendering**, not AI-generated visual art. BEFORE is the checked-in M1 `after` evidence from commit `96f8054`; AFTER is the newly rebaked M2 Unity scene. Every view uses exactly the same camera, FOV=60°, 1.55m eye and 960×640 output. `compare/` contains side-by-side left=M1, right=M2. `before/` and `after/` contain source unmodified frames.

| View | M1 YAVG | M2 YAVG |
| --- | ---: | ---: |
| Hero_LivingDining | 127.325 | 127.139 |
| Entry_North | 97.416 | 97.088 |
| Dining_Cabinet | 120.296 | 119.924 |
| Living_BirthdayWall | 169.846 | 169.787 |
| Balcony_South | 117.033 | 117.038 |

Full-frame YAVG measures luminance, NOT material realism, physical illumination, or a photo similarity score. Original real image references still need artist judgement in closer PBR/wood/cabinet views. M2 keeps light intensity unchanged, so the small luminance deltas are expected.

`DailyDecor-OFF-vs-ON.png` is a separate same-angle, real Unity-rendered look at the physical sideboard Worktop. The ON mode adds a small decorative glazed-ceramic planter and olive leaves; OFF hides it without removing the furniture or changing the Birthday scene. `daily-OFF.png` and `daily-ON.png` are source 960×640 frames.

Verification: M2-specific Unity editor QA PASS; 2 pooled mesh renderers, zero colliders/lights, 17 original movables, 5 Birthday anchors/115+ elements, independent settings; legacy CAD/PBR/glass/solar/sofa regression PASS, actual Day/Birthday 256×256 GI / 46 probes, repeat-16-toggle lighting cache PASS, Unity Android library export PASS. Android host unit tests and build outcomes are recorded in the release/build audit if available. No live ADB device was attached; no device FPS or touch interaction is claimed.
