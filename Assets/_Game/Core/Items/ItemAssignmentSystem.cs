using System;
using System.Collections.Generic;

namespace Game.Core.Items
{
    public readonly struct ItemDefinition
    {
        public ItemDefinition(string itemId, string category, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id is required.", nameof(itemId));
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Category is required.", nameof(category));
            }

            ItemId = itemId.Trim();
            Category = category.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? ItemId
                : displayName.Trim();
        }

        public string ItemId { get; }
        public string Category { get; }
        public string DisplayName { get; }
    }

    public readonly struct PlayerItemAssignment
    {
        public PlayerItemAssignment(int playerIndex, ItemDefinition item)
        {
            PlayerIndex = playerIndex;
            Item = item;
        }

        public int PlayerIndex { get; }
        public ItemDefinition Item { get; }
    }

    public static class ItemAssignmentSystem
    {
        public static PlayerItemAssignment[] Assign(int playerCount, Random random)
        {
            return Assign(ItemCatalog.AssignmentDefinitions, playerCount, random);
        }

        public static PlayerItemAssignment[] Assign(
            IReadOnlyList<ItemDefinition> definitions,
            int playerCount,
            Random random)
        {
            return Assign(definitions, playerCount, random, null);
        }

        public static PlayerItemAssignment[] Assign(
            IReadOnlyList<ItemDefinition> definitions,
            int playerCount,
            Random random,
            string categoryId)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (playerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var itemsByCategory = new Dictionary<string, List<ItemDefinition>>(
                StringComparer.Ordinal);
            var categories = new List<string>();

            foreach (var definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.ItemId) ||
                    string.IsNullOrWhiteSpace(definition.Category))
                {
                    throw new ArgumentException(
                        "Every item requires an id and category.",
                        nameof(definitions));
                }

                if (!itemIds.Add(definition.ItemId))
                {
                    throw new ArgumentException(
                        $"Duplicate item id: {definition.ItemId}",
                        nameof(definitions));
                }

                if (!itemsByCategory.TryGetValue(definition.Category, out var categoryItems))
                {
                    categoryItems = new List<ItemDefinition>();
                    itemsByCategory.Add(definition.Category, categoryItems);
                    categories.Add(definition.Category);
                }

                categoryItems.Add(definition);
            }

            for (var categoryIndex = categories.Count - 1; categoryIndex >= 0; categoryIndex--)
            {
                var candidateCategory = categories[categoryIndex];
                if (itemsByCategory[candidateCategory].Count < playerCount ||
                    (!string.IsNullOrWhiteSpace(categoryId) &&
                     !string.Equals(candidateCategory, categoryId.Trim(), StringComparison.Ordinal)))
                {
                    categories.RemoveAt(categoryIndex);
                }
            }

            if (categories.Count == 0)
            {
                throw new InvalidOperationException(
                    $"A category with at least {playerCount} unique items is required.");
            }

            var category = categories[random.Next(categories.Count)];
            var candidates = itemsByCategory[category];
            var assignments = new PlayerItemAssignment[playerCount];
            for (var playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                var itemIndex = random.Next(candidates.Count);
                var item = candidates[itemIndex];

                assignments[playerIndex] = new PlayerItemAssignment(playerIndex, item);
                candidates.RemoveAt(itemIndex);
            }

            return assignments;
        }
    }
}
