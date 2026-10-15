# Release validation checklist

## Completed in this build workspace

- [x] Exactly 12 original props have source geometry and actual GLB/FBX exports.
- [x] Finite vertices, valid indices and positive non-degenerate triangle areas.
- [x] Display normals agree with triangle winding; closed components have positive volume.
- [x] Metre dimensions and ground-centred source pivots checked.
- [x] UV0 and UV1 complete; UV1 charts in 0–1 and non-overlapping by conservative chart-box test.
- [x] Material indices/submesh counts checked; 15 sets / 61 PNG maps decoded and channel packing checked.
- [x] GLB chunks, accessors, indices, tangents and embedded PNGs checked.
- [x] All 12 GLBs loaded through independent Trimesh 4.11.1; mesh counts and bounds matched.
- [x] FBX binary records, offsets, arrays, IDs/connections, units and relative texture paths checked by the supplied structural reader.
- [x] Actual-mesh renders reviewed; tube-frame shading corrected and re-exported.
- [x] Unity serialized asset references, deterministic GUIDs and static collider declarations checked locally.
- [x] Offline docs, provenance, AI disclosure and license notes included.

The machine-readable `Validation/validation_report.json` is the authority for the final local check count, not a made-up Editor report.

## Required before calling the pack production/Asset Store ready

- [ ] Run Blender reconstruction; open the resulting `.blend`; inspect editable groups, normals, UVs and PBR nodes.
- [ ] Independently import the FBX files in Blender/Unity or another FBX-capable DCC. Verify front direction and real-world scale, not just bounding size.
- [ ] Import the `.unitypackage` into a fresh Unity URP project; compile without package-origin errors or obsolete warnings.
- [ ] Run Setup, Rebuild Demo and Validate Imported Pack; save the actual UnityImportReport and Editor log.
- [ ] Confirm all 12 prefabs have visible meshes, correct materials, zero root transforms and correctly aligned colliders.
- [ ] Inspect both sides of signs, the hollow hat, wheelbarrow underside/tray, handle cavities and graphic-plane depth separation.
- [ ] Test Built-In separately if advertising it; remove unused pipeline content or supply a clearly documented alternative.
- [ ] Bake lighting at realistic target texel density; inspect chart margins and seams, regenerating UV1 when necessary.
- [ ] Check collision in Play Mode. Use compound/convex alternatives for dynamic rigidbodies.
- [ ] Profile actual target hardware; inspect texture memory, mipmaps, draw calls and repeated-prop use. No mobile/VR performance claim is prevalidated.
- [ ] Run the official Khronos glTF Validator for interchange assurance.
- [ ] Run Unity's current publishing validator; record the exact tested Editor/pipeline versions.
- [ ] Have the publisher review the marketing copy, original content, title clearance, license and AI disclosure. Do not upload unreviewed AI copy as a verified claim.
- [ ] Export through Unity after tests; submit the asset root rather than this outer delivery archive. Obtain Unity review/approval through the normal publishing process.
