using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Items
{
    /// <summary>
    /// Player-item candidates already placed as carryable props in Playground.
    /// Add another surface-resting prop here when it becomes an assignment candidate.
    /// </summary>
    public static class ItemCatalog
    {
        private const string AssignedPrefix = "Assigned_";
        private static readonly ItemDefinition[] DefinitionValues =
        {
            new("Soda_01", "food", "탄산음료"),
            new("Burger_01", "food", "햄버거"),
            new("Pineapple_01", "food", "파인애플"),
            new("Cup1_C3", "tableware", "컵"),
            new("Plate1_C1", "tableware", "접시"),
            new("Plant_01", "decoration", "화분"),
            new("Kettle1_C1", "kitchen", "주전자"),
            new("Toaster_03", "kitchen", "토스터")
        };
        public static IReadOnlyList<ItemDefinition> Definitions { get; } =
            Array.AsReadOnly(DefinitionValues);
        public static IReadOnlyList<string> Categories { get; } =
            Array.AsReadOnly(DefinitionValues
                .Select(definition => definition.Category)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        public static IReadOnlyList<ItemDefinition> DefinitionsInCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Array.Empty<ItemDefinition>();
            }

            var normalizedCategory = category.Trim();
            return Array.AsReadOnly(DefinitionValues
                .Where(definition => string.Equals(
                    definition.Category,
                    normalizedCategory,
                    StringComparison.Ordinal))
                .ToArray());
        }

        public static string DisplayNameOf(string itemId)
        {
            if (TryGetAssignedDefinition(itemId, out var assigned))
            {
                return assigned.DisplayName;
            }

            for (var index = 0; index < DefinitionValues.Length; index++)
            {
                if (string.Equals(
                        DefinitionValues[index].ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return DefinitionValues[index].DisplayName;
                }
            }

            return itemId?.Trim() ?? string.Empty;
        }

        public static string AssignedObjectId(int definitionIndex)
        {
            if (definitionIndex < 0 || definitionIndex >= DefinitionValues.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionIndex));
            }

            return $"{AssignedPrefix}{definitionIndex}";
        }

        public static ItemDefinition AssignedDefinition(int definitionIndex)
        {
            var source = DefinitionValues[definitionIndex];
            return new ItemDefinition(
                AssignedObjectId(definitionIndex),
                source.Category,
                source.DisplayName);
        }

        private static bool TryGetAssignedDefinition(
            string itemId,
            out ItemDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(itemId) &&
                itemId.StartsWith(AssignedPrefix, StringComparison.Ordinal) &&
                int.TryParse(itemId.AsSpan(AssignedPrefix.Length), out var index) &&
                index >= 0 && index < DefinitionValues.Length)
            {
                definition = DefinitionValues[index];
                return true;
            }

            definition = default;
            return false;
        }
    }
}
