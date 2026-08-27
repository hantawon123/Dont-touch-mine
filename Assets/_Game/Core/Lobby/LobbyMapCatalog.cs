using System;
using System.Collections.Generic;
using Game.Core.Maps;

namespace Game.Core.Lobby
{
    public readonly struct LobbyMapOption
    {
        public LobbyMapOption(string id, string displayName)
        {
            Id = id?.Trim() ?? string.Empty;
            DisplayName = displayName?.Trim() ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    public static class LobbyMapCatalog
    {
        public static IReadOnlyList<LobbyMapOption> Maps { get; } = CreateMaps();

        public static int IndexOf(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return 0;
            }

            for (var i = 0; i < Maps.Count; i++)
            {
                if (string.Equals(Maps[i].Id, mapId.Trim(), StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        private static IReadOnlyList<LobbyMapOption> CreateMaps()
        {
            var options = new LobbyMapOption[MapCatalog.MapIds.Count];
            for (var i = 0; i < options.Length; i++)
            {
                var mapId = MapCatalog.MapIds[i];
                options[i] = new LobbyMapOption(mapId, mapId);
            }

            return Array.AsReadOnly(options);
        }
    }
}
