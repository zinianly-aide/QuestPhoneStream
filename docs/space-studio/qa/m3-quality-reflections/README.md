# HefuVR M3 actual Unity camera QA (2026-10-25)

All screenshots are **actual Mac Unity 2022.3 Metal renders**, not generated conceptual interior images. The scene remains Built-in render pipeline. Compare `before` (M2 commit `42192d9`) and `after` (M3) at 960×640, 60° FOV, exactly the same five archived 1.55m eye-level positions. The same Day/Birthday 256×256 lightmap and 46 light probes remain; M3 adds locally distinct precomputed 128px reflection cubemaps and runtime graphics tiering, not new furniture or lighting.

- `compare/Hero_LivingDining-M2-vs-M3.png`: main living/dining camera
- `compare/Dining_Cabinet-M2-vs-M3.png`: cabinet/kitchen reflection-sensitive materials
- `compare/Living_BirthdayWall-M2-vs-M3.png`: green leather sofa and specular reflection
- `compare/Entry_North-M2-vs-M3.png`: foyer scene
- `compare/Balcony_South-M2-vs-M3.png`: south glazing and illustrative exterior
- `compare/Battery-Balanced-Quality-Hero.png` and `profiles/Profile_*_Hero.png`: same hero scene from the three rendered tier states (the visual difference comes primarily from sun shadows, reflection volumes, and HDR; antialiasing should be judged on native resolution, not scaled thumbnails)

**Measured full-frame YAVG changed negligibly in the default editor capture**: Hero M2=127.139, M3=127.139; Entry M2=97.0875, M3=97.0872; Cabinet, Birthday Wall and Balcony virtually identical. That does NOT mean the reflection cubemaps are duplicates: M3 probe QA measured **3,149 color-different sampled pixels across their 6 faces**, comparing LDR cube face contents, and verified profile states. Default-pose renders may not resolve these small view-angle-specific specular improvements. Avoid claiming major photo realism gains from the global brightness metric.

Testing: M3 Unity QA PASS; legacy CAD/UV/PBR/south window/sofa QA PASS; M2 Daily decor/Birthday independence PASS; two real 256px GI bakes/46 probes and 16 mode toggles PASS; roof cutaway/2.677m ceiling PASS; Android exported Unity Library PASS. Real Android/Quest FPS, throttling, heat, device memory and transparency ordering require **device-connected QA**; this document reports no invented benchmark.
