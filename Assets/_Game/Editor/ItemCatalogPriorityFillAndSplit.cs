#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Client.Interactions;
using Game.SOAP.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 1순위: tools/modern을 20개 이상으로 충원(미빌드 프리팹 생성 포함).
    /// 2순위: household에서 bathroom·plants 분리.
    /// </summary>
    public static class ItemCatalogPriorityFillAndSplit
    {
        private const string PrefabRoot = ItemCollectionBuilder.Folder + "/Prefabs";
        private const string CharacterPrefab = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        // JSON에는 있으나 프리팹이 없던 tools → 빌드 후 카탈로그 추가 (15→23)
        private static readonly string[] ToolBuilds =
        {
            "tools_58256b3aaf", // Crowbar
            "tools_620e5958e0", // Flashlight
            "tools_e4ff06e4d3", // GasCan
            "tools_b2e0f6c6e2", // Hatchet
            "tools_2a6dab02c5", // ScrewDriverBlue
            "tools_3695a1c260", // Shovel
            "tools_4f0817e5c1", // TapeMeasurer
            "tools_5564f300dd", // Basement_Tools_Vise
        };

        // reserve JSON + 무기 악세서리 소스 → modern으로 빌드·이동 (16→20)
        private static readonly string[] ModernFromReserveBuilds =
        {
            "reserve_6befb51510", // AmmoCase
            "reserve_8793e53949", // pistol_drum_001
            "reserve_aeee61587f", // rifle_magazine_001
            "reserve_f1c3736732", // ANPEQ15
        };

        private static readonly string[] BathroomIds =
        {
            "household_f598eb7607", "household_ca9d889ba9", "household_204de6857f",
            "household_9881c5c43b", "household_89f3c6d3c9", "household_f89d1ee5b9",
            "household_d417da86ab", "household_f05c88d061", "household_34ac53ea84",
            "household_cdce725984", "household_156fe0ba0b", "household_7ad4eafba8",
            "household_066e38a6ff", "household_072b375834", "household_957b15eb7c",
            "household_ccdc8322c2", "household_4a8e69ba9e", "household_16137d462d",
            "household_992a868dab",
        };

        private static readonly string[] PlantsIds =
        {
            "household_99f9e167ce", "household_470b7c5d52", "household_2d18c6fa17",
            "household_a748381168", "household_e8f6140412", "household_2d23115cd4",
            "household_036176ef14", "household_1b99880120", "household_0e10b11f29",
            "household_0b766f425d", "household_aa0e710e7e", "household_9e3de3ca83",
            "household_18405a29e2", "household_1f65f42023", "household_72e8a78f9c",
            "household_a795ce75f0", "household_69b0ae4309",
        };

        [MenuItem("Tools/Game/Items/Fill Tools Modern And Split Bathroom Plants")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogSetup.Path)
                          ?? throw new InvalidOperationException("Missing ItemCatalog.asset");

            var manifestPath = ItemCollectionBuilder.Folder + "/ItemCollection.json";
            var entries = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(manifestPath))
                .items.ToList();
            var maxSize = MeasureCharacterSize();
            var log = new StringBuilder();

            var tools = EnsureCategory(catalog, "tools", "공구·작업용품", true);
            foreach (var id in ToolBuilds)
            {
                EnsureCollectionPrefab(entries, id, maxSize, log);
                AddFromCollection(catalog, tools, entries, id, log, "+tools");
            }

            var modern = EnsureCategory(catalog, "modern", "총기·폭발물", true);
            foreach (var id in ModernFromReserveBuilds)
            {
                EnsureCollectionPrefab(entries, id, maxSize, log);
                MoveCollectionItem(catalog, entries, id, "modern", "총기·폭발물", modern, log);
            }

            var bathroom = EnsureCategory(catalog, "bathroom", "욕실용품", true);
            bathroom.items.Clear();
            foreach (var id in BathroomIds)
                MoveCollectionItem(catalog, entries, id, "bathroom", "욕실용품", bathroom, log);

            var plants = EnsureCategory(catalog, "plants", "화분·식물", true);
            plants.items.Clear();
            foreach (var id in PlantsIds)
                MoveCollectionItem(catalog, entries, id, "plants", "화분·식물", plants, log);

            catalog.categories = catalog.categories.OrderBy(c => CategoryOrder(c.id)).ToList();
            WriteManifest(manifestPath, entries);
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;

            var household = EnsureCategory(catalog, "household", "생활용품", true);
            var summary =
                $"[PriorityFillAndSplit] tools={EnabledCount(tools)} modern={EnabledCount(modern)} " +
                $"bathroom={EnabledCount(bathroom)} plants={EnabledCount(plants)} household={EnabledCount(household)}\n{log}";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/ItemCatalogPriorityFillAndSplit.log", summary);
        }

        private static void EnsureCollectionPrefab(
            List<ItemCollectionBuilder.Entry> entries,
            string collectionId,
            Vector3 maxSize,
            StringBuilder log)
        {
            var entry = entries.FirstOrDefault(e => e.id == collectionId)
                        ?? throw new InvalidOperationException("Missing entry " + collectionId);
            var path = $"{PrefabRoot}/{entry.category}/{collectionId}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;

            // Idempotent: already moved/renamed on a previous partial run.
            var suffix = collectionId.Substring(collectionId.LastIndexOf('_') + 1);
            foreach (var category in new[] { "modern", "tools", "bathroom", "plants", entry.category })
            {
                var renamed = $"{PrefabRoot}/{category}/{category}_{suffix}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(renamed) != null)
                {
                    entry.prefab = renamed;
                    return;
                }
            }

            var source = LoadSourcePrefab(entry.source);
            var wrapper = BuildCarryable(source, entry.displayName, maxSize);
            EnsurePrefabFolder(entry.category);
            PrefabUtility.SaveAsPrefabAsset(wrapper, path);
            UnityEngine.Object.DestroyImmediate(wrapper);
            entry.prefab = path;
            log.AppendLine($"build {collectionId} {entry.displayName}");
            AssetDatabase.ImportAsset(path);
        }

        private static GameObject LoadSourcePrefab(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new FileNotFoundException("Missing source", path);

            // Low Poly Weapons VOL1 prefabs may have null meshes — fall back to model.
            if (path.Contains("/Low_Poly_Weapons_VOL1/") && path.EndsWith(".prefab") &&
                asset.GetComponentsInChildren<MeshFilter>(true).Any(m => m.sharedMesh == null))
            {
                var model = AssetDatabase.FindAssets("t:Model", new[] { "Assets/ItemSources/Low_Poly_Weapons_VOL1" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Single(p => Path.GetFileNameWithoutExtension(p) == Path.GetFileNameWithoutExtension(path));
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(model)
                        ?? throw new FileNotFoundException("Missing weapons model", model);
            }

            return asset;
        }

        private static GameObject BuildCarryable(GameObject source, string displayName, Vector3 maxSize)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            var originalSize = BoundsOf(instance).size;
            var scale = Mathf.Min(
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

            if (scale <= 0f || scale > 1f || float.IsNaN(scale) || bounds.size.sqrMagnitude < 1e-10f ||
                bounds.size.x > maxSize.x + 0.001f || bounds.size.y > maxSize.y + 0.001f ||
                bounds.size.z > maxSize.z + 0.001f)
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

        private static void AddFromCollection(
            ItemCatalogSO catalog,
            ItemCatalogSO.Category target,
            List<ItemCollectionBuilder.Entry> entries,
            string collectionId,
            StringBuilder log,
            string tag)
        {
            var entry = entries.FirstOrDefault(e => e.id == collectionId)
                        ?? throw new InvalidOperationException("Missing entry " + collectionId);
            var catalogId = ToCatalogId(collectionId);
            if (FindItem(catalog, catalogId) != null)
                return;

            var prefab = LoadCarryable($"{PrefabRoot}/{entry.category}/{collectionId}.prefab");
            target.items.Add(new ItemCatalogSO.Item
            {
                id = catalogId,
                displayName = entry.displayName,
                enabled = true,
                prefab = prefab,
            });
            log.AppendLine($"{tag} {catalogId} {entry.displayName}");
        }

        private static void MoveCollectionItem(
            ItemCatalogSO catalog,
            List<ItemCollectionBuilder.Entry> entries,
            string collectionId,
            string toCategory,
            string toLabel,
            ItemCatalogSO.Category target,
            StringBuilder log)
        {
            var entry = entries.FirstOrDefault(e => e.id == collectionId);
            if (entry == null)
            {
                // Already renamed on a previous partial run.
                var suffix = collectionId.Substring(collectionId.LastIndexOf('_') + 1);
                var renamedId = toCategory + "_" + suffix;
                entry = entries.FirstOrDefault(e => e.id == renamedId);
            }

            if (entry == null)
                throw new InvalidOperationException("Missing entry " + collectionId);

            var catalogId = ToCatalogId(collectionId);
            var newCollectionId = toCategory + "_" + collectionId.Substring(collectionId.LastIndexOf('_') + 1);
            var oldPath = $"{PrefabRoot}/{entry.category}/{entry.id}.prefab";
            var newPath = $"{PrefabRoot}/{toCategory}/{newCollectionId}.prefab";

            // If JSON still has old id but prefab already moved.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) == null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(newPath) != null)
            {
                entry.category = toCategory;
                entry.categoryName = toLabel;
                entry.id = newCollectionId;
                entry.prefab = newPath;
                oldPath = newPath;
            }

            EnsurePrefabFolder(toCategory);
            var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(oldPath);
            var newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPath);
            if (oldPrefab != null && newPrefab != null && oldPath != newPath)
            {
                // Partial prior run left both copies — keep destination.
                AssetDatabase.DeleteAsset(oldPath);
                oldPrefab = null;
            }

            if (oldPrefab != null && newPrefab == null)
            {
                var error = AssetDatabase.MoveAsset(oldPath, newPath);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException($"{oldPath} -> {newPath}: {error}");
            }

            var prefabPath = AssetDatabase.LoadAssetAtPath<GameObject>(newPath) != null ? newPath : oldPath;
            var prefab = LoadCarryable(prefabPath);
            RemoveItemEverywhere(catalog, catalogId);
            target.items.Add(new ItemCatalogSO.Item
            {
                id = catalogId,
                displayName = entry.displayName,
                enabled = true,
                prefab = prefab,
            });

            entry.category = toCategory;
            entry.categoryName = toLabel;
            entry.id = newCollectionId;
            entry.prefab = prefabPath;
            log.AppendLine($"->{toCategory} {catalogId} {entry.displayName}");
        }

        private static void EnsurePrefabFolder(string category)
        {
            var root = PrefabRoot.Replace('\\', '/');
            var folder = $"{root}/{category}";
            if (AssetDatabase.IsValidFolder(folder))
                return;

            if (!AssetDatabase.IsValidFolder(root))
            {
                var parent = ItemCollectionBuilder.Folder.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                    throw new InvalidOperationException("Missing ItemCollection folder");
                AssetDatabase.CreateFolder(parent, "Prefabs");
            }

            // Disk-only folders (no .meta) block MoveAsset — recreate through AssetDatabase.
            if (Directory.Exists(folder) && !File.Exists(folder + ".meta"))
                Directory.Delete(folder, true);

            AssetDatabase.CreateFolder(root, category);
            AssetDatabase.Refresh();
        }

        private static ItemCatalogSO.Category EnsureCategory(
            ItemCatalogSO catalog, string id, string label, bool enabled)
        {
            var category = catalog.categories.FirstOrDefault(c => c.id == id);
            if (category == null)
            {
                category = new ItemCatalogSO.Category
                {
                    id = id,
                    label = label,
                    enabled = enabled,
                    items = new List<ItemCatalogSO.Item>(),
                };
                catalog.categories.Add(category);
            }
            else
            {
                category.label = label;
                if (id != "reserve")
                    category.enabled = enabled;
                category.items ??= new List<ItemCatalogSO.Item>();
            }

            return category;
        }

        private static ItemCatalogSO.Item FindItem(ItemCatalogSO catalog, string catalogId) =>
            catalog.categories.SelectMany(c => c.items ?? Enumerable.Empty<ItemCatalogSO.Item>())
                .FirstOrDefault(i => i != null && i.id == catalogId);

        private static void RemoveItemEverywhere(ItemCatalogSO catalog, string catalogId)
        {
            foreach (var category in catalog.categories)
                category.items?.RemoveAll(i => i != null && i.id == catalogId);
        }

        private static GameObject LoadCarryable(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<CarryableItem>() == null)
                throw new InvalidOperationException("Missing carryable prefab: " + path);
            return prefab;
        }

        private static string ToCatalogId(string collectionId)
        {
            var id = "i" + collectionId.Substring(collectionId.LastIndexOf('_') + 1);
            if (id.Length > 16)
                throw new InvalidOperationException("Catalog ID too long: " + id);
            return id;
        }

        private static int EnabledCount(ItemCatalogSO.Category category) =>
            category.items.Count(i => i != null && i.enabled);

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
            "reserve" => 8,
            _ => 50,
        };

        private static Vector3 MeasureCharacterSize()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            var visual = player.transform.Find("Visual");
            if (visual == null)
                throw new InvalidOperationException("Player Visual missing");
            var temp = UnityEngine.Object.Instantiate(visual.gameObject);
            try { return BoundsOf(temp).size; }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
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
