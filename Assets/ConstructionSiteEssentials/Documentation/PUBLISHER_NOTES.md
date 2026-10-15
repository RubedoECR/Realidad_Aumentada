# Publisher notes — review before submission

**Current state:** generated submission candidate; real files, with external Editor tests outstanding. Do not use “Unity verified”, “Asset Store approved”, “tested in Unity 6” or guaranteed compatibility language for this untested build.

## Listing draft, subject to human review

**Title:** Construction Site Essentials

**Summary:** Twelve original static construction props with reusable PBR materials, individual FBX/GLB exports, editable source generation and organized Unity assets.

**Description draft:** Dress a compact construction scene with an orange-and-charcoal prop set: a traffic cone, safety barrier, hard hat, wheelbarrow, shovel, cement bag, toolbox, direction sign, portable twin-head work light, stack of 18 bricks, six-bar rebar bundle and folding caution sign. The pack includes 12 unique runtime meshes, 15 reusable texture sets, metre-scale ground pivots, two UV channels and actual-mesh previews. Geometry ranges from 592 to 6,480 triangles per prop, or 41,568 triangles for one of each. Thirty material assets provide URP and Built-In variants using the same 61 PNG maps. All props are static: no LOD variants, animations, interactive tooling or dynamic rigidbody setup are included.

**Before publishing, replace this status statement with tested evidence:** URP/Built-In are intended targets, not yet verified in a real Unity Editor. A Blender reconstruction script is included; the master delivery has not yet been converted to a `.blend`. Do not advertise a pre-generated `.blend` until you have run and inspected it.

**AI description:** Use the factual disclosure draft in PROVENANCE_AND_OWNERSHIP.md, then review it yourself. Do not call this hand-painted or human hand-modeled. The preview art must remain faithful to what is actually included.

## Practical upload preparation

Work in a clean test project. Run the supplied setup/rebuild/validation tools, inspect the result and finish the release checklist. Save an actual Editor log and report. Verify the exact Unity and render-pipeline version you intend to advertise.

Upload the organized `Assets/ConstructionSiteEssentials` folder through Unity's current publishing tools, not the outer delivery ZIP and not a nested `.unitypackage` hiding the runtime assets. The optional `ConstructionSiteEssentials_source.zip` contains non-Unity Python/data sources and is named explicitly as source. The main meshes/materials/prefabs remain directly accessible. Remove any unused material variant that would cause unavailable-shader warnings in a single-pipeline submission, or follow Unity's alternative-pipeline packaging guidance.

Preview images exist outside the Unity asset root for storefront use. They are honest renders but are not claimed to have the current Publisher Portal's required key-image dimensions; crop/resize to the portal's requested sizes without hiding the pack's content. The full-set image normalizes individual object sizes for inspection; only the hero depicts the group at a shared scale.

Add your real publisher identity, support contact, exact tested versions and final listing terms in the Publisher Portal. None have been invented here. This delivery has no analytics, account requirement, API key, remote download, paid runtime service or DRM.

## Official references checked 15 September 2026

- Unity Asset Store submission guidelines, last updated 20 May 2026: https://assetstore.unity.com/publishing/submission-guidelines
- Unity Asset Store Terms of Service and EULA: https://unity.com/legal/as-terms
- OpenAI Terms of Use: https://openai.com/policies/terms-of-use/

The submission guidelines require usable organized content, appropriate demos/prefabs, disclosure of AI-assisted functional content and publisher review. They do not guarantee approval. They permit appropriately named source archives for non-Unity source files. Recheck the official requirements at the time you submit; this document is not a substitute for them.
