#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Client.Interactions;
using Game.SOAP.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Casual Furniture 팩의 Prefabs/toy를 toys(장난감) 카테고리로 등록한다.
    /// </summary>
    public static class ToysCategoryImporter
    {
        private const string CategoryId = "toys";
        private const string CategoryLabel = "장난감";
        private const string Package = "Casual Furniture - Low Poly 3D Models Pack";
        private const string SourceFolder = "Assets/ItemSources/ithappy/Furniture_Casual/Prefabs/toy";
        private const string CharacterPrefab = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        [MenuItem("Tools/Game/Items/Add Toys Category")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            var sources = AssetDatabase.FindAssets("t:Prefab", new[] { SourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
            if (sources.Length == 0)
                throw new InvalidOperationException("No toy prefabs in " + SourceFolder);

            var maxSize = MeasureCharacterSize();
            EnsurePrefabFolder(CategoryId);
            var prefabDir = $"{ItemCollectionBuilder.Folder}/Prefabs/{CategoryId}";

            var manifestPath = ItemCollectionBuilder.Folder + "/ItemCollection.json";
            var items = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(manifestPath))
                .items?.ToList() ?? new List<ItemCollectionBuilder.Entry>();
            items.RemoveAll(e => e.category == CategoryId);

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogSetup.Path)
                          ?? throw new InvalidOperationException("Missing ItemCatalog.asset");

            var category = catalog.categories.FirstOrDefault(c => c.id == CategoryId);
            if (category == null)
            {
                category = new ItemCatalogSO.Category
                {
                    id = CategoryId,
                    label = CategoryLabel,
                    enabled = true,
                    items = new List<ItemCatalogSO.Item>(),
                };
                catalog.categories.Add(category);
            }
            else
            {
                category.label = CategoryLabel;
                category.enabled = true;
                category.items.Clear();
            }

            foreach (var old in AssetDatabase.FindAssets("t:Prefab", new[] { prefabDir })
                         .Select(AssetDatabase.GUIDToAssetPath).ToArray())
                AssetDatabase.DeleteAsset(old);

            var created = new List<(ItemCollectionBuilder.Entry entry, string destPath, string catalogId)>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var sourcePath in sources)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    if (source == null)
                        throw new FileNotFoundException(sourcePath);

                    var family = Path.GetFileNameWithoutExtension(sourcePath);
                    var entryId = "toys_" + StableHash10(sourcePath);
                    var catalogId = "i" + entryId.Substring(entryId.LastIndexOf('_') + 1);
                    var destPath = $"{prefabDir}/{entryId}.prefab";

                    var wrapper = BuildCarryable(source, CategoryLabel, maxSize, out var originalSize, out var finalSize, out var scale);
                    PrefabUtility.SaveAsPrefabAsset(wrapper, destPath);
                    UnityEngine.Object.DestroyImmediate(wrapper);

                    created.Add((
                        new ItemCollectionBuilder.Entry
                        {
                            id = entryId,
                            source = sourcePath,
                            originalSource = sourcePath,
                            package = Package,
                            category = CategoryId,
                            categoryName = CategoryLabel,
                            displayName = CategoryLabel,
                            note = "",
                            family = family,
                            prefab = destPath,
                            originalSize = originalSize,
                            finalSize = finalSize,
                            scale = scale,
                        },
                        destPath,
                        catalogId));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var (entry, destPath, catalogId) in created)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
                if (prefab == null || prefab.GetComponent<CarryableItem>() == null)
                    throw new InvalidOperationException("Missing carryable prefab after save: " + destPath);
                if (catalogId.Length > 16)
                    throw new InvalidOperationException("Catalog ID too long: " + catalogId);

                category.items.Add(new ItemCatalogSO.Item
                {
                    id = catalogId,
                    displayName = CategoryLabel,
                    enabled = true,
                    prefab = prefab,
                });
                items.Add(entry);
            }

            catalog.categories = catalog.categories.OrderBy(c => CategoryOrder(c.id)).ToList();
            WriteManifest(manifestPath, items);
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;

            var summary =
                $"[ToysCategoryImporter] added {created.Count} items to '{CategoryLabel}' ({CategoryId}).";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/ToysCategoryImporter.log", summary, Encoding.UTF8);
        }

        private static void EnsurePrefabFolder(string category)
        {
            var root = ItemCollectionBuilder.Folder + "/Prefabs";
            var folder = $"{root}/{category}";
            if (AssetDatabase.IsValidFolder(folder))
                return;
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder(ItemCollectionBuilder.Folder, "Prefabs");
            if (Directory.Exists(folder) && !File.Exists(folder + ".meta"))
                Directory.Delete(folder, true);
            AssetDatabase.CreateFolder(root, category);
            AssetDatabase.Refresh();
        }

        private static Vector3 MeasureCharacterSize()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            var visual = player.transform.Find("Visual")
                         ?? throw new InvalidOperationException("Player Visual missing");
            var temp = UnityEngine.Object.Instantiate(visual.gameObject);
            try { return BoundsOf(temp).size; }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
        }

        private static GameObject BuildCarryable(
            GameObject source,
            string displayName,
            Vector3 maxSize,
            out Vector3 originalSize,
            out Vector3 finalSize,
            out float scale)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            originalSize = BoundsOf(instance).size;
            scale = Mathf.Min(
                1f,
                maxSize.x / Mathf.Max(0.00001f, originalSize.x),
                maxSize.y / Mathf.Max(0.00001f, originalSize.y),
                maxSize.z / Mathf.Max(0.00001f, originalSize.z));

            var wrapper = new GameObject(displayName);
            instance.transform.SetParent(wrapper.transform, false);
            instance.transform.localScale *= scale;

            foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                UnityEngine.Object.DestroyImmediate(script);
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(ItemCollectionBuilder.CompatibleMaterial).ToArray();

            var bounds = BoundsOf(wrapper);
            instance.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = BoundsOf(wrapper);
            finalSize = bounds.size;

            if (scale <= 0f || scale > 1f || float.IsNaN(scale) || finalSize.sqrMagnitude < 1e-10f ||
                finalSize.x > maxSize.x + 0.001f || finalSize.y > maxSize.y + 0.001f ||
                finalSize.z > maxSize.z + 0.001f)
            {
                UnityEngine.Object.DestroyImmediate(wrapper);
                throw new InvalidOperationException("Invalid size for " + displayName);
            }

            var box = wrapper.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.005f);
            wrapper.AddComponent<Rigidbody>().isKinematic = true;
            var carry = wrapper.AddComponent<CarryableItem>();
            var so = new SerializedObject(carry);
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            return wrapper;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var found = false;
            var bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            if (!found)
                throw new InvalidOperationException("No visible mesh: " + root.name);
            return bounds;
        }

        private static string StableHash10(string value)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            var sb = new StringBuilder(10);
            for (var i = 0; i < 5; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        private static int CategoryOrder(string id) => id switch
        {
            "food" => 0,
            "household" => 1,
            "bathroom" => 2,
            "plants" => 3,
            "tools" => 4,
            "modern" => 5,
            "fantasy" => 6,
            "beach" => 7,
            "casino" => 8,
            "halloween" => 9,
            "toys" => 10,
            "reserve" => 11,
            _ => 50,
        };

        private static void WriteManifest(string path, List<ItemCollectionBuilder.Entry> items)
        {
            var ordered = items
                .OrderBy(e => CategoryOrder(e.category))
                .ThenBy(e => e.source, StringComparer.Ordinal)
                .ToArray();
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"items\": [");
            for (var i = 0; i < ordered.Length; i++)
            {
                var e = ordered[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"source\": {J(e.source)},");
                sb.AppendLine($"      \"package\": {J(e.package)},");
                sb.AppendLine($"      \"category\": {J(e.category)},");
                sb.AppendLine($"      \"categoryName\": {J(e.categoryName)},");
                sb.AppendLine($"      \"displayName\": {J(e.displayName)},");
                sb.AppendLine($"      \"note\": {J(e.note ?? "")},");
                sb.AppendLine($"      \"originalSource\": {J(e.originalSource)},");
                sb.AppendLine($"      \"id\": {J(e.id)},");
                sb.AppendLine($"      \"family\": {J(e.family)}");
                sb.Append(i == ordered.Length - 1 ? "    }\n" : "    },\n");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string J(string value) =>
            "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
#endif
