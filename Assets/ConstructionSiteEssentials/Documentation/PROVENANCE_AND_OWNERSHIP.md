# Provenance, ownership and AI disclosure

**Created for this request:** 15 September 2026. Pack name: Construction Site Essentials. Version: 1.0.0. Method: AI-assisted procedural authoring in ChatGPT plus executed local Python tooling.

## Origin of the asset content

| Content | Origin / evidence |
|---|---|
| 12 prop meshes | Newly authored procedural primitives and assemblies in `Source/author_pack.py`; complete editable part data in `mesh_data.json` |
| Surface texture maps | Original deterministic noise, analytical patterns and color/roughness/metallic construction; no texture-library sampling |
| Sign/bag graphics | Generic geometric arrows, stripes, warning shapes and a small geometric bitmap alphabet written into the authoring script |
| GLB/FBX exports | Produced from that same mesh data by `Source/export_pack.py`, an original serializer; not downloaded stock models |
| Unity scaffolding | Original deterministic YAML/meta generation plus package-local C# setup/validation tools |
| Previews | VTK renders of the actual generated geometry, then layout/type composition; not text-to-image substitutes |
| Blender source | Complete reconstruction script and geometry delivered; the actual `.blend` must be generated with Blender |

No third-party artist models, stock material packs, scanned objects, photographs, real manufacturer logos, licensed characters or extracted game files were used. Shapes are generic construction objects, not asserted replicas of proprietary products. The road/caution graphics and hard hat are visual game props, not certified signs or safety equipment.

The source uses several ordinary shape algorithms: lathe profiles, beveled boxes, cylinders, tori, stable tube frames, modeled shell interiors, deformation and UV chart packing. This is not a raw text-to-3D point cloud or remeshed scan. Model names, component hierarchy, metre scale, bottom pivots, two UV sets, reusable material maps and runtime export metadata were authored explicitly. Tube-frame shading problems were found in the mesh/render review and corrected before delivery.

## Ownership limits

The OpenAI Terms of Use state that, as between the user and OpenAI and to the extent permitted by applicable law, the user owns Output; they also warn that output may not be unique. This supports the intended commercial handoff but does not establish exclusive copyright in every AI-assisted element. The publisher remains responsible for rights review and the eventual customer license. This package's license notes do not claim to confer third-party rights that do not exist.

Official source: https://openai.com/policies/terms-of-use/ (accessed 15 September 2026).

## Tooling, not embedded dependencies

Python, NumPy, SciPy, Pillow, Trimesh and VTK were used as local build/inspection tools. None of their binaries or source libraries are redistributed in the pack. Blender is the target of the delivered reconstruction script, but was not installed in the build workspace. Unity Editor was not installed either. Preview titles/labels use rasterized DejaVu Sans typography from the local environment; no font file or font-dependent runtime asset is delivered. Asset texture lettering is generated from the original geometric alphabet, not a font file.

## Disclosure draft for the publisher

“ChatGPT assisted with Python geometry and texture authoring, export generation, Unity editor tooling and documentation. The models and textures were generated procedurally from newly authored code. Additional work included logical component structure, metre scale and ground pivots, two UV sets, reusable PBR maps, topology/UV/export checks and actual-mesh preview rendering.”

Review and amend this text yourself before publication. Do not describe this content as human hand-modeled, hand-painted or independently Unity-tested unless you actually perform and document that work. The current package is a submission candidate with explicit pending external checks, not an Asset Store approval claim.
