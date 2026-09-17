#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// ItemCollection 전시/게임 프리팹 머티리얼이 하얗게 보이는 문제를 복구한다.
    /// - 소스 GUID 기반 복사본을 원본 속성으로 다시 채운다.
    /// - 텍스처 없는 버텍스 컬러 메시는 Vertex Color Simple Lit을 쓴다.
    /// </summary>
    public static class ItemCollectionMaterialFixer
    {
        private const string MaterialsFolder = "Assets/_Game/Content/ItemCollection/Materials";
        private const string PrefabsFolder = "Assets/_Game/Content/ItemCollection/Prefabs";
        private const string VertexColorShader = "Game/Item Collection/Vertex Color Simple Lit";
        private const string UrpLitShader = "Universal Render Pipeline/Lit";

        [MenuItem("Tools/Game/Assets/Fix ItemCollection Materials")]
        public static void Fix()
        {
            var vertexShader = Shader.Find(VertexColorShader);
            var litShader = Shader.Find(UrpLitShader);
            if (vertexShader == null || litShader == null)
                throw new System.Exception("Required shaders not found.");

            var report = new StringBuilder();
            var fixedMats = 0;
            var missingSource = 0;
            var alreadyOk = 0;

            var matGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder });
            foreach (var matGuid in matGuids)
            {
                var matPath = AssetDatabase.GUIDToAssetPath(matGuid);
                var fileName = Path.GetFileNameWithoutExtension(matPath);
                if (fileName.StartsWith("gallery_"))
                {
                    alreadyOk++;
                    continue;
                }

                var underscore = fileName.IndexOf('_');
                if (underscore != 32)
                {
                    report.AppendLine("skip-name " + fileName);
                    continue;
                }

                var sourceGuid = fileName.Substring(0, 32);
                var materialName = fileName.Substring(33);
                var sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuid);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    missingSource++;
                    report.AppendLine("missing-source " + fileName);
                    continue;
                }

                var sourceMaterial = FindSourceMaterial(sourcePath, materialName);
                if (sourceMaterial == null)
                {
                    missingSource++;
                    report.AppendLine("missing-mat " + fileName + " from " + sourcePath);
                    continue;
                }

                var collectionMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                var usesVertexColor = ShouldUseVertexColor(sourcePath, sourceMaterial, materialName);
                var rebuilt = RebuildMaterial(
                    sourceMaterial,
                    sourcePath,
                    materialName,
                    usesVertexColor,
                    vertexShader,
                    litShader);
                EditorUtility.CopySerialized(rebuilt, collectionMat);
                Object.DestroyImmediate(rebuilt);
                EditorUtility.SetDirty(collectionMat);
                fixedMats++;
            }

            // Prefab renderers should already reference collection materials; refresh dirty assets.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg =
                $"[ItemCollectionMaterialFixer] fixed={fixedMats} missingSource={missingSource} skippedGallery={alreadyOk}\n"
                + report;
            Debug.Log(msg);
            File.WriteAllText("Temp/ItemCollectionMaterialFix.log", msg);
        }

        private static Material FindSourceMaterial(string sourcePath, string materialName)
        {
            // Embedded materials on models/prefabs.
            var assets = AssetDatabase.LoadAllAssetsAtPath(sourcePath);
            foreach (var asset in assets)
            {
                if (asset is Material mat && mat.name == materialName)
                    return mat;
            }

            // Prefab referencing external materials.
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (root != null)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat.name == materialName)
                            return mat;
                    }
                }
            }

            // Fallback: material asset with same name under nearby folders.
            var folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                var hits = AssetDatabase.FindAssets(materialName + " t:Material", new[] { folder });
                foreach (var guid in hits)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat != null && mat.name == materialName)
                        return mat;
                }
            }

            return null;
        }

        private static bool ShouldUseVertexColor(string sourcePath, Material sourceMaterial, string materialName)
        {
            if (HasAlbedoOrMask(sourceMaterial))
                return false;

            // Only use vertex-color shader when meshes actually carry color attributes.
            return MeshHasNonWhiteVertexColors(sourcePath);
        }

        private static bool HasAlbedoOrMask(Material material)
        {
            foreach (var property in new[] { "_BaseMap", "_MainTex", "_Mask_Texture" })
            {
                if (material.HasProperty(property) && material.GetTexture(property) != null)
                    return true;
            }

            return false;
        }

        private static bool MeshHasNonWhiteVertexColors(string sourcePath)
        {
            foreach (var mesh in EnumerateMeshes(sourcePath))
            {
                if (!mesh.isReadable)
                    continue;
                var colors = mesh.colors;
                if (colors == null || colors.Length == 0)
                    continue;
                for (var i = 0; i < colors.Length; i++)
                {
                    var c = colors[i];
                    if (c.r < 0.98f || c.g < 0.98f || c.b < 0.98f)
                        return true;
                }
            }

            return false;
        }

        private static IEnumerable<Mesh> EnumerateMeshes(string sourcePath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sourcePath))
            {
                if (asset is Mesh mesh)
                    yield return mesh;
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (root == null)
                yield break;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                    yield return filter.sharedMesh;
            }
        }

        private static Material RebuildMaterial(
            Material original,
            string sourcePath,
            string materialName,
            bool usesVertexColor,
            Shader vertexShader,
            Shader litShader)
        {
            // Keep Shader Graph materials intact (bathroom mask workflow).
            if (original.shader != null && original.shader.name.StartsWith("Shader Graphs/"))
                return new Material(original);

            var hasBaseMap = HasAlbedoOrMask(original);
            Material material;
            if (usesVertexColor && !hasBaseMap)
            {
                material = new Material(vertexShader);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.25f);
                material.EnableKeyword("_SPECULAR_COLOR");
            }
            else
            {
                material = new Material(litShader);
                var textureKey = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (original.HasProperty(textureKey) && original.GetTexture(textureKey) != null)
                {
                    material.SetTexture("_BaseMap", original.GetTexture(textureKey));
                    material.SetTextureScale("_BaseMap", original.GetTextureScale(textureKey));
                    material.SetTextureOffset("_BaseMap", original.GetTextureOffset(textureKey));
                }

                if (sourcePath.Contains("/Low_Poly_Weapons_VOL1/") && material.GetTexture("_BaseMap") == null)
                {
                    material.SetTexture(
                        "_BaseMap",
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            "Assets/ItemSources/Low_Poly_Weapons_VOL1/Low Poly Weapons VOL.1/Textures_Guns.png"));
                }

                Color baseColor;
                if (original.HasProperty("_BaseColor"))
                    baseColor = original.GetColor("_BaseColor");
                else if (original.HasProperty("_Color"))
                    baseColor = original.GetColor("_Color");
                else
                    baseColor = Color.white;

                // Kabungus HouseholdItems lost atlas textures on import; paint flat colors from names.
                if (!hasBaseMap && material.GetTexture("_BaseMap") == null
                    && (materialName == "SimpleItems" || sourcePath.Contains("/HouseholdItems/")))
                {
                    baseColor = ColorFromAssetName(Path.GetFileNameWithoutExtension(sourcePath));
                }

                material.SetColor("_BaseColor", baseColor);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", baseColor);

                if (original.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", original.GetFloat("_Metallic"));
                material.SetFloat(
                    "_Smoothness",
                    original.HasProperty("_Smoothness")
                        ? original.GetFloat("_Smoothness")
                        : original.HasProperty("_Glossiness")
                            ? original.GetFloat("_Glossiness")
                            : 0.25f);

                foreach (var property in new[] { "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_BumpMap" })
                {
                    if (!original.HasProperty(property) || original.GetTexture(property) == null)
                        continue;
                    material.SetTexture(property, original.GetTexture(property));
                    if (property == "_BumpMap")
                        material.EnableKeyword("_NORMALMAP");
                }

                if (original.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", original.GetColor("_EmissionColor"));
            }

            material.name = original.name;
            return material;
        }

        public static Color ColorFromAssetName(string name)
        {
            var tokens = new (string token, Color color)[]
            {
                ("Blue", new Color(0.25f, 0.45f, 0.85f)),
                ("Red", new Color(0.85f, 0.22f, 0.18f)),
                ("Green", new Color(0.28f, 0.72f, 0.32f)),
                ("Orange", new Color(0.95f, 0.55f, 0.15f)),
                ("Purple", new Color(0.55f, 0.28f, 0.75f)),
                ("Yellow", new Color(0.95f, 0.82f, 0.2f)),
                ("Pink", new Color(0.95f, 0.45f, 0.65f)),
                ("Brown", new Color(0.55f, 0.32f, 0.18f)),
                ("Grey", new Color(0.55f, 0.55f, 0.58f)),
                ("Gray", new Color(0.55f, 0.55f, 0.58f)),
                ("Black", new Color(0.12f, 0.12f, 0.14f)),
                ("White", new Color(0.92f, 0.92f, 0.94f)),
            };
            foreach (var (token, color) in tokens)
            {
                if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return color;
            }

            var keywords = new (string token, Color color)[]
            {
                ("Book", new Color(0.35f, 0.45f, 0.7f)),
                ("Mug", new Color(0.75f, 0.55f, 0.35f)),
                ("Beer", new Color(0.72f, 0.5f, 0.28f)),
                ("Cup", new Color(0.85f, 0.85f, 0.88f)),
                ("Bottle", new Color(0.35f, 0.55f, 0.45f)),
                ("Hammer", new Color(0.45f, 0.45f, 0.48f)),
                ("Hatchet", new Color(0.4f, 0.42f, 0.45f)),
                ("Knife", new Color(0.7f, 0.72f, 0.75f)),
                ("Wrench", new Color(0.75f, 0.55f, 0.15f)),
                ("Saw", new Color(0.6f, 0.62f, 0.65f)),
                ("Crowbar", new Color(0.35f, 0.2f, 0.15f)),
                ("Toolbox", new Color(0.85f, 0.25f, 0.2f)),
                ("Pan", new Color(0.2f, 0.2f, 0.22f)),
                ("Pot", new Color(0.25f, 0.25f, 0.28f)),
                ("Plate", new Color(0.9f, 0.9f, 0.92f)),
                ("Bowl", new Color(0.88f, 0.88f, 0.9f)),
                ("Chair", new Color(0.55f, 0.35f, 0.2f)),
                ("Table", new Color(0.5f, 0.32f, 0.18f)),
                ("Desk", new Color(0.48f, 0.3f, 0.16f)),
                ("TV", new Color(0.15f, 0.15f, 0.18f)),
                ("Phone", new Color(0.18f, 0.18f, 0.2f)),
                ("Camera", new Color(0.2f, 0.2f, 0.22f)),
                ("Flashlight", new Color(0.85f, 0.75f, 0.2f)),
                ("Bat", new Color(0.55f, 0.35f, 0.2f)),
                ("Wood", new Color(0.55f, 0.35f, 0.18f)),
                ("Metal", new Color(0.55f, 0.58f, 0.62f)),
            };
            foreach (var (token, color) in keywords)
            {
                if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return color;
            }

            // Deterministic non-white fallback so items stay distinguishable.
            var hash = (uint)Animator.StringToHash(name);
            var hue = (hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.78f);
        }
    }
}
#endif
