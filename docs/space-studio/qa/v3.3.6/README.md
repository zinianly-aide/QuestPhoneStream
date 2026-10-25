# v3.3.6 — actual Unity Day/Birthday light-source comparisons

Six direct Unity 2022.3 Mac Metal `960×640` render captures, **not AI-generated image proposals**. Camera positions are identical between Day and Birthday:

- `Dining-Day-vs-Birthday.png`: pendant underside glow and warm table SpotLight pools. Right-hand Birthday side includes actual CAD-anchored birthday decorations.
- `Living-Day-vs-Birthday.png`: sofa, walnut table and outdoor autumn sky with contrasting natural vs local room illumination.
- `FloorLamp-Day-vs-Birthday.png`: a physically visible emissive fabric shade and warm light on nearby wall. Originally this lamp was hidden by `12_CAD_wall_12`; moved to the living side and added a raycast visibility gate.
- `Day_Dining.png`, `Birthday_Dining.png`, `Day_Living.png`, `Birthday_Living.png`, `Day_FloorLamp.png`, `Birthday_FloorLamp.png` are the full-size fixed camera views.

Two different 128x128 baked EXR lightmaps and two distinct 46-probe SH sets were generated in separate Unity GPU bake sessions. Test `HefuDualLightingQA.Validate` proves live swap of both baked datasets plus emission and direct lights; previous CAD/roof/glass/furniture QA still required. Mobile live image/FPS checks remain pending because no ADB device was available.
