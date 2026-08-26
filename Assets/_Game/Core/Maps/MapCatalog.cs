using System;
using System.Collections.Generic;

namespace Game.Core.Maps
{
    /// <summary>
    /// Maps currently available to room creation and match startup.
    /// Add a map id here when another playable map is ready.
    /// </summary>
    public static class MapCatalog
    {
        public const string PlaygroundId = "playground";

        private static readonly string[] MapIdValues =
        {
            PlaygroundId
        };

        public static IReadOnlyList<string> MapIds { get; } =
            Array.AsReadOnly(MapIdValues);

        public static string DefaultMapId => MapIds[0];

        public static bool Contains(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return false;
            }

            var candidate = mapId.Trim();

            foreach (var availableMapId in MapIds)
            {
                if (string.Equals(availableMapId, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
