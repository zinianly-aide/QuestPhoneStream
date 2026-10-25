# HefuVR v3.3.5 — actual Unity Mac Metal comparison and acceptance

Four GPU-rendered 960×640 PNGs (`Unity_Dining.png`, `Unity_Living.png`, `Unity_SouthWindow.png`, `Unity_TopNorth.png`) are direct Unity scene captures, **not AI-generated mockups**. Three 1920×640 comparisons show v3.3.4 on the left and v3.3.5 on the right at exactly the same fixed camera configuration.

## Reproducible average image-luminance comparison

The `ffmpeg` `signalstats` filter computes mean 8-bit YAVG over each entire rendered frame. Camera and resolution are identical. The statistic includes darker furniture and exterior sky and does not represent a physical lux-meter, nor does it prove photo-level realism.

| Fixed camera | v3.3.4 YAVG | v3.3.5 YAVG | Relative increase |
|---|---:|---:|---:|
| Dining | 91.3207 | 127.497 | 39.6% |
| Living | 89.127 | 121.640 | 36.5% |
| South window | 74.1037 | 99.2579 | 34.0% |

New geometry: 60 chamfered cabinet door faces over 6 pooled meshes, with 8 updated walnut sideboard surface UVs. Original cabinet collider/renderers and 17 dynamic furniture roots were preserved. Existing ambient, sky, sunset orientation and flat-view interactions are maintained.

### Automated acceptance

- `HefuExposureCabinetQA.Validate` PASS — 2 unshadowed point fills, no extra lights, bounded performance; 6 cabinet batches, 1,020 vertices, 8 walnut UV meshes, furniture 17.
- `HefuRoofGiQA.Validate` PASS — static GI atlas 1, probes 46, floorplan cutaway and collision.
- `HefuWallPBRRegression.Validate` PASS — former light range cap 4.6m widened to 5.6m rather than disabling the legacy check.
- Unity GPU progressive GI bake completed successfully with no lightmap UV overlap warnings. `ExportPrepared` and host Android Gradle build independently verified in separate Unity sessions.

**Known limitations:** the photograph board remains an aspirational design. Present cabinet faces use procedural UV-wrapped veneers but are not physically fabricated bevel fittings. Not a physical lux match, no real mobile screenshot/fps/thermal tests without ADB.
