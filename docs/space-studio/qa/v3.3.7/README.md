# v3.3.7 — genuine Unity Metal lighting comfort regression captures

Each direct Unity camera capture is 960×640, same camera composition as v3.3.6. Files `Day_Dining.png`, `Day_Living.png`, `Day_FloorLamp.png`, `Birthday_Dining.png`, `Birthday_Living.png`, and `Birthday_FloorLamp.png` come from the newly prebaked generated phone Unity scene; these are not AI-generated effect images. Six `*-v336-v337.png` comparisons present left old v3.3.6, right new v3.3.7.

## Measured image brightness (FFmpeg full-frame signalstats mean YAVG, 8-bit)

| View | v3.3.6 | v3.3.7 | Change |
| --- | ---: | ---: | ---: |
| Day Dining | 105.367 | 120.117 | +14.0% |
| Day Living | 102.886 | 133.570 | +29.8% |
| Day FloorLamp | 114.478 | 143.717 | +25.5% |
| Birthday Dining | 69.779 | 81.289 | +16.5% |
| Birthday Living | 98.296 | 107.477 | +9.3% |
| Birthday FloorLamp | 129.636 | 143.300 | +10.5% |

All six frames become brighter while the existing physical light fixtures, shadow casters and visibility remain unchanged. This is **not** a measured interior lux report and does not imply a visual-quality percentage relative to the supplied inspiration art.

Quality gates: real Day/Birthday GI bake `256×256` atlas each, `46` baked probes each, old PBR/CAD/glass/solar/sofa regression PASS, roof GI QA PASS, both mode swaps PASS with 16 toggles / one SH cache build, no same-mode repeat processing, no overlapping-lightmap-UV warnings. Native Android QA is pending because ADB did not find a phone.
