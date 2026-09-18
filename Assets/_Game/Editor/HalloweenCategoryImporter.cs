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
    /// DestiaArt / Polyperfect 할로윈 프리팹에서 휴대 가능한 소품만 골라 halloween 카테고리로 등록한다.
    /// </summary>
    public static class HalloweenCategoryImporter
    {
        private const string CategoryId = "halloween";
        private const string CategoryLabel = "할로윈";
        private const string CharacterPrefab = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        // 들고 숨기기 적합한 소형·중형만. 묘비/관/허수아비/거미줄/깃발줄 등은 제외.
        private static readonly (string sourcePath, string displayName, string package)[] Items =
        {
            // DestiaArt — candy / party
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Balloon_Single.prefab", "풍선", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Bat.prefab", "박쥐", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_1.prefab", "사탕 1", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_4.prefab", "사탕 4", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_Bowl.prefab", "사탕 그릇", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_Cane_1.prefab", "캔디케인", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_Corn_1.prefab", "캔디콘", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_Eye.prefab", "눈알 사탕", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_Holder_V1.prefab", "사탕통", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_Holder_V1_Carved_1.prefab", "조각 사탕통", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Candy_Worm_1.prefab", "지렁이 사탕", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Chocolate_1.prefab", "초콜릿", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Cone_Hat.prefab", "고깔모자", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Lollypop_Ghost.prefab", "유령 롤리팝", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Lollypop_Pumpkin.prefab", "호박 롤리팝", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Lollypop_Skull.prefab", "해골 롤리팝", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Candies and party/SM_Lollypop_Round_1.prefab", "원형 롤리팝", "Low Poly Halloween Props Pack"),
            // DestiaArt — pumpkins / witch / graveyard small
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Pumpkins/SM_Pumpkin_1.prefab", "호박", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Pumpkins/SM_Pumpkin_3.prefab", "작은 호박", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Pumpkins/SM_Pumpkin_Carved_1.prefab", "조각 호박", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Pumpkins/SM_Pumpkin_Face_1.prefab", "얼굴 호박", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Witch/SM_Broom.prefab", "빗자루", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Witch/SM_Candle_1.prefab", "양초", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Witch/SM_Candlebra.prefab", "촛대", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Witch/SM_Ladle.prefab", "국자", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Witch/SM_Wand_1.prefab", "지팡이", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Witch/SM_Witch_Hat_1.prefab", "마녀 모자", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Bone.prefab", "뼈", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Hand_1.prefab", "손", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Lantern_1.prefab", "랜턴", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Skull.prefab", "해골", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Scythe.prefab", "낫", "Low Poly Halloween Props Pack"),
            ("Assets/DestiaArt/LowPolyHalloweenPropsPack/Prefabs/Graveyard/SM_Urn_01.prefab", "항아리", "Low Poly Halloween Props Pack"),
            // Polyperfect
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Book_Evil.prefab", "악마의 책", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Book_Magical_Eye.prefab", "마법 눈 책", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Candle_Skull_Unlit.prefab", "해골 양초", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Candy_Pumpkin.prefab", "호박 캔디", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Candy_Stick.prefab", "캔디스틱", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Cauldron_Potion_Empty.prefab", "가마솥", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Hand_Severed.prefab", "잘린 손", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Lollipop_Flat_A.prefab", "납작 롤리팝", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Mushroom_Magical_Amanita_Baby.prefab", "마법 버섯", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Pumpkin_Candy.prefab", "사탕 호박통", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Pumpkin_Carved_A.prefab", "조각 호박 A", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Shovel_Gravedigger.prefab", "무덤지기 삽", "Poly Halloween"),
            ("Assets/polyperfect/Poly Halloween/Prefabs/Halloween/Witch_Hat.prefab", "마녀 모자(폴리)", "Poly Halloween"),
        };

        [MenuItem("Tools/Game/Items/Add Halloween Category")]
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

            foreach (var old in AssetDatabase.FindAssets("t:Prefab", new[] { prefabDir })
                         .Select(AssetDatabase.GUIDToAssetPath).ToArray())
                AssetDatabase.DeleteAsset(old);

            var created = new List<(ItemCollectionBuilder.Entry entry, string destPath, string catalogId)>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var (sourcePath, displayName, package) in Items)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    if (source == null)
                        throw new FileNotFoundException(sourcePath);

                    var family = Path.GetFileNameWithoutExtension(sourcePath);
                    var entryId = "halloween_" + StableHash10(sourcePath);
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
                $"[HalloweenCategoryImporter] added {created.Count} items to '{CategoryLabel}' ({CategoryId}). " +
                $"enabled={category.items.Count(i => i.enabled)}";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/HalloweenCategoryImporter.log", summary);
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
            foreach (var light in instance.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(light);
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
