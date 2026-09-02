using System;
using System.Collections.Generic;
using System.Linq;

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

    public enum ItemSelectionFailure
    {
        None = 0,
        MissingCategory,
        UnknownItem,
        WrongCategory,
        AlreadySelected,
        AlreadyConfirmed,
        SelectionClosed
    }

    public static class ItemSelectionRules
    {
        public static IReadOnlyList<ItemDefinition> AvailableItems(
            IReadOnlyList<ItemDefinition> definitions,
            string category,
            IReadOnlyCollection<string> selectedItemIds)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (selectedItemIds == null) throw new ArgumentNullException(nameof(selectedItemIds));
            if (string.IsNullOrWhiteSpace(category)) return Array.Empty<ItemDefinition>();

            var normalizedCategory = category.Trim();
            return Array.AsReadOnly(definitions
                .Where(definition =>
                    string.Equals(definition.Category, normalizedCategory, StringComparison.Ordinal) &&
                    !selectedItemIds.Contains(definition.ItemId))
                .ToArray());
        }

        public static bool TryResolveSelection(
            IReadOnlyList<ItemDefinition> definitions,
            string category,
            string itemId,
            IReadOnlyCollection<string> selectedItemIds,
            out ItemDefinition selected,
            out ItemSelectionFailure failure)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (selectedItemIds == null) throw new ArgumentNullException(nameof(selectedItemIds));

            selected = default;
            if (string.IsNullOrWhiteSpace(category))
            {
                failure = ItemSelectionFailure.MissingCategory;
                return false;
            }

            var normalizedCategory = category.Trim();
            var normalizedItemId = itemId?.Trim();
            foreach (var definition in definitions)
            {
                if (!string.Equals(definition.ItemId, normalizedItemId, StringComparison.Ordinal)) continue;
                if (!string.Equals(definition.Category, normalizedCategory, StringComparison.Ordinal))
                {
                    failure = ItemSelectionFailure.WrongCategory;
                    return false;
                }

                if (selectedItemIds.Contains(definition.ItemId))
                {
                    failure = ItemSelectionFailure.AlreadySelected;
                    return false;
                }

                selected = definition;
                failure = ItemSelectionFailure.None;
                return true;
            }

            failure = ItemSelectionFailure.UnknownItem;
            return false;
        }
    }

    public static class ItemAssignmentSystem
    {
        public static PlayerItemAssignment[] Assign(int playerCount, Random random)
        {
            return Assign(ItemCatalog.Definitions, playerCount, random);
        }

        public static PlayerItemAssignment[] Assign(
            IReadOnlyList<ItemDefinition> definitions,
            int playerCount,
            Random random)
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

            if (itemIds.Count < playerCount)
            {
                throw new InvalidOperationException(
                    $"At least {playerCount} unique items are required.");
            }

            var assignments = new PlayerItemAssignment[playerCount];
            for (var playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                var categoryIndex = random.Next(categories.Count);
                var category = categories[categoryIndex];
                var categoryItems = itemsByCategory[category];
                var itemIndex = random.Next(categoryItems.Count);
                var item = categoryItems[itemIndex];

                assignments[playerIndex] = new PlayerItemAssignment(playerIndex, item);
                categoryItems.RemoveAt(itemIndex);

                if (categoryItems.Count == 0)
                {
                    categories.RemoveAt(categoryIndex);
                }
            }

            return assignments;
        }
    }
}
