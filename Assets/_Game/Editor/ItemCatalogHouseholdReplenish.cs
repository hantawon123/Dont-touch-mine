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
    /// household 분리 후 줄어든 수량을 JSON에만 있던 미빌드 아이템으로 충원.
    /// 욕실·화분 성격은 bathroom/plants로, 나머지는 household로 등록.
    /// </summary>
    public static class ItemCatalogHouseholdReplenish
    {
        private const string PrefabRoot = ItemCollectionBuilder.Folder + "/Prefabs";
        private const string CharacterPrefab = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        private static readonly HashSet<string> BathroomFamilies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Adhesive Tape", "AdhesiveBandage", "Bobbypin", "Clothes pin", "Cotton Swab",
            "Soapbox", "Tweezers", "Toothbrush", "Plunger",
        };

        private static readonly HashSet<string> PlantFamilies = new(StringComparer.OrdinalIgnoreCase)
        {
            "CS_Plant_Pot_01_L1", "CS_Plant_Pot_25_L1", "Pot4", "Pot7",
        };

        [MenuItem("Tools/Game/Items/Replenish Household From Missing Prefabs")]
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

            var household = EnsureCategory(catalog, "household", "생활용품", true);
            var bathroom = EnsureCategory(catalog, "bathroom", "욕실·위생", true);
            var plants = EnsureCategory(catalog, "plants", "화분·식물", true);

            var missing = entries
                .Where(e => e.category == "household")
                .Where(e => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/household/{e.id}.prefab") == null)
                .Where(e => AssetDatabase.LoadAssetAtPath<GameObject>(e.source) != null)
                .ToList();

            foreach (var entry in missing)
            {
                var (destCategory, destLabel, destCat) = Route(entry, household, bathroom, plants);
                BuildPrefab(entry, destCategory, maxSize, log);
                AddOrMoveToCatalog(catalog, entries, entry, destCategory, destLabel, destCat, log);
            }

            catalog.categories = catalog.categories.OrderBy(CategoryOrder).ToList();
            WriteManifest(manifestPath, entries);
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;

            var summary =
                $"[HouseholdReplenish] built={missing.Count} " +
                $"household={EnabledCount(household)} bathroom={EnabledCount(bathroom)} plants={EnabledCount(plants)}\n{log}";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/ItemCatalogHouseholdReplenish.log", summary);
        }

        private static (string category, string label, ItemCatalogSO.Category cat) Route(
            ItemCollectionBuilder.Entry entry,
            ItemCatalogSO.Category household,
            ItemCatalogSO.Category bathroom,
            ItemCatalogSO.Category plants)
        {
            if (PlantFamilies.Contains(entry.family) ||
                entry.family.StartsWith("CS_Plant", StringComparison.OrdinalIgnoreCase) ||
                entry.displayName.Contains("화분"))
                return ("plants", "화분·식물", plants);

            if (BathroomFamilies.Contains(entry.family) ||
                entry.package.IndexOf("Bathroom", StringComparison.OrdinalIgnoreCase) >= 0)
                return ("bathroom", "욕실·위생", bathroom);

            return ("household", "생활용품", household);
        }

        private static void BuildPrefab(
            ItemCollectionBuilder.Entry entry,
            string destCategory,
            Vector3 maxSize,
            StringBuilder log)
        {
            var suffix = entry.id.Substring(entry.id.LastIndexOf('_') + 1);
            var newId = destCategory + "_" + suffix;
            var destPath = $"{PrefabRoot}/{destCategory}/{newId}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
            {
                entry.category = destCategory;
                entry.id = newId;
                entry.prefab = destPath;
                return;
            }

            var source = LoadSourcePrefab(entry.source);
            var wrapper = BuildCarryable(source, entry.displayName, maxSize);
            EnsurePrefabFolder(destCategory);
            PrefabUtility.SaveAsPrefabAsset(wrapper, destPath);
            UnityEngine.Object.DestroyImmediate(wrapper);
            AssetDatabase.ImportAsset(destPath);

            entry.category = destCategory;
            entry.categoryName = destCategory switch
            {
                "bathroom" => "욕실·위생",
                "plants" => "화분·식물",
                _ => "생활용품",
            };
            entry.id = newId;
            entry.prefab = destPath;
            log.AppendLine($"build {newId} {entry.displayName}");
        }

        private static void AddOrMoveToCatalog(
            ItemCatalogSO catalog,
            List<ItemCollectionBuilder.Entry> entries,
            ItemCollectionBuilder.Entry entry,
            string destCategory,
            string destLabel,
            ItemCatalogSO.Category target,
            StringBuilder log)
        {
            entry.category = destCategory;
            entry.categoryName = destLabel;
            var catalogId = "i" + entry.id.Substring(entry.id.LastIndexOf('_') + 1);
            if (catalogId.Length > 16)
                throw new InvalidOperationException("Catalog ID too long: " + catalogId);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
            if (prefab == null || prefab.GetComponent<CarryableItem>() == null)
                throw new InvalidOperationException("Missing carryable: " + entry.prefab);

            foreach (var category in catalog.categories)
                category.items?.RemoveAll(i => i != null && i.id == catalogId);

            target.items.Add(new ItemCatalogSO.Item
            {
                id = catalogId,
                displayName = entry.displayName,
                enabled = true,
                prefab = prefab,
            });
            log.AppendLine($"+{destCategory} {catalogId} {entry.displayName}");
        }

        private static GameObject LoadSourcePrefab(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new FileNotFoundException("Missing source", path);
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
                category.enabled = enabled;
                category.items ??= new List<ItemCatalogSO.Item>();
            }

            return category;
        }

        private static void EnsurePrefabFolder(string category)
        {
            var root = PrefabRoot.Replace('\\', '/');
            var folder = $"{root}/{category}";
            if (AssetDatabase.IsValidFolder(folder))
                return;
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder(ItemCollectionBuilder.Folder.Replace('\\', '/'), "Prefabs");
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

        private static int EnabledCount(ItemCatalogSO.Category category) =>
            category.items.Count(i => i != null && i.enabled);

        private static int CategoryOrder(ItemCatalogSO.Category c) => c.id switch
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

        private static void WriteManifest(string path, List<ItemCollectionBuilder.Entry> items)
        {
            var ordered = items
                .OrderBy(e => CategoryOrder(new ItemCatalogSO.Category { id = e.category }))
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
