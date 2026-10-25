# HefuVR v3.3.1 — Luoyang late-autumn lighting, weather-independent seasonal preview

## Exact scene configuration

- Location preset: Luoyang, Henan **34.6836°N 112.4536°E**, China Standard Time **UTC+8**. Design date 2026-10-25, local time **14:30**.
- NOAA-style approximate solar calculation, outputs late-afternoon **elevation 33.4° and azimuth 220.9°** (from north clockwise). Solar noon ~12:14 local, elevation ~43.3° from astronomy reference, our approximation ~43.5° within 0.2°. Precise urban shadow occlusion, terrain elevation and real weather are not modelled. Autumn daylight ~06:44–17:44 for that day; see https://www.timeanddate.com/astronomy/china/luoyang .
- Unity coordinate axes: +X east, +Z north; afternoon sunlight comes from SW and enters the south window traveling toward NE. The code sets `sun.rotation = Quaternion.LookRotation(-toSun)` instead of reusing an arbitrary hardcoded light direction. Only the existing directional sun casts realtime shadows.
- Default skybox: procedurally generated 1280×640 autumn sky with gentle warm horizon/atmospheric haze. Autumn muted foliage swatches on **five** existing low-poly trees; original Blender FBX size/orientation, location and material slots preserved. Procedural **512×512** tinted dry grass/soil on one low-cost, no-collider exterior ground mesh beyond south windows; nine simplified distant buildings get warmer haze colours.
- Fixed-view afternoon preview is **not** a real-time weather, geolocation, sunrise/sunset scheduler or city-specific photographed panorama. Seasonal colours are illustrative.

## Actual Unity Editor render evidence (800×560, Metal GPU)

- `SouthWindow-v330-v331-Comparison.png` — same camera, left v3.3.0 cool placeholder, right v3.3.1 Luoyang-autumn model with warmer sky, foliage and textured ground. **No image generation output used as proof.**
- `Living-v330-v331-Comparison.png` — same `living` camera, left original vs right autumn lighting. Warm indirect-bounce appearance is a viewport effect, **not true baked GI**.
- `Autumn_south_window.png`, `Autumn_living.png`, `Autumn_sofa_front.png` — final fixed-camera screenshots for repeated acceptance and subsequent visual regression. No Android device was attached.

## Automated gates

Unity `HefuAndroidBuilder.Prepare` PASS; `HefuLuoyangAutumnQA.Validate` PASS, including solar noon and afternoon elevation/azimuth bounds, sun direction, generated sky/ground, 20 autumn material slots, no outdoor ground collider. `HefuP1P2RegressionSuite.Validate` six legacy checks PASS: 18 CAD windows, interactive doors/slider, 17 movable objects, correct sofa seat orientation, tree heights, old QA. Combined `HefuLuoyangAutumnRegression.Validate` PASS.

**Limitations:** all five trees are still low-poly blocky silhouettes rather than realistic semi-transparent leaf cards. Ground terrain has no measured elevation or real-world camera panorama; there is no baked GI, true physically captured reflections, or hardware phone/Quest FPS measurements. Do not claim 80% photorealistic similarity based on these tests.

## Source locations

`assets/public/hefu-cad/atmosphere/unity-editor/HefuLuoyangSolar.cs`, `HefuLuoyangAutumnQA.cs`, `HefuLuoyangAutumnRegression.cs`, updated `HefuSceneAtmosphereBuilder.cs`; two generated PNGs in `assets/public/hefu-cad/atmosphere/sky/`; reproducible Python scripts in `scripts/hefu/generate_luoyang_autumn_sky.py` and `scripts/hefu/generate_autumn_ground.py`. Original local Unity working directory remains separate and must receive scripts+assets in the corresponding `Assets/Hefu/` paths. Generator scripts currently output into the Mac's local `/Volumes/ssd/HefuVR-Android` path, which should be made configurable before generic CI.
