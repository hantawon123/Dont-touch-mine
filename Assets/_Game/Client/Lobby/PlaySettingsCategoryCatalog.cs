using System;
using System.Collections.Generic;
using Game.Core.Maps;
using Game.Core.Settings;
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
        private static PlaySettingsCategoryOption[] Options => new[] { new PlaySettingsCategoryOption(string.Empty, PlaySettingsMapCatalog.RandomLabel) }
            .Concat(ItemCatalogSO.LoadMetadata().categories.Where(c => c.enabled)
                .Select(c => new PlaySettingsCategoryOption(c.id, c.label))).ToArray();

        public static IReadOnlyList<PlaySettingsCategoryOption> All => Options;

        public static int DefaultIndex => 0;

        public static PlaySettingsCategoryOption Default => Options[DefaultIndex];

        public static PlaySettingsCategoryOption GetOption(int index)
        {
            var options = Options;
            if (options.Length == 0)
            {
                return default;
            }

            return options[Math.Clamp(index, 0, options.Length - 1)];
        }

        public static int IndexOf(string categoryId)
        {
            var normalized = categoryId?.Trim() ?? string.Empty;
            var options = Options;
            for (var i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i].Id, normalized, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool Contains(string categoryId) => IndexOf(categoryId) >= 0;

        public static string LabelOf(string categoryId) =>
            LabelOf(categoryId, UiLocale.AppliedLanguage);

        public static string LabelOf(string categoryId, string language)
        {
            var normalized = categoryId?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return UiTextCatalog.Shipped.Get(UiText.Play.Random, language);
            }

            var key = KeyFor(normalized);
            if (key != null)
            {
                return UiTextCatalog.Shipped.Get(key, language);
            }

            // Displaying a label must not validate and rebuild every item definition.
            foreach (var category in ItemCatalogSO.LoadMetadata().categories)
            {
                if (category.enabled &&
                    string.Equals(category.id?.Trim(), normalized, StringComparison.Ordinal))
                {
                    return category.label ?? string.Empty;
                }
            }

            return normalized;
        }

        private static string KeyFor(string categoryId)
        {
            switch (categoryId)
            {
                case "food":
                    return UiText.Category.Food;
                case "household":
                    return UiText.Category.Household;
                case "bathroom":
                    return UiText.Category.Bathroom;
                case "plants":
                    return UiText.Category.Plants;
                case "tools":
                    return UiText.Category.Tools;
                case "modern":
                    return UiText.Category.Modern;
                case "fantasy":
                    return UiText.Category.Fantasy;
                case "beach":
                    return UiText.Category.Beach;
                case "casino":
                    return UiText.Category.Casino;
                case "halloween":
                    return UiText.Category.Halloween;
                case "toys":
                    return UiText.Category.Toys;
                case "reserve":
                    return UiText.Category.Reserve;
                default:
                    return null;
            }
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
    /// Map choices shown in play settings. Random comes first; only playable maps are
    /// out. Playable lobby maps come from <see cref="MapCatalog.LobbyMapIds"/>.
    /// </summary>
    public static class PlaySettingsMapCatalog
    {
        public static string RandomLabel =>
            UiLocale.Applied(UiText.Play.Random);

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

        public static string LabelOf(string mapId) =>
            LabelOf(mapId, UiLocale.AppliedLanguage);

        public static string LabelOf(string mapId, string language)
        {
            var normalized = mapId?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return UiTextCatalog.Shipped.Get(UiText.Play.Random, language);
            }

            var key = KeyFor(normalized);
            return key != null
                ? UiTextCatalog.Shipped.Get(key, language)
                : normalized;
        }

        private static string KeyFor(string mapId)
        {
            if (string.Equals(mapId, MapCatalog.SupermarketId, StringComparison.Ordinal))
            {
                return UiText.Map.Supermarket;
            }

            if (string.Equals(mapId, MapCatalog.MansionId, StringComparison.Ordinal))
            {
                return UiText.Map.Mansion;
            }

            return null;
        }

        private static PlaySettingsMapOption[] CreateOptions()
        {
            var playableMaps = MapCatalog.LobbyMapIds;
            var options = new PlaySettingsMapOption[playableMaps.Count + 1];
            options[0] = new PlaySettingsMapOption(string.Empty, RandomLabel);
            for (var i = 0; i < playableMaps.Count; i++)
            {
                options[i + 1] = new PlaySettingsMapOption(playableMaps[i], playableMaps[i]);
            }

            return options;
        }
    }
}
