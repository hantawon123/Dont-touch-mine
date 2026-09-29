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
    public static class ItemCatalogSetup
    {
        public const string Path = "Assets/_Game/Content/Resources/Items/ItemCatalog.asset";
        private const string PrefabRoot = ItemCollectionBuilder.Folder + "/Prefabs";

        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.update += CheckRequest;
        private static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists("Temp/ImportItemCatalog.request")) return;
            File.Delete("Temp/ImportItemCatalog.request");
            try { Import(); File.WriteAllText("Temp/ImportItemCatalog.done", "PASS"); }
            catch (Exception e) { File.WriteAllText("Temp/ImportItemCatalog.error", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/Game/Items/Import New Collection Items Into Catalog")]
        public static void Import()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(Path);
            if (catalog == null)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
                AssetDatabase.CreateAsset(catalog, Path);
            }
            var manifest = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(ItemCollectionBuilder.Folder + "/ItemCollection.json"));
            foreach (var entry in manifest.items)
            {
                var category = catalog.categories.FirstOrDefault(c => c.id == entry.category);
                if (category == null)
                {
                    category = new ItemCatalogSO.Category { id = entry.category, label = entry.categoryName, enabled = entry.category != "reserve" };
                    catalog.categories.Add(category);
                }
                var id = "i" + entry.id.Substring(entry.id.LastIndexOf('_') + 1);
                if (catalog.categories.Any(c => c.items.Any(i => i.id == id))) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemCollectionBuilder.Folder}/Prefabs/{entry.category}/{entry.id}.prefab");
                if (prefab == null || prefab.GetComponent<CarryableItem>() == null) throw new InvalidOperationException("Missing carryable prefab: " + entry.id);
                category.items.Add(new ItemCatalogSO.Item { id = id, displayName = entry.displayName, prefab = prefab });
            }
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
        }

        [MenuItem("Tools/Game/Items/Purge Missing Prefabs And Sync Names")]
        public static void PurgeMissingPrefabsAndSyncNames()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(Path)
                          ?? throw new InvalidOperationException("Missing ItemCatalog.asset");
            var manifestPath = ItemCollectionBuilder.Folder + "/ItemCollection.json";
            var entries = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(manifestPath))
                .items?.ToList() ?? new List<ItemCollectionBuilder.Entry>();
            var log = new StringBuilder();

            var removedCatalog = 0;
            foreach (var category in catalog.categories)
            {
                foreach (var item in category.items.Where(i => i != null).ToList())
                {
                    if (item.prefab != null)
                        continue;
                    category.items.Remove(item);
                    removedCatalog++;
                    log.AppendLine($"catalog-drop {category.id} {item.id} {item.displayName}");
                }
            }

            var before = entries.Count;
            entries = entries.Where(e =>
            {
                var path = $"{PrefabRoot}/{e.category}/{e.id}.prefab";
                var ok = !string.IsNullOrEmpty(e.id) && File.Exists(path) &&
                         AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
                if (!ok)
                    log.AppendLine($"manifest-drop {e.category} {e.id}");
                return ok;
            }).ToList();

            var synced = 0;
            foreach (var category in catalog.categories)
            {
                foreach (var item in category.items.Where(i => i?.prefab != null))
                {
                    var carry = item.prefab.GetComponent<CarryableItem>();
                    if (carry == null) continue;
                    var so = new SerializedObject(carry);
                    var prop = so.FindProperty("displayName");
                    if (prop == null) continue;
                    if (prop.stringValue == item.displayName) continue;
                    prop.stringValue = item.displayName ?? "물건";
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item.prefab);
                    synced++;
                }
            }

            WriteManifest(manifestPath, entries);
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary =
                $"[PurgeMissing] catalogDropped={removedCatalog} manifestDropped={before - entries.Count} " +
                $"namesSynced={synced}\n{log}";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/ItemCatalogPurgeMissing.log", summary, Encoding.UTF8);
            Selection.activeObject = catalog;
        }

        private static void WriteManifest(string path, List<ItemCollectionBuilder.Entry> items)
        {
            var ordered = items
                .OrderBy(e => e.category, StringComparer.Ordinal)
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
