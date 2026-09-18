#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Game.Client.Interactions;
using Game.SOAP.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 할로윈 대형 소품·카지노 ATM/금고 추가 + 전체 displayName을 짧은 일반명으로 정리.
    /// </summary>
    public static class ItemCatalogExtrasAndRename
    {
        private const string PrefabRoot = ItemCollectionBuilder.Folder + "/Prefabs";
        private const string CharacterPrefab = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        private static readonly (string sourcePath, string displayName, string package)[] HalloweenExtras =
        {
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Coffin.prefab", "관", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Tombstone_03.prefab", "묘비", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Tombstone_05.prefab", "묘비", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Tombstone_01.prefab", "묘비", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Cross_02.prefab", "십자가", "Low Poly Halloween Props Pack"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Coffin_Old.prefab", "관", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Coffin_New.prefab", "관", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Gravestone_Old_Small_A.prefab", "묘비", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Gravestone_Old_Small_B.prefab", "묘비", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Gravestone_Old_Cross.prefab", "묘비", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Scarecrow_A.prefab", "허수아비", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Scarecrow_B.prefab", "허수아비", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Spiderweb_A.prefab", "거미줄", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Spiderweb_B.prefab", "거미줄", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Spiderweb_C.prefab", "거미줄", "Poly Halloween"),
        };

        private static readonly (string sourcePath, string displayName)[] CasinoExtras =
        {
            ("Assets/ithappy/Casino_Free/Prefabs/Assets/ATM_01.prefab", "ATM"),
            ("Assets/ithappy/Casino_Free/Prefabs/Assets/ATM_03.prefab", "ATM"),
            ("Assets/ithappy/Casino_Free/Prefabs/Assets/SafeBox_01.prefab", "금고"),
            ("Assets/Smoking Pipes set/Prefab/Pipe01.prefab", "파이프"),
            ("Assets/Smoking Pipes set/Prefab/Pipe02.prefab", "파이프"),
        };

        private static string PackageForCasinoSource(string sourcePath) =>
            sourcePath.StartsWith("Assets/Smoking Pipes set/", StringComparison.Ordinal)
                ? "Low-poly smoking pipes set"
                : "Casino FREE - Low Poly 3D Models Pack";

        [MenuItem("Tools/Game/Items/Add Extras And Clean Display Names")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogSetup.Path)
                          ?? throw new InvalidOperationException("Missing ItemCatalog.asset");
            var maxSize = MeasureCharacterSize();
            var manifestPath = ItemCollectionBuilder.Folder + "/ItemCollection.json";
            var entries = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(manifestPath))
                .items.ToList();
            var log = new StringBuilder();

            RepairMissingPrefabs(catalog, entries, maxSize, log);

            var halloween = EnsureCategory(catalog, "halloween", "할로윈", true);
            foreach (var (source, name, package) in HalloweenExtras)
                AddNewItem(catalog, entries, halloween, "halloween", name, source, package, maxSize, log);

            var casino = EnsureCategory(catalog, "casino", "카지노", true);
            foreach (var (source, name) in CasinoExtras)
                AddNewItem(catalog, entries, casino, "casino", name, source,
                    PackageForCasinoSource(source), maxSize, log);

            var renamed = 0;
            foreach (var category in catalog.categories)
            {
                foreach (var item in category.items.Where(i => i != null).ToList())
                {
                    if (item.prefab == null)
                    {
                        category.items.Remove(item);
                        log.AppendLine($"removed-null {category.id} {item.id}");
                        continue;
                    }

                    var cleaned = CleanDisplayName(item.displayName, category.id);
                    if (cleaned == item.displayName) continue;
                    item.displayName = cleaned;
                    renamed++;
                    var carry = item.prefab.GetComponent<CarryableItem>();
                    if (carry != null)
                    {
                        var so = new SerializedObject(carry);
                        so.FindProperty("displayName").stringValue = cleaned;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(item.prefab);
                    }
                }
            }

            foreach (var entry in entries)
                entry.displayName = CleanDisplayName(entry.displayName, entry.category);

            WriteManifest(manifestPath, entries);
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;

            var summary =
                $"[ExtrasAndRename] halloween={EnabledCount(halloween)} casino={EnabledCount(casino)} renamed={renamed}\n{log}";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/ItemCatalogExtrasAndRename.log", summary, Encoding.UTF8);
        }

        private static void RepairMissingPrefabs(
            ItemCatalogSO catalog,
            List<ItemCollectionBuilder.Entry> entries,
            Vector3 maxSize,
            StringBuilder log)
        {
            foreach (var category in catalog.categories)
            {
                foreach (var item in category.items.Where(i => i != null && i.prefab == null).ToList())
                {
                    var suffix = item.id.StartsWith("i") ? item.id.Substring(1) : item.id;
                    var entry = entries.FirstOrDefault(e =>
                        e.id.EndsWith("_" + suffix, StringComparison.Ordinal) ||
                        e.id == category.id + "_" + suffix);
                    if (entry == null || string.IsNullOrEmpty(entry.source) ||
                        AssetDatabase.LoadAssetAtPath<GameObject>(entry.source) == null)
                    {
                        category.items.Remove(item);
                        log.AppendLine($"drop-unrepairable {category.id} {item.id}");
                        continue;
                    }

                    EnsurePrefabFolder(category.id);
                    var destPath = $"{PrefabRoot}/{category.id}/{entry.id}.prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) == null)
                    {
                        var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.source);
                        var wrapper = BuildCarryable(source, item.displayName, maxSize);
                        PrefabUtility.SaveAsPrefabAsset(wrapper, destPath);
                        UnityEngine.Object.DestroyImmediate(wrapper);
                        AssetDatabase.ImportAsset(destPath);
                    }

                    item.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
                    entry.prefab = destPath;
                    entry.category = category.id;
                    if (item.prefab == null)
                    {
                        category.items.Remove(item);
                        log.AppendLine($"drop-rebuild-failed {category.id} {item.id}");
                    }
                    else
                        log.AppendLine($"repaired {category.id} {item.id}");
                }
            }
        }

        public static string CleanDisplayName(string name, string categoryId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "물건";

            var n = name.Trim();

            // 화분·식물: 전부 짧은 일반명
            if (categoryId == "plants" || n.Contains("화분"))
            {
                if (n.Contains("선인장")) return "선인장";
                return "화분";
            }

            n = Regex.Replace(n, @"\([^)]*\)", "").Trim();
            n = Regex.Replace(n, @"\s*/\s*성장\s*\d+", "").Trim();
            n = Regex.Replace(n, @"\s+(Blue|Red|Green|Orange|White|Purple|Brown|Grey|Gray|Black)$", "",
                RegexOptions.IgnoreCase).Trim();
            n = Regex.Replace(n, @"^창고\s+", "").Trim();
            n = Regex.Replace(n, @"\(폴리\)$", "").Trim();

            // 코드형 접미사: 01-001, 02-001
            n = Regex.Replace(n, @"\s+\d{1,3}-\d{1,3}$", "").Trim();
            // 숫자 접미사: 001, 01, 1, 39
            n = Regex.Replace(n, @"\s+\d{1,3}$", "").Trim();
            // 알파벳 단일 접미사: A, B
            n = Regex.Replace(n, @"\s+[A-Za-z]$", "").Trim();

            // 카지노 계열
            if (Regex.IsMatch(n, @"^카드")) return "카드";
            if (Regex.IsMatch(n, @"^슬롯")) return "슬롯 심볼";
            if (Regex.IsMatch(n, @"^골드바")) return "골드바";
            if (n.StartsWith("지폐 상자", StringComparison.Ordinal)) return "지폐 상자";
            if (n.StartsWith("지폐 다발", StringComparison.Ordinal)) return "지폐 다발";
            if (n.StartsWith("동전", StringComparison.Ordinal)) return "동전";
            if (n.StartsWith("지폐", StringComparison.Ordinal)) return "지폐";
            if (n is "금고 문") return "금고 문";

            // 할로윈 중복 표기
            if (n.Contains("롤리팝")) return n.Contains("유령") ? "유령 롤리팝"
                : n.Contains("호박") ? "호박 롤리팝"
                : n.Contains("해골") ? "해골 롤리팝"
                : "롤리팝";
            if (n.Contains("마녀 모자")) return "마녀 모자";
            if (n is "작은 호박" or "얼굴 호박" or "조각 호박" or "사탕 호박통" or "호박 캔디") return "호박";
            if (n is "조각 사탕통") return "사탕통";
            if (n is "사탕 그릇") return "사탕 그릇";
            if (n.StartsWith("사탕", StringComparison.Ordinal) && n != "사탕통" && n != "사탕 그릇") return "사탕";

            // 해변
            if (n.StartsWith("서핑보드", StringComparison.Ordinal)) return "서핑보드";
            if (n.StartsWith("모래성", StringComparison.Ordinal)) return "모래성 틀";

            // 도구 접두
            if (n is "손도끼") return "도끼";
            if (n is "쇠지렛대") return "쇠지렛대";

            n = Regex.Replace(n, @"\s+", " ").Trim();
            return string.IsNullOrEmpty(n) ? "물건" : n;
        }

        private static void AddNewItem(
            ItemCatalogSO catalog,
            List<ItemCollectionBuilder.Entry> entries,
            ItemCatalogSO.Category category,
            string categoryId,
            string displayName,
            string sourcePath,
            string package,
            Vector3 maxSize,
            StringBuilder log)
        {
            if (entries.Any(e => e.source == sourcePath && e.category == categoryId))
                return;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
                throw new FileNotFoundException(sourcePath);

            var family = Path.GetFileNameWithoutExtension(sourcePath);
            var entryId = categoryId + "_" + StableHash10(sourcePath);
            var catalogId = "i" + entryId.Substring(entryId.LastIndexOf('_') + 1);
            if (catalogId.Length > 16)
                throw new InvalidOperationException("Catalog ID too long: " + catalogId);
            if (FindItem(catalog, catalogId) != null)
                return;

            EnsurePrefabFolder(categoryId);
            var destPath = $"{PrefabRoot}/{categoryId}/{entryId}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) == null)
            {
                var wrapper = BuildCarryable(source, displayName, maxSize);
                PrefabUtility.SaveAsPrefabAsset(wrapper, destPath);
                UnityEngine.Object.DestroyImmediate(wrapper);
                AssetDatabase.ImportAsset(destPath);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
            if (prefab == null || prefab.GetComponent<CarryableItem>() == null)
                throw new InvalidOperationException("Missing carryable: " + destPath);

            category.items.Add(new ItemCatalogSO.Item
            {
                id = catalogId,
                displayName = displayName,
                enabled = true,
                prefab = prefab,
            });
            entries.Add(new ItemCollectionBuilder.Entry
            {
                id = entryId,
                source = sourcePath,
                originalSource = sourcePath,
                package = package,
                category = categoryId,
                categoryName = category.label,
                displayName = displayName,
                note = "",
                family = family,
                prefab = destPath,
            });
            log.AppendLine($"+{categoryId} {catalogId} {displayName}");
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
            foreach (var light in instance.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(light);
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

        private static ItemCatalogSO.Item FindItem(ItemCatalogSO catalog, string catalogId) =>
            catalog.categories.SelectMany(c => c.items ?? Enumerable.Empty<ItemCatalogSO.Item>())
                .FirstOrDefault(i => i != null && i.id == catalogId);

        private static void EnsurePrefabFolder(string category)
        {
            var root = PrefabRoot.Replace('\\', '/');
            var folder = $"{root}/{category}";
            if (AssetDatabase.IsValidFolder(folder)) return;
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
            "reserve" => 10,
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
