# HefuVR M4 five real 2K Unity captures and editorial exhibit

`HEFU_M4_ExhibitionBoard_3200x1700.png` is a **real five-view Unity Mac Metal screenshot collage**, composed in native macOS AppKit with title/labels. This is not an AI-generated reference rendering. Its source five direct Unity 2048×1365 RGB PNG frames are in `highres/` along with an exact metric camera manifest. No HDRP/URP or panorama projection is used; target is Android GLES 3, Built-in pipeline.

| Photo | Physical camera | Purpose |
| --- | --- | --- |
| `Hero_LivingDining.png` | eye (-6.46,1.55,-3.96) toward south living | Main showroom composition |
| `Entry_North.png` | eye (-7.31,1.55,-1.10) | North foyer |
| `Dining_Cabinet.png` | eye (-7.28,1.55,-4.29) | Kitchen glass / sideboard |
| `Living_BirthdayWall.png` | eye (-5.55,1.55,-6.30) | Green leather sofa and birthday wall |
| `Balcony_South.png` | eye (-6.30,1.55,-7.05) | South windows and illustrative outdoor plants |

These are all the original **M0 fixed-camera coordinates** verified by M4 QA, with FOV=60 degrees and matching CAD geometry. Image dimensions 2048×1365. A 3200×1700 exhibit board groups these into one readable design overview; artifacts should not be interpreted as photometric or 80% photo-reference proof.

## Automatic QA actually performed

- M4 plan/geometry/performance percentile & CSV quote helper: PASS; all five camera baseline coords match, no scene layout writes from exporter.
- Legacy M1 glass/PBR/sofa/CAD and 17 furniture regression: PASS; M2 daily/party toggles and original UserPrefs: PASS.
- M3 quality tiers and unique actual local 128px reflection cubemaps: PASS.
- Day and Birthday GPU GI 256×256 each, 46 SH probes each; 16 rapid mode switches with single lightmap/SH cache build: PASS.
- Rooftop CAD cutaway height 2.677m, GI/UV2 and no extra colliders: PASS.
- Actual five high-res Mac Unity screenshot export: PASS, each nonempty file and manifest saved.
- Android Unity Library export and host unit tests / APK build: see build status; 24/24 Android unit tests expected.

**No ADB device was connected at this run.** Mobile buttons, actual Gallery MediaStore write, CSV results/FPS, sustained temperature and Quest rendering have NOT been validated on hardware. The M4 runtime benchmark samples frame delta and is not a real GPU profiler. Do not claim any real-device performance based on these Mac captures.
