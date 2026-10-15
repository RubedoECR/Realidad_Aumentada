# Construction Site Essentials — Unity setup

Version 1.0.1. Exactly 12 static props and 15 reusable material sets. The delivered assets were imported and validated in Unity 6.0.4f1 using the Built-In renderer; repeat the checks in your target pipeline before shipping. Default prefabs reference URP materials; a Built-In variant is also supplied. HDRP is not supplied.

Use a fresh test project. After importing/compilation, run the Tools → Construction Site Essentials menu in order:

1. Setup for Active Pipeline: recreates only this pack's materials and prefabs from the actual imported FBX mesh identities. It can overwrite your edits within this pack; back them up first.
2. Rebuild Demo Scene: saves a practical showcase with all 12 prefabs placed on a ground and backdrop, a tagged MainCamera, and key/fill/rim lights. Open `Assets/ConstructionSiteEssentials/Demo/CSE_Showcase.unity` directly to inspect the included demo before rebuilding it.
3. Validate Imported Pack: writes a real UnityImportReport.txt beside this README. Review the Console and report; then complete the manual checks in VALIDATION_CHECKLIST.md.
4. Export Unity Package: creates a Unity-native archive after your checks.

The static non-convex MeshColliders are for scenery, not dynamic pickups. All props have one detail level only. No rigs, animation, LODs or actual work-light Light components are included. ORM red is constant white rather than baked AO. See MATERIALS_AND_IMPORT.md for UV channels, map conventions and renderer limitations.

Editable source generation is in ../Source/ConstructionSiteEssentials_source.zip. Extract it outside Assets and follow its START_HERE.txt. Blender source and native exports are included in the source archive; the Unity package itself contains the runtime assets and the ready-to-open demo scene.

41,568 triangles total for one of each prop; individual counts are in ASSET_INVENTORY.csv. Finish genuine Editor import, rendering and publishing validation before claiming production or Asset Store readiness.
