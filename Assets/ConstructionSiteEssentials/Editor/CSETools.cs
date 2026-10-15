// Construction Site Essentials. Original package-local editor tooling.
// No network access, telemetry, external binaries, packages or runtime code.
// Intended for Unity 2022.3+ / Unity 6. Actual Editor verification is pending.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConstructionSiteEssentials.Editor
{
    [Serializable] public sealed class CSEMaterialSpec
    {
        public string name;
        public float metallic;
        public float roughness;
    }
    [Serializable] public sealed class CSEPropSpec
    {
        public string name;
        public string[] materials;
        public int triangles;
        public float[] dimensions;
        public float[] demoPosition;
    }
    [Serializable] public sealed class CSEManifest
    {
        public string version;
        public CSEMaterialSpec[] materials;
        public CSEPropSpec[] props;
    }

    public static class CSETools
    {
        public const string Root = "Assets/ConstructionSiteEssentials";
        private static CSEManifest ReadManifest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Editor/CSEManifest.json");
            if (asset == null) throw new FileNotFoundException("Missing CSEManifest.json in the package Editor folder.");
            var data = JsonUtility.FromJson<CSEManifest>(asset.text);
            if (data == null || data.props == null || data.props.Length != 12)
                throw new InvalidDataException("The manifest must describe exactly 12 props.");
            return data;
        }
        private static bool IsURP()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return false;
            if (!pipeline.GetType().Name.Contains("UniversalRenderPipeline"))
                throw new NotSupportedException("This pack supplies URP and Built-In materials, not HDRP materials.");
            return true;
        }
        private static Texture2D Texture(string name, string suffix)
        {
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/CSE_" + name + "_" + suffix + ".png");
            if (result == null) throw new FileNotFoundException("Missing texture: " + name + " / " + suffix);
            return result;
        }
        private static Material MaterialFor(string name, bool urp)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + (urp ? "URP" : "BuiltIn") + "/CSE_" + name + ".mat");
        }

        [MenuItem("Tools/Construction Site Essentials/1 - Setup for Active Pipeline")]
        public static void Setup()
        {
            if (!EditorUtility.DisplayDialog("Construction Site Essentials",
                "Rebuild this pack's materials and prefabs from its FBX files? Existing edits inside this pack may be replaced. No unrelated assets or project packages will be changed.", "Rebuild Pack", "Cancel")) return;
            try { Rebuild(); Debug.Log("CSE: materials and 12 prefabs rebuilt. Run validation next."); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private static void Rebuild()
        {
            var data = ReadManifest(); bool urp = IsURP();
            var shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) throw new InvalidOperationException("The active pipeline's Lit shader was not found.");
            foreach (var spec in data.materials)
            {
                var mat = MaterialFor(spec.name, urp);
                string path = Root + "/Materials/" + (urp ? "URP" : "BuiltIn") + "/CSE_" + spec.name + ".mat";
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
                mat.SetTexture(urp ? "_BaseMap" : "_MainTex", Texture(spec.name, "BaseColor"));
                mat.SetTexture("_BumpMap", Texture(spec.name, "Normal"));
                mat.SetFloat("_BumpScale", 0.5f); mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap", Texture(spec.name, "MetallicSmoothness"));
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 1f);
                mat.EnableKeyword(urp ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
                mat.enableInstancing = true;
                if (spec.name == "WorklightLens")
                {
                    mat.SetTexture("_EmissionMap", Texture(spec.name, "Emission"));
                    mat.SetColor("_EmissionColor", Color.white); mat.EnableKeyword("_EMISSION");
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            foreach (var spec in data.props)
            {
                string modelPath = Root + "/Models/CSE_" + spec.name + ".fbx";
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var mesh = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().FirstOrDefault();
                if (mesh == null) throw new InvalidDataException("No mesh imported from " + modelPath);
                var materials = spec.materials.Select(name => MaterialFor(name, urp)).ToArray();
                if (materials.Any(item => item == null)) throw new InvalidDataException("Missing material for " + spec.name);
                var go = new GameObject("CSE_" + spec.name);
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    // Static, non-convex collision. Do not add a non-kinematic Rigidbody.
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go.transform.localScale = Vector3.one;
                    PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/CSE_" + spec.name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Construction Site Essentials/2 - Rebuild Demo Scene")]
        public static void RebuildDemo()
        {
            if (!EditorUtility.DisplayDialog("Rebuild CSE demo", "Replaces only this pack's demo scene. Save any other scene edits first.", "Continue", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try { BuildDemo(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private static void BuildDemo()
        {
            var data = ReadManifest(); bool urp = IsURP();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Give reviewers an immediately readable, practical showcase scene.
            // The earlier scene placed the props against the default sky/background,
            // which made it easy to miss that a complete demo was included.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "CSE_DemoGround";
            ground.transform.SetPositionAndRotation(new Vector3(0f, -0.08f, 0f), Quaternion.identity);
            ground.transform.localScale = new Vector3(6.5f, 0.16f, 5.5f);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            var groundMaterial = MaterialFor("CharcoalPaint", urp);
            if (groundRenderer != null && groundMaterial != null) groundRenderer.sharedMaterial = groundMaterial;

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "CSE_DemoBackdrop";
            backdrop.transform.SetPositionAndRotation(new Vector3(0f, 1.9f, -2.7f), Quaternion.identity);
            backdrop.transform.localScale = new Vector3(6.5f, 3.8f, 0.16f);
            var backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            var backdropMaterial = MaterialFor("TealPaint", urp);
            if (backdropRenderer != null && backdropMaterial != null) backdropRenderer.sharedMaterial = backdropMaterial;

            foreach (var spec in data.props)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CSE_" + spec.name + ".prefab");
                if (prefab == null) throw new InvalidDataException("Missing prefab " + spec.name);
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                obj.transform.position = new Vector3(spec.demoPosition[0], spec.demoPosition[1], spec.demoPosition[2]);
            }
            var cameraObject = new GameObject("CSE_DemoCamera"); var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(6.4f, 5.2f, 7.4f); camera.transform.LookAt(new Vector3(0, 0.7f, 0));
            camera.orthographic = false; camera.fieldOfView = 46f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.85f, 0.87f, 0.85f);
            var lightObject = new GameObject("CSE_KeyLight"); var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.8f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45, -28, 0);
            var fill = new GameObject("CSE_FillLight").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.65f; fill.transform.rotation = Quaternion.Euler(38, 142, 0);
            var rim = new GameObject("CSE_RimLight").AddComponent<Light>();
            rim.type = LightType.Directional; rim.intensity = 0.35f; rim.transform.rotation = Quaternion.Euler(25, 28, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.68f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.48f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.25f, 0.26f);
            EditorSceneManager.SaveScene(scene, Root + "/Demo/CSE_Showcase.unity");
            Debug.Log("CSE: rebuilt demo scene for " + (urp ? "URP" : "Built-In") + ".");
        }

        [MenuItem("Tools/Construction Site Essentials/3 - Validate Imported Pack")]
        public static void ValidateMenu()
        {
            try { int errors = ValidateImported(); Debug.Log("CSE validation complete: " + errors + " error(s). See Documentation/UnityImportReport.txt."); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        public static int ValidateImported()
        {
            var data = ReadManifest(); int errors = 0; var report = new StringBuilder();
            report.AppendLine("Construction Site Essentials — actual Unity import check");
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine("Pipeline: " + (GraphicsSettings.currentRenderPipeline == null ? "Built-In" : GraphicsSettings.currentRenderPipeline.name));
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            foreach (var spec in data.props)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CSE_" + spec.name + ".prefab");
                string message = "PASS";
                if (go == null) message = "FAIL: missing prefab";
                else
                {
                    var filter = go.GetComponent<MeshFilter>(); var renderer = go.GetComponent<MeshRenderer>();
                    if (filter == null || filter.sharedMesh == null) message = "FAIL: missing mesh; run Setup";
                    else if (renderer == null || renderer.sharedMaterials.Any(mat => mat == null || mat.shader == null || !mat.shader.isSupported)) message = "FAIL: missing/unsupported material";
                    else if (filter.sharedMesh.subMeshCount != renderer.sharedMaterials.Length) message = "FAIL: material/submesh mismatch";
                    else if (go.transform.localPosition != Vector3.zero || go.transform.localRotation != Quaternion.identity || go.transform.localScale != Vector3.one) message = "FAIL: root transforms";
                    else if (Mathf.Abs(filter.sharedMesh.bounds.min.y) > 0.01f) message = "FAIL: pivot is not on the floor";
                    else if (go.GetComponent<MeshCollider>() == null || go.GetComponent<MeshCollider>().sharedMesh == null) message = "FAIL: missing static collider";
                    else
                    {
                        var actual = filter.sharedMesh.bounds.size;
                        var expected = new Vector3(spec.dimensions[0], spec.dimensions[2], spec.dimensions[1]);
                        if ((actual - expected).magnitude > 0.02f) message = "FAIL: unexpected scale/axes " + actual;
                        ulong indices = 0;
                        for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++) indices += filter.sharedMesh.GetIndexCount(sub);
                        if (indices / 3 != (ulong)spec.triangles) message = "FAIL: triangle count mismatch";
                    }
                }
                if (message.StartsWith("FAIL", StringComparison.Ordinal)) errors++;
                report.AppendLine(spec.name + ": " + message);
            }
            report.AppendLine("Still manually inspect UV lightmap baking, collider behaviour, renders and target-device performance.");
            report.AppendLine("Errors: " + errors);
            File.WriteAllText(Root + "/Documentation/UnityImportReport.txt", report.ToString());
            AssetDatabase.Refresh(); return errors;
        }
        // Example: Unity -batchmode -projectPath <project> -executeMethod
        // ConstructionSiteEssentials.Editor.CSETools.BatchValidate -logFile <log>
        public static void BatchValidate()
        {
            try { Rebuild(); BuildDemo(); EditorApplication.Exit(ValidateImported() == 0 ? 0 : 1); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(2); }
        }

        // Headless export used for the corrected 1.0.1 submission package.
        public static void BatchExport101()
        {
            try
            {
                var output = Environment.GetEnvironmentVariable("CSE_EXPORT_PATH");
                if (string.IsNullOrEmpty(output)) output = "ConstructionSiteEssentials_1.0.1.unitypackage";
                if (File.Exists(output)) File.Delete(output);
                AssetDatabase.ExportPackage(Root, output, ExportPackageOptions.Recurse);
                Debug.Log("CSE export complete: " + output);
                EditorApplication.Exit(File.Exists(output) ? 0 : 1);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(2); }
        }

        [MenuItem("Tools/Construction Site Essentials/4 - Export Unity Package")]
        public static void Export()
        {
            var path = EditorUtility.SaveFilePanel("Export validated CSE package", "", "ConstructionSiteEssentials_1.0.0", "unitypackage");
            if (!string.IsNullOrEmpty(path)) AssetDatabase.ExportPackage(Root, path, ExportPackageOptions.Recurse);
        }
    }

    public sealed class CSEImportSettings : AssetPostprocessor
    {
        private bool Ours { get { return assetPath.StartsWith(CSETools.Root + "/", StringComparison.Ordinal); } }
        private void OnPreprocessModel()
        {
            if (!Ours) return;
            var model = (ModelImporter)assetImporter;
            model.globalScale = 1; model.useFileScale = true; model.bakeAxisConversion = true;
            model.importAnimation = false; model.importCameras = false; model.importLights = false;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.generateSecondaryUV = false;
        }
        private Material OnAssignMaterialModel(Material source, Renderer renderer)
        {
            if (!Ours) return null;
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            string name = source.name.StartsWith("CSE_", StringComparison.Ordinal) ? source.name : "CSE_" + source.name;
            return AssetDatabase.LoadAssetAtPath<Material>(CSETools.Root + "/Materials/" + (urp ? "URP" : "BuiltIn") + "/" + name + ".mat");
        }
        private void OnPreprocessTexture()
        {
            if (!Ours) return;
            var texture = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_Normal.png", StringComparison.Ordinal);
            texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            texture.sRGBTexture = assetPath.EndsWith("_BaseColor.png", StringComparison.Ordinal) || assetPath.EndsWith("_Emission.png", StringComparison.Ordinal);
            texture.mipmapEnabled = true; texture.wrapMode = TextureWrapMode.Repeat;
            texture.alphaIsTransparency = false;
        }
    }
}
#endif
