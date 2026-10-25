# HefuVR v3.3.2 — Unity warm interior reference style image QA

These are **actual Unity 2022.3 Metal Editor GPU captures**, not image-generated decoration proposals, 960×640 resolution. Rendered from the current real CAD home layout with simulated 2026-10-25 Luoyang autumn sun.

- `Unity_Dining.png`: dining view of 2 cream pendant lamps with brass rods, warm walls, walnut table. The original cabinetry, white fridge and kitchen glass door are preserved. This is a fixed design approximation, not calibrated manufacturer lighting.
- `Unity_Living.png`: same default living camera as v3.3.1 and warm neutral wall material. The original green sofa/coffee table and south-window autumn trees persist.
- `Unity_SouthWindow.png`: the south-side view showing original transparent window and illustrative sky/tree placeholders.
- `Living-v331-v332-Comparison.png`: pixel-composed left v3.3.1 living view, right v3.3.2 living view (v3.3.2 neutral `Style` applied for runtime parity); field-of-view same, base image resolutions differ. The most visible change is wall warmth rather than a change in layout.

### Test gates

`HefuWarmReferenceStyleQA.Validate` PASS: 2 lamps with lathed shade mesh, 2 linen curtains placed at the sides, 0 new colliders, 0 new lights, no south-window center blocking, 17 original movable roots, Style switch neutral/cool both still working. `HefuWarmReferenceRegression.Validate` PASS including 1 autumn seasonal QA + original six editor checks.

### Not in scope / acceptance still pending

Mobile device not available via ADB as of build. Phone UI gesture/save/load, frame rate, GPU batch count and transparent material sorting need validation on-device. The outside trees remain angular low-poly models. The reference image has realistic global illumination and reflected light on floors; these are **not baked or physically matched** in v3.3.2, so no '80% photo-level' claim is made.
