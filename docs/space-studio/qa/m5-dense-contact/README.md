# HefuVR M5-C · 256 light probes + contact shadow experiment

Local Unity 2022.3 Built-in Editor validation, 2026-11-05.

- Probe count: 46 -> 256. Original 46 preserved, furniture still non-static.
- GI stays 20 texels/m, AO off. Actual bake 6.4 seconds, 1 x 512px lightmap, 256 baked probe SH data; independent candidate scene.
- 14 procedural blob shadow renderers share one 128px texture/material/quad and follow real furniture roots; three empty roots remain empty.
- No added collider, static GI contribution, lights, runtime shadow map or layout persistence edits.
- 5 cameras x two blob states = 10 actual Unity 2048x1365 captures, plus comparison with 46-probe M5 NoAO screenshots.
- A/B findings: visibly minimal blob contribution. Wider tuning improved nonzero pixels but SSIM on fixed views remained 0.999984 or higher. Do not claim it solved the furniture contact shadow problem.
- NOT ready for a Quest release: prior Day/Birthday lighting preset contains 46-probe SH; both modes must be rebaked for 256 probes, and an actual device must benchmark FPS and heat.
- No reflection-probe recapture in this stage. Old M4 scenes and source settings preserved.
- Reproducible source: assets/public/hefu-cad/m5-gi-ao.
- Local screenshot evidence: /Volumes/ssd/HefuVR-BiRP-M5-ABTest-20261025/ABTestReports/M5_GI_AO/DenseContact.

No private home screenshots, scene-specific LightingData or generated assets committed to GitHub.
