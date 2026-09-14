using System;
using System.Collections.Generic;
using Game.Core.Maps;
using System.Linq;
using Game.SOAP.Config;

namespace Game.Client.Lobby
{
    public readonly struct PlaySettingsCategoryOption
    {
        public PlaySettingsCategoryOption(string id, string label)
        {
            Id = id?.Trim() ?? string.Empty;
            Label = label ?? string.Empty;
        }

        /// <summary>Empty id selects a random assignment category at runtime.</summary>
        public string Id { get; }

        public string Label { get; }

        public bool IsRandom => string.IsNullOrEmpty(Id);
    }

    /// <summary>
    /// Category choices read from the authored catalog; random always comes first.
    /// </summary>
    public static class PlaySettingsCategoryCatalog
    {
        private static PlaySettingsCategoryOption[] Options => new[] { new PlaySettingsCategoryOption(string.Empty, "랜덤") }
            .Concat(ItemCatalogSO.Load().categories.Where(c => c.enabled)
                .Select(c => new PlaySettingsCategoryOption(c.id, c.label))).ToArray();

        public static IReadOnlyList<PlaySettingsCategoryOption> All => Options;

        public static int DefaultIndex => 0;

        public static PlaySettingsCategoryOption Default => Options[DefaultIndex];

        public static PlaySettingsCategoryOption GetOption(int index)
        {
            if (Options.Length == 0)
            {
                return default;
            }

            return Options[Math.Clamp(index, 0, Options.Length - 1)];
        }

        public static int IndexOf(string categoryId)
        {
            var normalized = categoryId?.Trim() ?? string.Empty;
            for (var i = 0; i < Options.Length; i++)
            {
                if (string.Equals(Options[i].Id, normalized, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool Contains(string categoryId) => IndexOf(categoryId) >= 0;

        public static string LabelOf(string categoryId)
        {
            var index = IndexOf(categoryId);
            if (index < 0)
            {
                return string.IsNullOrWhiteSpace(categoryId) ? string.Empty : categoryId.Trim();
            }

            return GetOption(index).Label;
        }
    }

    public readonly struct PlaySettingsMapOption
    {
        public PlaySettingsMapOption(string id, string label)
        {
            Id = id?.Trim() ?? string.Empty;
            Label = label ?? string.Empty;
        }

        /// <summary>Empty id keeps a random playable map until match start.</summary>
        public string Id { get; }

        public string Label { get; }

        public bool IsRandom => string.IsNullOrEmpty(Id);
    }

    /// <summary>
    /// Map choices shown in play settings. Random stays first; playable maps
    /// come from <see cref="MapCatalog"/>. Add a map id there when another
    /// map is ready and it appears here automatically.
    /// </summary>
    public static class PlaySettingsMapCatalog
    {
        private static readonly PlaySettingsMapOption[] Options = CreateOptions();

        public static IReadOnlyList<PlaySettingsMapOption> All { get; } = Options;

        public static int DefaultIndex => 0;

        public static PlaySettingsMapOption Default => Options[DefaultIndex];

        public static PlaySettingsMapOption GetOption(int index)
        {
            if (Options.Length == 0)
            {
                return default;
            }

            return Options[Math.Clamp(index, 0, Options.Length - 1)];
        }

        public static int IndexOf(string mapId)
        {
            var normalized = mapId?.Trim() ?? string.Empty;
            for (var i = 0; i < Options.Length; i++)
            {
                if (string.Equals(Options[i].Id, normalized, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool Contains(string mapId) => IndexOf(mapId) >= 0;

        private static PlaySettingsMapOption[] CreateOptions()
        {
            var playableMaps = MapCatalog.MapIds;
            var options = new PlaySettingsMapOption[playableMaps.Count + 1];
            options[0] = new PlaySettingsMapOption(string.Empty, "랜덤");
            for (var i = 0; i < playableMaps.Count; i++)
            {
                options[i + 1] = new PlaySettingsMapOption(playableMaps[i], playableMaps[i]);
            }

            return options;
        }
    }
}
