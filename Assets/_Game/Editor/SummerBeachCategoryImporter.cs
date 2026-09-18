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
    /// Summer Beach 프리팹에서 휴대 가능한 소품만 골라 beach(여름·해변) 카테고리로 등록한다.
    /// </summary>
    public static class SummerBeachCategoryImporter
    {
        private const string CategoryId = "beach";
        private const string CategoryLabel = "여름·해변";
        private const string Package = "Summer Beach - Low Poly";
        private const string SourceFolder = "Assets/ItemSources/Summer_Beach_Low_Poly/Prefabs";
        private const string CharacterPrefab = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        // 들고 숨기기 적합한 소형·중형만. 캐빈/샤워/그물/대형 파라솔·윈드브레이크 등은 제외.
        private static readonly (string prefab, string displayName)[] Items =
        {
            ("Beachball_color", "비치볼(컬러)"),
            ("Beachball_dot", "비치볼(도트)"),
            ("Beachball_red", "비치볼(빨강)"),
            ("Bucket_blue", "양동이(파랑)"),
            ("Bucket_yellow", "양동이(노랑)"),
            ("BucketCastle_green", "모래성 틀(초록)"),
            ("BucketCastle_red", "모래성 틀(빨강)"),
            ("Cocktail_blue", "칵테일(파랑)"),
            ("Cocktail_red", "칵테일(빨강)"),
            ("Cocktail_white", "칵테일(흰)"),
            ("Coconut", "코코넛"),
            ("Coconut_cocktail", "코코넛 칵테일"),
            ("Flipflop_black", "슬리퍼(검정)"),
            ("Flipflop_pink", "슬리퍼(분홍)"),
            ("Flipflop_purple", "슬리퍼(보라)"),
            ("Flipflop_tropic", "슬리퍼(트로픽)"),
            ("RubberRing_medium", "튜브(중)"),
            ("Shovel_blue", "삽(파랑)"),
            ("Shovel_orange", "삽(주황)"),
            ("Rake_green", "갈퀴(초록)"),
            ("Rake_yellow", "갈퀴(노랑)"),
            ("Sunglasses", "선글라스"),
            ("Sunscreen", "선크림"),
            ("SurfBoard_tinyBlack", "서핑보드(미니 검정)"),
            ("SurfBoard_tinyFluo", "서핑보드(미니 형광)"),
            ("SurfBoard_smallBlue", "서핑보드(소 파랑)"),
            ("SurfBoard_smallTurquoise", "서핑보드(소 청록)"),
            ("Towel_blue", "타월(파랑)"),
            ("Towel_pink", "타월(분홍)"),
            ("Towel_red", "타월(빨강)"),
            ("Towel_sea", "타월(바다)"),
            ("VolleyBall", "배구공"),
            ("Watermelon", "수박"),
            ("Watermelon_quarter", "수박(조각)"),
            ("ParasolSmall", "파라솔(소)"),
        };

        [MenuItem("Tools/Game/Items/Add Summer Beach Category")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            var maxSize = MeasureCharacterSize();
            var prefabDir = $"{ItemCollectionBuilder.Folder}/Prefabs/{CategoryId}";
            Directory.CreateDirectory(prefabDir);

            var manifestPath = ItemCollectionBuilder.Folder + "/ItemCollection.json";
            var manifest = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(manifestPath));
            var items = manifest.items?.ToList() ?? new List<ItemCollectionBuilder.Entry>();
            items.RemoveAll(e => e.category == CategoryId);

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogSetup.Path);
            if (catalog == null)
                throw new InvalidOperationException("Missing ItemCatalog.asset");

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

            var created = new List<(ItemCollectionBuilder.Entry entry, string destPath, string catalogId)>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var (prefabName, displayName) in Items)
                {
                    var sourcePath = $"{SourceFolder}/{prefabName}.prefab";
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    if (source == null)
                        throw new FileNotFoundException(sourcePath);

                    var entryId = "beach_" + StableHash10(prefabName);
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
                            family = prefabName,
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

            // Keep a stable authoring order.
            catalog.categories = catalog.categories
                .OrderBy(c => CategoryOrder(c.id))
                .ToList();

            WriteManifest(manifestPath, items);
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;

            Debug.Log(
                $"[SummerBeachCategoryImporter] added {created.Count} items to '{CategoryLabel}' ({CategoryId}). " +
                $"enabled={category.items.Count(i => i.enabled)}");
        }

        private static Vector3 MeasureCharacterSize()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            var visual = player.transform.Find("Visual");
            if (visual == null)
                throw new InvalidOperationException("Player Visual missing");
            var temp = UnityEngine.Object.Instantiate(visual.gameObject);
            try
            {
                return BoundsOf(temp).size;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
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
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            originalSize = BoundsOf(instance).size;
            scale = Mathf.Min(
                1f,
                maxSize.x / Mathf.Max(0.00001f, originalSize.x),
                maxSize.y / Mathf.Max(0.00001f, originalSize.y),
                maxSize.z / Mathf.Max(0.00001f, originalSize.z));

            var wrapper = new GameObject();
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

            var box = wrapper.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.005f);

            var rigidbody = wrapper.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;

            var carry = wrapper.AddComponent<CarryableItem>();
            var so = new SerializedObject(carry);
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();

            wrapper.name = displayName;
            return wrapper;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var found = false;
            var bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
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

        private static void WriteManifest(string path, List<ItemCollectionBuilder.Entry> items)
        {
            // Preserve existing order groups; append beach entries after fantasy, before reserve.
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
                sb.AppendLine($"      \"source\": {JsonString(e.source)},");
                sb.AppendLine($"      \"package\": {JsonString(e.package)},");
                sb.AppendLine($"      \"category\": {JsonString(e.category)},");
                sb.AppendLine($"      \"categoryName\": {JsonString(e.categoryName)},");
                sb.AppendLine($"      \"displayName\": {JsonString(e.displayName)},");
                sb.AppendLine($"      \"note\": {JsonString(e.note ?? "")},");
                sb.AppendLine($"      \"originalSource\": {JsonString(e.originalSource)},");
                sb.AppendLine($"      \"id\": {JsonString(e.id)},");
                sb.AppendLine($"      \"family\": {JsonString(e.family)}");
                sb.Append(i == ordered.Length - 1 ? "    }\n" : "    },\n");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static int CategoryOrder(string category) => category switch
        {
            "food" => 0,
            "household" => 1,
            "tools" => 2,
            "modern" => 3,
            "fantasy" => 4,
            "beach" => 5,
            "reserve" => 6,
            _ => 50,
        };

        private static string JsonString(string value)
        {
            if (value == null)
                value = "";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
#endif
