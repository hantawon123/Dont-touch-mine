using System;
using System.Collections.Generic;

namespace Game.Core.Items
{
    /// <summary>
    /// Player-item candidates already placed as carryable props in Playground.
    /// Add another surface-resting prop here when it becomes an assignment candidate.
    /// </summary>
    public static class ItemCatalog
    {
        private static readonly ItemDefinition[] DefinitionValues =
        {
            new("Soda_01", "food"),
            new("Burger_01", "food"),
            new("Pineapple_01", "food"),
            new("Cup1_C3", "tableware"),
            new("Plate1_C1", "tableware"),
            new("Plant_01", "decoration"),
            new("Kettle1_C1", "kitchen"),
            new("Toaster_03", "kitchen")
        };

        public static IReadOnlyList<ItemDefinition> Definitions { get; } =
            Array.AsReadOnly(DefinitionValues);
    }
}
