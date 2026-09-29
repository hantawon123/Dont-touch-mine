using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Items
{
    // Runtime snapshot populated from the authored item catalog.
    public static class ItemCatalog
    {
        public static IReadOnlyList<ItemDefinition> Definitions { get; private set; } = Array.Empty<ItemDefinition>();
        public static IReadOnlyList<ItemDefinition> AssignmentDefinitions => Definitions;
        public static IReadOnlyList<string> Categories { get; private set; } = Array.Empty<string>();

        public static void Configure(IEnumerable<ItemDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var values = definitions.ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in values)
                if (string.IsNullOrWhiteSpace(item.ItemId) || string.IsNullOrWhiteSpace(item.Category) || !ids.Add(item.ItemId))
                    throw new ArgumentException("Every item needs a unique ID and category.", nameof(definitions));
            Definitions = Array.AsReadOnly(values);
            Categories = Array.AsReadOnly(values.Select(d => d.Category).Distinct(StringComparer.Ordinal).ToArray());
        }

        public static IReadOnlyList<ItemDefinition> DefinitionsInCategory(string category) =>
            Array.AsReadOnly(Definitions.Where(d => string.Equals(d.Category, category?.Trim(), StringComparison.Ordinal)).ToArray());
        public static string DisplayNameOf(string itemId) =>
            Definitions.FirstOrDefault(d => d.ItemId == itemId).DisplayName ?? itemId?.Trim() ?? string.Empty;
        public static string CategoryOf(string itemId)
        {
            var id = itemId?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            var definition = Definitions.FirstOrDefault(d => d.ItemId == id);
            return string.IsNullOrEmpty(definition.ItemId) ? string.Empty : definition.Category;
        }
        public static string VisualSourceIdOf(string itemId) => itemId?.Trim() ?? string.Empty;
        public static ItemDefinition AssignedDefinition(int index) => Definitions[index];
        public static ItemDefinition AssignedSourceDefinition(int index) => Definitions[index];
        public static string AssignedObjectId(int index) => Definitions[index].ItemId;
    }
}
