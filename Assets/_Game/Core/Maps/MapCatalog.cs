using System;
using System.Collections.Generic;

namespace Game.Core.Maps
{
    /// <summary>
    /// Maps currently available to room creation and match startup.
    /// Add a map id here when another playable map is ready. Lobby settings
    /// only list <see cref="LobbyMapIds"/>.
    /// </summary>
    public static class MapCatalog
    {
        /// <summary>마트 맵(Synty Shops 팩, 씬 <c>Supermarket</c>). 맵 id → 씬은 <c>NetworkScenes</c>가 잇는다.</summary>
        public const string SupermarketId = "supermarket";

        /// <summary>저택 맵(Synty Horror Mansion 팩, 씬 <c>Mansion</c>). 2층 저택 본관과 앞뜰이 플레이 구역.</summary>
        public const string MansionId = "mansion";

        private static readonly string[] MapIdValues =
        {
            SupermarketId,
            MansionId
        };

        private static readonly string[] LobbyMapIdValues =
        {
            SupermarketId,
            MansionId
        };

        private static readonly Random RandomPicker = new();

        public static IReadOnlyList<string> MapIds { get; } =
            Array.AsReadOnly(MapIdValues);

        /// <summary>Maps offered in lobby room settings.</summary>
        public static IReadOnlyList<string> LobbyMapIds { get; } =
            Array.AsReadOnly(LobbyMapIdValues);

        public static string DefaultMapId => LobbyMapIds[0];

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
            if (Contains(mapId))
            {
                return mapId.Trim();
            }

            if (IsRandom(mapId))
            {
                // Kept empty until match start, where PickRandom chooses a lobby map.
                return string.Empty;
            }

            return Contains(fallback) ? fallback.Trim() : DefaultMapId;
        }

        /// <summary>
        /// Picks one of the lobby maps. Used when the lobby map choice is random.
        /// </summary>
        public static string PickRandom()
        {
            var pool = LobbyMapIdValues.Length > 0 ? LobbyMapIdValues : MapIdValues;
            if (pool.Length == 0)
            {
                return DefaultMapId;
            }

            if (pool.Length == 1)
            {
                return pool[0];
            }

            lock (RandomPicker)
            {
                return pool[RandomPicker.Next(pool.Length)];
            }
        }
    }
}
