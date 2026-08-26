using System;
using System.Collections.Generic;

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
        public static IReadOnlyList<LobbyMapOption> SampleMaps { get; } = Array.AsReadOnly(new[]
        {
            new LobbyMapOption("market-01", "시장 골목"),
            new LobbyMapOption("rooftop-01", "옥상 정원"),
            new LobbyMapOption("station-01", "역전 광장"),
            new LobbyMapOption("park-01", "밤의 공원"),
            new LobbyMapOption("harbor-01", "항구 창고"),
            new LobbyMapOption("school-01", "폐교 복도"),
            new LobbyMapOption("subway-01", "지하 상가"),
            new LobbyMapOption("plaza-01", "도심 광장"),
        });

        public static int IndexOf(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return 0;
            }

            for (var i = 0; i < SampleMaps.Count; i++)
            {
                if (string.Equals(SampleMaps[i].Id, mapId.Trim(), StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
