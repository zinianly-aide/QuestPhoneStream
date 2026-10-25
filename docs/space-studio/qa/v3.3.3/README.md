# HefuVR v3.3.3 — actual Unity wall-PBR and reflection QA

The before/after assets are real Unity 2022.3 Mac Metal GPU camera captures, not AI-generated renders.

* `Dining-v332-v333-Comparison.png`: v3.3.2 baseline **left**, v3.3.3 **right**, same dining camera. Wall material is now projected at 1m UV repeat and uses low-amplitude plaster normal map; original dual pendant and real CAD cabinet positions are unchanged.
* `Living-v332-v333-Comparison.png`: baseline left, new right, same living camera; wall PBR, CAD-derived baseboard and offline 64px reflection volumes applied.
* `Unity_Dining.png`, `Unity_Living.png`, `Unity_SouthWindow.png`: v3.3.3 final 960×640 GPU captures.

### Technical validation

* Original failure: all twenty `*_CAD_wall_*` imported meshes had `mesh.uv.Length=0` (20/20). Now all 20 have exactly one UV per vertex, 1,144 vertices total; Standard rough plaster with generated NormalMap applied, while CAD MeshCollider references remain unchanged.
* One pooled trim mesh built from **107 existing wall floor-face edges**; no new physics object or door obstruction is intended. Check actual camera and CAD per room before real-world fabrication.
* Two custom, **prebaked**, box-projected 64px reflections in living and dining. No runtime cube capture and no global illumination.
* Full `HefuWallPBRRegression.Validate` PASS including existing style/palette, bedroom/kitchen, south window glass, Luoyang autumn sun, old sofa/coffee table, 17 movable objects.
* Android APK, if built, must separately pass Gradle 24-unit test suite, signature and build version checks. ADB phone not present in the Mac at capture.

### Remaining photorealism constraints

Walls, doors and cabinetry are still lightweight architectural proxies. The principal remaining work is physically modelled architectural mouldings/cabinet bevels and the **roofless scene/GI/lightmaps split for camera mode**; next, mixed fixed-object lightmaps and dynamic-furniture light probes. This release is a real technical foundation, not a claimed numerical 80% similarity to a style board.
