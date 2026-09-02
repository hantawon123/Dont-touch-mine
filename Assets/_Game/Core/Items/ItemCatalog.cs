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
        // 더미 카탈로그가 최대 6명 배정을 지원하도록 기존 씬 물건을 반복 사용한다.
        private static readonly int[] AssignmentSourceIndices =
        {
            0, 1, 2, 0, 1, 2,
            3, 4, 3, 4, 3, 4,
            5, 5, 5, 5, 5, 5,
            6, 7, 6, 7, 6, 7
        };
        private static readonly ItemDefinition[] AssignmentDefinitionValues =
            AssignmentSourceIndices
                .Select((sourceIndex, assignmentIndex) =>
                {
                    var source = DefinitionValues[sourceIndex];
                    return new ItemDefinition(
                        AssignedObjectId(assignmentIndex),
                        source.Category,
                        source.DisplayName);
                })
                .ToArray();

        public static IReadOnlyList<ItemDefinition> Definitions { get; } =
            Array.AsReadOnly(DefinitionValues);
        public static IReadOnlyList<ItemDefinition> AssignmentDefinitions { get; } =
            Array.AsReadOnly(AssignmentDefinitionValues);
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

        public static string AssignedObjectId(int assignmentIndex)
        {
            if (assignmentIndex < 0 || assignmentIndex >= AssignmentSourceIndices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(assignmentIndex));
            }

            return $"{AssignedPrefix}{assignmentIndex}";
        }

        public static ItemDefinition AssignedDefinition(int assignmentIndex)
        {
            if (assignmentIndex < 0 || assignmentIndex >= AssignmentDefinitionValues.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(assignmentIndex));
            }

            return AssignmentDefinitionValues[assignmentIndex];
        }

        public static ItemDefinition AssignedSourceDefinition(int assignmentIndex)
        {
            if (assignmentIndex < 0 || assignmentIndex >= AssignmentSourceIndices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(assignmentIndex));
            }

            return DefinitionValues[AssignmentSourceIndices[assignmentIndex]];
        }

        private static bool TryGetAssignedDefinition(
            string itemId,
            out ItemDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(itemId) &&
                itemId.StartsWith(AssignedPrefix, StringComparison.Ordinal) &&
                int.TryParse(itemId.AsSpan(AssignedPrefix.Length), out var index) &&
                index >= 0 && index < AssignmentDefinitionValues.Length)
            {
                definition = AssignmentDefinitionValues[index];
                return true;
            }

            definition = default;
            return false;
        }
    }
}
