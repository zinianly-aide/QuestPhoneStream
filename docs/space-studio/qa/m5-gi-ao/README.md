# M5 · GI/AO controlled test — 2026-11-05

Source patch: ../../../../assets/public/hefu-cad/m5-gi-ao/README.md

The test runs on an isolated HefuVR Built-in Unity 2022.3 copy. It does **not** mean the Quest XR runtime now renders this Hefu scene.

- M4 baseline: GI 12 texels/m, 256² per mode, AO off, 46 probes.
- M5-A: 20 texels/m, AO off, real Unity bake 8.4 s, one 512², 46 probes.
- M5-B: 20 texels/m, AO 0.65m, AO influence 0.55 indirect/0.05 direct, real bake 6.6 s, one 512², 46 probes.
- A/B/Baked AO use matching five M4 positions (2048×1365); all 15 PNG frames confirmed nonempty.
- SSIM (M5-A vs M5-B): Hero 0.997622, Entry 0.997929, Dining 0.998789, BirthdayWall 0.998331, Balcony 0.998541.
- Qualitative result: AO has very subtle effect on the architectural shell and does not solve the sofa/table dynamic contact gap. Avoid claiming photographic realism.
- Original M4 scene and original GI settings are SHA-256 unchanged. Output scenes and LightingData are not committed because they reference the isolated HefuVR project.
- No ADB/Quest device: no actual FPS/GPU/thermal readings.
- Before publishing: inspect UV overlap warning logs, run both Day/Birthday independent GI bakes, validate switching and mobile QA.

The reference five M4 2K frames remain at ../m4-presentation-bench/highres/. Independent experiment images are retained at the local M5 ABTest workspace, not uploaded to the public repository.
