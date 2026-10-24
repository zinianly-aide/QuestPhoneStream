# HefuVR AI-render-reference-derived materials (v3.2.7)

Nine diffuse/normal preview textures. `PhotoWalnut`, `PhotoOak`, `PhotoLeather`
and `PhotoRug` combine small, owner-approved, AI-generated effect-image pixel
samples with tileable synthetic microtexture. `PhotoPorcelainFloor` and its
normal map use the effect image's porcelain colour reference with generated
porcelain veining and 0.78 m tile-edge grout. All normal textures were generated
algorithmically; none are actual scans of the furniture or floors.

Because the effect image includes illumination, sampling it *whole* would bake
in incorrect highlights from one camera angle. We use local patch samples only,
then apply materials to independently UV-mapped meshes. Imported CAD, user
scene save files, real room photographs and secrets are NOT included.

**Limitations:** texture accuracy and scale depend on Unity geometry and light
setup; these are not vendor PBR materials, no path-traced GI, and not a
pixel-identical recreation of the generated image. Requires on-device checks.
