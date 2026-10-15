# Materials, textures and import conventions

## Shared material library

Fifteen sets: SafetyOrange, SignalYellow, TealPaint, GalvanizedSteel, Rubber, Timber, KraftPaper, Terracotta, ReflectiveWhite, CharcoalPaint, HazardStripes, RoadGraphic, CautionGraphic, BagGraphic and WorklightLens.

Every set has four PNG files. WorklightLens has a fifth emission file, for **61 textures total**. The ten general surface materials use 512 × 512 maps. HazardStripes, RoadGraphic, CautionGraphic, BagGraphic and WorklightLens use 1024 × 1024 maps. Surface noise was generated procedurally; panel graphics use original geometric shapes/bitmap lettering. Graphic panels are dedicated panel artwork, not general seamless repeat textures. HazardStripes and the general surface sets are designed for repetition.

| Suffix | Channels | Import color space / use |
|---|---|---|
| BaseColor | RGB | sRGB; opaque albedo |
| Normal | RGB, tangent-space OpenGL (+Y) | Non-color / Unity Normal Map; strength 0.5 |
| ORM | R = 255 constant; G = roughness; B = metallic | Linear; glTF metal–rough workflow |
| MetallicSmoothness | RGB = metallic; A = 1 − roughness | Linear; Unity metallic workflow; alpha is data, not transparency |
| Emission | RGB, WorklightLens only | sRGB; emissive color, factor 1 |

**There is no per-object baked AO, displacement, opacity or high-resolution source scan.** The red ORM channel must not be advertised as baked AO. ReflectiveWhite is a light/rough surface material, not a physical retroreflective traffic-sign shader. Painted/metallic names describe visual materials, not certified physical measurements.

## Unity materials

`Materials/URP` contains 15 Lit materials; `Materials/BuiltIn` contains 15 Standard materials. They reuse the same PNG files. Default serialized prefabs refer to URP materials. Run the setup tool in a fresh URP or Built-In project to rebind the current pipeline and actual FBX mesh file IDs. The optional inactive-pipeline materials may show an unavailable shader when that pipeline is not installed; do not use them in scenes for the other pipeline. For a final single-pipeline submission, retain only the tested pipeline's material assets or package alternatives according to Unity's current guidance.

URP depends on Unity's Universal Render Pipeline package supplied by the chosen URP project template. No shader binaries or copies of Unity shaders are bundled. Built-In materials use Unity's built-in Standard shader. HDRP support and tested version numbers are not claimed. The setup tool deliberately rejects unsupported non-URP pipelines rather than silently claiming compatibility.

## FBX versus GLB materials

The standalone GLBs embed base color, normal, ORM and optional emission textures and contain explicit metallic/roughness materials. Each GLB is independently portable; embedded texture duplication across different GLBs is intentional. No Draco/Meshopt/KTX compression or decoder dependency is used.

The original binary FBXs contain mesh topology, normals, both UV sets, named material assignments and relative base-color texture links. **FBX shader translation is not relied on for complete PBR.** Use the supplied Unity material assets/Editor setup, import GLB into Blender, or run the Blender source generator for its complete material nodes. Keep the delivery folder layout intact for standalone FBX relative paths; when moving an FBX independently, also copy/relink its textures.

Unity computes MikkTSpace tangents from the imported normals and UV0. Authoring normals are supplied; smoothing/UV seams intentionally split vertices. Neither the custom FBX writer nor its axis/unit metadata has received an independent Autodesk/Blender/Unity import test in this workspace. The included local validator verifies internal structure, arrays, connections, paths and scale metadata only.

## Lighting, shadows and budgets

All runtime materials are opaque. Graphic planes sit slightly in front of supporting surfaces, are one-sided, and should not be scaled to microscopic or very large world sizes without inspecting depth separation. The hard hat and wheelbarrow have modeled interior surfaces rather than relying on a global double-sided shader.

The complete 12-prop set has 41,568 triangles and 45 material slots across its 12 meshes. Shared materials reduce duplication but do not imply a single draw call. Instancing is enabled on the supplied materials; actual batching, texture memory, overdraw and GPU cost require profiling on the chosen renderer/device. There are no automatic LODs or performance guarantees.

UV1 charts are unique but deliberately numerous: every source face has a chart. For low texel-density baked scenes, let Unity regenerate secondary UVs with an appropriate margin, then bake and inspect seams. The package import helper defaults to preserving the provided UV1 channel, so disable/adjust that setting first if you choose regeneration.
