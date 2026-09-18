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
    /// ithappy Casino Free 프리팹에서 휴대 가능한 소품만 골라 casino(카지노) 카테고리로 등록한다.
    /// </summary>
    public static class CasinoCategoryImporter
    {
        private const string CategoryId = "casino";
        private const string CategoryLabel = "카지노";
        private const string Package = "Casino FREE - Low Poly 3D Models Pack";
        private const string SourceRoot = "Assets/ithappy/Casino_Free/Prefabs";
        private const string CharacterPrefab = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        // 들고 숨기기 적합한 소형·중형만. 슬롯머신/ATM/가구/기둥/바닥 등은 제외.
        private static readonly (string relativePath, string displayName)[] Items =
        {
            ("Assets/Card_39", "카드 39"),
            ("Assets/Card_44", "카드 44"),
            ("Assets/Card_48", "카드 48"),
            ("Assets/Card_53", "카드 53"),
            ("Assets/Cash_01", "지폐 01"),
            ("Assets/Cash_02", "지폐 02"),
            ("Assets/Cash_07", "지폐 다발 07"),
            ("Assets/Cash_10", "지폐 상자 10"),
            ("Assets/Cash_11", "동전 컵 11"),
            ("Assets/Cash_Machine_01", "현금 계수기"),
            ("Assets/Gold_Ingot_01", "골드바 01"),
            ("Assets/Gold_Ingot_02", "골드바 02"),
            ("Assets/Book_01", "장부"),
            ("Assets/Mouse_01", "마우스"),
            ("Assets/Keyboard_01", "키보드"),
            ("Assets/Fruits_01", "과일 접시"),
            ("Assets/Monitor_02", "모니터"),
            ("Assets/SafeBox_Door_01", "금고 문"),
            ("Games/Slot_01", "슬롯 심볼 01"),
            ("Games/Slot_02", "슬롯 심볼 02"),
            ("Games/Slot_03", "슬롯 심볼 03"),
            ("Games/Slot_04", "슬롯 심볼 04"),
            ("Games/Slot_05", "슬롯 심볼 05"),
            ("Games/Slot_06", "슬롯 심볼 06"),
            ("Games/Slot_07", "슬롯 심볼 07"),
            ("Games/Slot_08", "슬롯 심볼 08"),
            ("Games/Slot_Machine_Aquarium_Fish_01", "수족관 물고기"),
        };

        private static readonly (string sourcePath, string displayName, string package)[] ExtraSources =
        {
            ("Assets/Smoking Pipes set/Prefab/Pipe01.prefab", "파이프", "Low-poly smoking pipes set"),
            ("Assets/Smoking Pipes set/Prefab/Pipe02.prefab", "파이프", "Low-poly smoking pipes set"),
        };

        [MenuItem("Tools/Game/Items/Add Casino Smoking Pipes")]
        public static void ImportSmokingPipes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            var maxSize = MeasureCharacterSize();
            EnsurePrefabFolder(CategoryId);
            var prefabDir = $"{ItemCollectionBuilder.Folder}/Prefabs/{CategoryId}";

            var manifestPath = ItemCollectionBuilder.Folder + "/ItemCollection.json";
            var items = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(manifestPath))
                .items?.ToList() ?? new List<ItemCollectionBuilder.Entry>();

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogSetup.Path)
                          ?? throw new InvalidOperationException("Missing ItemCatalog.asset");
            var category = catalog.categories.FirstOrDefault(c => c.id == CategoryId)
                           ?? throw new InvalidOperationException("Missing casino category — run Add Casino Category first.");

            var created = new List<(ItemCollectionBuilder.Entry entry, string destPath, string catalogId)>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var (sourcePath, displayName, package) in ExtraSources)
                {
                    if (items.Any(e => e.source == sourcePath && e.category == CategoryId))
                        continue;

                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    if (source == null)
                        throw new FileNotFoundException(sourcePath);

                    var family = Path.GetFileNameWithoutExtension(sourcePath);
                    var entryId = "casino_" + StableHash10(sourcePath);
                    var catalogId = "i" + entryId.Substring(entryId.LastIndexOf('_') + 1);
                    if (catalogId.Length > 16)
                        throw new InvalidOperationException("Catalog ID too long: " + catalogId);
                    if (category.items.Any(i => i != null && i.id == catalogId))
                        continue;

                    var destPath = $"{prefabDir}/{entryId}.prefab";
                    var wrapper = BuildCarryable(source, displayName, maxSize, out var originalSize, out var finalSize, out var scale);
                    PrefabUtility.SaveAsPrefabAsset(wrapper, destPath);
                    UnityEngine.Object.DestroyImmediate(wrapper);

                    created.Add((
                        new ItemCollectionBuilder.Entry
                        {
                            id = entryId,
                            source = sourcePath,
                            originalSource = sourcePath,
                            package = package,
                            category = CategoryId,
                            categoryName = CategoryLabel,
                            displayName = displayName,
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

                category.items.Add(new ItemCatalogSO.Item
                {
                    id = catalogId,
                    displayName = entry.displayName,
                    enabled = true,
                    prefab = prefab,
                });
                items.Add(entry);
            }

            if (created.Count > 0)
            {
                WriteManifest(manifestPath, items);
                catalog.Apply();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Selection.activeObject = catalog;
            var summary =
                $"[CasinoCategoryImporter] smoking pipes added={created.Count}, casino enabled={category.items.Count(i => i.enabled)}";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/CasinoSmokingPipesImporter.log", summary);
        }

        [MenuItem("Tools/Game/Items/Add Casino Category")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            var maxSize = MeasureCharacterSize();
            var prefabDir = $"{ItemCollectionBuilder.Folder}/Prefabs/{CategoryId}";
            EnsurePrefabFolder(CategoryId);

            var manifestPath = ItemCollectionBuilder.Folder + "/ItemCollection.json";
            var manifest = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(manifestPath));
            var items = manifest.items?.ToList() ?? new List<ItemCollectionBuilder.Entry>();
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

            // Remove previous casino collection prefabs.
            foreach (var old in AssetDatabase.FindAssets("t:Prefab", new[] { prefabDir })
                         .Select(AssetDatabase.GUIDToAssetPath).ToArray())
                AssetDatabase.DeleteAsset(old);

            var created = new List<(ItemCollectionBuilder.Entry entry, string destPath, string catalogId)>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var (relativePath, displayName) in Items)
                {
                    var sourcePath = $"{SourceRoot}/{relativePath}.prefab";
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    if (source == null)
                        throw new FileNotFoundException(sourcePath);

                    var family = Path.GetFileName(relativePath);
                    var entryId = "casino_" + StableHash10(family);
                    var catalogId = "i" + entryId.Substring(entryId.LastIndexOf('_') + 1);
                    var destPath = $"{prefabDir}/{entryId}.prefab";

                    var wrapper = BuildCarryable(source, displayName, maxSize, out var originalSize, out var finalSize, out var scale);
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
                            displayName = displayName,
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
                    displayName = entry.displayName,
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
                $"[CasinoCategoryImporter] added {created.Count} items to '{CategoryLabel}' ({CategoryId}). " +
                $"enabled={category.items.Count(i => i.enabled)}";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/CasinoCategoryImporter.log", summary);
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
            "reserve" => 9,
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
