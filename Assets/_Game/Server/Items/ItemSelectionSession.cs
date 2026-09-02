using System;
using System.Collections.Generic;
using Game.Core.Items;

namespace Game.Server.Items
{
    public sealed class ItemSelectionSession
    {
        private readonly ItemDefinition[] definitions;
        private readonly string[] categories;
        private readonly ItemDefinition?[] selections;
        private readonly HashSet<string> selectedItemIds = new(StringComparer.Ordinal);

        public ItemSelectionSession(
            IReadOnlyList<ItemDefinition> definitions,
            IReadOnlyList<string> categories)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (categories == null) throw new ArgumentNullException(nameof(categories));
            if (categories.Count == 0)
                throw new ArgumentException("At least one player category is required.", nameof(categories));

            this.definitions = new ItemDefinition[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
                this.definitions[index] = definitions[index];

            this.categories = new string[categories.Count];
            selections = new ItemDefinition?[categories.Count];
            for (var playerIndex = 0; playerIndex < categories.Count; playerIndex++)
            {
                var category = categories[playerIndex];
                if (string.IsNullOrWhiteSpace(category) ||
                    ItemSelectionRules.AvailableItems(
                        this.definitions,
                        category,
                        Array.Empty<string>()).Count == 0)
                {
                    throw new ArgumentException(
                        $"Player {playerIndex} requires a known category.",
                        nameof(categories));
                }

                this.categories[playerIndex] = category.Trim();
            }
        }

        public int ConfirmedCount { get; private set; }
        public bool IsComplete => ConfirmedCount == selections.Length;

        public IReadOnlyList<ItemDefinition> AvailableItemsFor(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            var unavailable = new HashSet<string>(selectedItemIds, StringComparer.Ordinal);
            if (selections[playerIndex].HasValue)
                unavailable.Remove(selections[playerIndex].Value.ItemId);
            return ItemSelectionRules.AvailableItems(
                definitions,
                categories[playerIndex],
                unavailable);
        }

        public bool TryConfirm(
            int playerIndex,
            string itemId,
            out ItemSelectionFailure failure)
        {
            ValidatePlayerIndex(playerIndex);
            var previous = selections[playerIndex];
            if (previous.HasValue) selectedItemIds.Remove(previous.Value.ItemId);

            if (!ItemSelectionRules.TryResolveSelection(
                    definitions,
                    categories[playerIndex],
                    itemId,
                    selectedItemIds,
                    out var selected,
                    out failure))
            {
                if (previous.HasValue) selectedItemIds.Add(previous.Value.ItemId);
                return false;
            }

            selections[playerIndex] = selected;
            selectedItemIds.Add(selected.ItemId);
            if (!previous.HasValue) ConfirmedCount++;
            return true;
        }

        public bool TryCreateAssignments(out PlayerItemAssignment[] assignments)
        {
            assignments = null;
            if (!IsComplete) return false;

            assignments = new PlayerItemAssignment[selections.Length];
            for (var playerIndex = 0; playerIndex < selections.Length; playerIndex++)
                assignments[playerIndex] = new PlayerItemAssignment(
                    playerIndex,
                    selections[playerIndex].Value);
            return true;
        }

        private void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= selections.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
        }
    }
}
