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

        /// <summary>마트 맵(Synty Shops 팩, 씬 <c>Supermarket</c>). 맵 id → 씬은 <c>NetworkScenes</c>가 잇는다.</summary>
        public const string SupermarketId = "supermarket";

        private static readonly string[] MapIdValues =
        {
            PlaygroundId,
            SupermarketId
        };

        private static readonly Random RandomPicker = new();

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

        /// <summary>
        /// Empty id is the lobby's random choice; it is resolved to a playable
        /// map when the match starts, not when the host saves settings.
        /// </summary>
        public static bool IsRandom(string mapId) => string.IsNullOrWhiteSpace(mapId);

        public static bool IsLobbyChoice(string mapId) => IsRandom(mapId) || Contains(mapId);

        public static string NormalizeLobbyMapId(string mapId, string fallback)
        {
            if (IsRandom(mapId))
            {
                return string.Empty;
            }

            return Contains(mapId) ? mapId.Trim() : fallback?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Picks one of the playable maps. Used when the lobby map choice is random.
        /// </summary>
        public static string PickRandom()
        {
            if (MapIdValues.Length == 0)
            {
                return DefaultMapId;
            }

            if (MapIdValues.Length == 1)
            {
                return MapIdValues[0];
            }

            lock (RandomPicker)
            {
                return MapIdValues[RandomPicker.Next(MapIdValues.Length)];
            }
        }
    }
}
