using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Items;
using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(menuName = "Game/Items/Item Catalog")]
    public sealed class ItemCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "Items/ItemCatalog";
        [Serializable] public sealed class Category
        {
            public string id;
            public string label;
            public bool enabled = true;
            public List<Item> items = new();
        }
        [Serializable] public sealed class Item
        {
            [Tooltip("Stable ASCII network ID, at most 16 characters. Do not change published IDs.")]
            public string id;
            public string displayName;
            public bool enabled = true;
            public GameObject prefab;
        }
        public List<Category> categories = new();

        public static ItemCatalogSO Load()
        {
            var catalog = Resources.Load<ItemCatalogSO>(ResourcePath);
            if (catalog == null) throw new InvalidOperationException("Missing Resources/Items/ItemCatalog asset.");
            catalog.Apply();
            return catalog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => Load();

        public void Apply()
        {
            Validate();
            ItemCatalog.Configure(categories.Where(c => c.enabled)
                .SelectMany(c => c.items.Where(i => i.enabled)
                    .Select(i => new ItemDefinition(i.id, c.id, i.displayName))));
        }

        public GameObject PrefabOf(string itemId)
        {
            var id = itemId?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                var items = categories[categoryIndex]?.items;
                if (items == null)
                {
                    continue;
                }

                for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    var item = items[itemIndex];
                    if (item != null && string.Equals(item.id, id, StringComparison.Ordinal))
                    {
                        return item.prefab;
                    }
                }
            }

            return null;
        }

        public void Validate()
        {
            var categoryIds = new HashSet<string>(StringComparer.Ordinal);
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var category in categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.id) || category.id != category.id.Trim() ||
                    string.IsNullOrWhiteSpace(category.label) || !categoryIds.Add(category.id))
                    throw new InvalidOperationException("Categories require a unique ID and label.");
                foreach (var item in category.items)
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || item.id.Length > 16 ||
                        item.id.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_')) ||
                        !itemIds.Add(item.id) || string.IsNullOrWhiteSpace(item.displayName) || item.prefab == null)
                        throw new InvalidOperationException($"Invalid item in {category.label}: unique ASCII ID (1–16), name and prefab required.");
                if (category.enabled && category.items.Count(i => i.enabled) < MatchRulesSO.MaxPlayerCount)
                    throw new InvalidOperationException($"{category.label} needs {MatchRulesSO.MaxPlayerCount} enabled items before enabling play.");
            }
        }
    }
}
