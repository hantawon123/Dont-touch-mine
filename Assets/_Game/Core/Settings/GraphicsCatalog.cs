using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// What every row of the 그래픽 tab offers, and what it starts on.
    /// </summary>
    /// <remarks>
    /// The codes are what gets saved and what the appliers read, so they are
    /// written to be read by both a human and a machine — "1920x1080", "120",
    /// "high" — and must not change once a build has shipped. The labels beside
    /// them are the only part the player sees, and are free to change.
    /// <para>
    /// Data rather than a static list, so a test can hand a screen a couple of
    /// choices; the game itself uses <see cref="Shipped"/>.
    /// </para>
    /// </remarks>
    public sealed class GraphicsCatalog
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";

        public const string Fullscreen = "fullscreen";
        public const string Windowed = "windowed";

        private readonly OptionCatalog rows;

        public GraphicsCatalog(IReadOnlyDictionary<GraphicsOption, OptionChoices> choices)
        {
            if (choices == null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            var byIndex = new Dictionary<int, OptionChoices>();
            foreach (var pair in choices)
            {
                byIndex[(int)pair.Key] = pair.Value;
            }

            rows = new OptionCatalog(GraphicsSettings.RowCount, byIndex);
        }

        /// <summary>
        /// What the game ships with, from the design's own lists.
        /// </summary>
        /// <remarks>
        /// The defaults are not stated by the design; they are the values its
        /// mock-up happens to show, which sit mid-range rather than at either
        /// extreme. Worth settling deliberately once the game has been profiled
        /// on the machines it is meant to run on.
        /// </remarks>
        public static GraphicsCatalog Shipped { get; } = new GraphicsCatalog(
            new Dictionary<GraphicsOption, OptionChoices>
            {
                [GraphicsOption.DisplayMode] = new OptionChoices(
                    new OptionChoice(Fullscreen, UiText.Settings.Fullscreen),
                    new OptionChoice(Windowed, UiText.Settings.Windowed)),

                [GraphicsOption.Resolution] = new OptionChoices(
                    "1920x1080",
                    new OptionChoice("1280x720", "1280x720"),
                    new OptionChoice("1600x900", "1600x900"),
                    new OptionChoice("1920x1080", "1920x1080"),
                    new OptionChoice("2560x1440", "2560x1440"),
                    new OptionChoice("3840x2160", "3840x2160")),

                [GraphicsOption.FpsLimit] = new OptionChoices(
                    new OptionChoice("120", "120"),
                    new OptionChoice("60", "60"),
                    new OptionChoice("40", "40"),
                    new OptionChoice("30", "30")),

                [GraphicsOption.TextureQuality] = new OptionChoices(
                    Medium,
                    new OptionChoice(Low, UiText.Settings.Low),
                    new OptionChoice(Medium, UiText.Settings.Medium),
                    new OptionChoice(High, UiText.Settings.High))
            });

        public OptionChoices For(GraphicsOption option) => rows.For((int)option);

        /// <inheritdoc cref="OptionCatalog.Defaults"/>
        public GraphicsSettings Defaults => new GraphicsSettings(rows.Defaults);

        /// <inheritdoc cref="OptionCatalog.Normalise"/>
        public GraphicsSettings Normalise(GraphicsSettings settings) =>
            new GraphicsSettings(rows.Normalise(settings.Values));

        /// <inheritdoc cref="OptionCatalog.Label"/>
        public string Label(GraphicsOption option, string code, string languageCode = "ko") =>
            UiTextCatalog.Shipped.Get(rows.Label((int)option, code), languageCode);
    }
}
