using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// The rows of the 인터페이스 tab, in the order they are drawn.
    /// </summary>
    public enum InterfaceOption
    {
        UiScale,
        FontScale,
        InGameUi,
        FpsCounter,
        PingCounter,
        PlayerNames,
        StreamerMode,
        BeginnerGuide,
        ChatScope
    }

    /// <summary>
    /// What the 인터페이스 tab holds: one chosen code per row.
    /// </summary>
    /// <inheritdoc cref="GraphicsSettings"/>
    public readonly struct InterfaceSettings : IEquatable<InterfaceSettings>
    {
        internal static readonly int RowCount = Enum.GetValues(typeof(InterfaceOption)).Length;

        private readonly OptionValues values;

        internal InterfaceSettings(OptionValues values)
        {
            this.values = values;
        }

        public static InterfaceSettings Empty => default;

        internal OptionValues Values => values;

        public string Get(InterfaceOption option) => values.Get((int)option);

        public InterfaceSettings With(InterfaceOption option, string code)
        {
            if ((int)option < 0 || (int)option >= RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(option));
            }

            return new InterfaceSettings(values.With((int)option, code));
        }

        /// <summary>Whether a row is on. Every row here is on or off.</summary>
        public bool IsOn(InterfaceOption option) =>
            string.Equals(Get(option), InterfaceCatalog.On, StringComparison.Ordinal);

        public bool Equals(InterfaceSettings other) => values == other.values;

        public override bool Equals(object obj) => obj is InterfaceSettings other && Equals(other);

        public override int GetHashCode() => values.GetHashCode();

        public static bool operator ==(InterfaceSettings left, InterfaceSettings right) => left.Equals(right);

        public static bool operator !=(InterfaceSettings left, InterfaceSettings right) => !left.Equals(right);

        public override string ToString()
        {
            var parts = new string[RowCount];
            for (var index = 0; index < RowCount; index++)
            {
                var option = (InterfaceOption)index;
                parts[index] = $"{option}={Get(option)}";
            }

            return "Interface(" + string.Join(", ", parts) + ")";
        }
    }

    /// <summary>
    /// What every row of the 인터페이스 tab offers, and what it starts on.
    /// </summary>
    public sealed class InterfaceCatalog
    {
        public const string On = "on";
        public const string Off = "off";

        public const string Large = "large";
        public const string Medium = "medium";
        public const string Small = "small";

        private static readonly OptionChoices Toggle = new OptionChoices(
            new OptionChoice(On, "켜기"),
            new OptionChoice(Off, "끄기"));

        private static OptionChoices Scale => new OptionChoices(
            Medium,
            new OptionChoice(Small, "작게"),
            new OptionChoice(Medium, "중간"),
            new OptionChoice(Large, "크게"));

        /// <summary>
        /// 스트리머 모드, which is off unless a player turns it on.
        /// </summary>
        /// <remarks>
        /// The one toggle that does not start on. The others describe how a
        /// player wants the screen to look; this one changes what everybody
        /// else sees of them, and that is not something to have happened to
        /// somebody who never opened the tab.
        /// </remarks>
        private static OptionChoices StreamerToggle => new OptionChoices(
            Off,
            new OptionChoice(On, "켜기"),
            new OptionChoice(Off, "끄기"));

        private readonly OptionCatalog rows;

        public InterfaceCatalog(IReadOnlyDictionary<InterfaceOption, OptionChoices> choices)
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

            rows = new OptionCatalog(InterfaceSettings.RowCount, byIndex);
        }

        /// <summary>
        /// What the game ships with, from the design's own lists.
        /// </summary>
        /// <remarks>
        /// The two name rows read differently from each other on purpose.
        /// 다른 플레이어 이름 표시 is about this player's own screen and hides
        /// nothing from anybody else; 스트리머 모드 is about everybody else's
        /// screens and does not change what this player sees. 끄기 on the first
        /// draws no nameplate; 켜기 on the second replaces this player's name
        /// with a <see cref="Pseudonym"/> wherever anybody else would read it.
        /// <para>
        /// 채팅 메시지 범위 제한 reads the other way round from the rest: it is
        /// a restriction, so 끄기 is everybody and 켜기 is friends only. The
        /// labels say so rather than leaving the player to work it out, and
        /// unrestricted is the default.
        /// </para>
        /// </remarks>
        public static InterfaceCatalog Shipped { get; } = new InterfaceCatalog(
            new Dictionary<InterfaceOption, OptionChoices>
            {
                [InterfaceOption.UiScale] = Scale,
                [InterfaceOption.FontScale] = Scale,
                [InterfaceOption.InGameUi] = Toggle,
                [InterfaceOption.FpsCounter] = Toggle,
                [InterfaceOption.PingCounter] = Toggle,
                [InterfaceOption.PlayerNames] = Toggle,
                [InterfaceOption.StreamerMode] = StreamerToggle,
                [InterfaceOption.BeginnerGuide] = Toggle,
                [InterfaceOption.ChatScope] = new OptionChoices(
                    new OptionChoice(Off, "끄기(모두)"),
                    new OptionChoice(On, "켜기(친구만)"))
            });

        public OptionChoices For(InterfaceOption option) => rows.For((int)option);

        /// <inheritdoc cref="OptionCatalog.Defaults"/>
        public InterfaceSettings Defaults => new InterfaceSettings(rows.Defaults);

        /// <inheritdoc cref="OptionCatalog.Normalise"/>
        public InterfaceSettings Normalise(InterfaceSettings settings) =>
            new InterfaceSettings(rows.Normalise(settings.Values));

        /// <inheritdoc cref="OptionCatalog.Label"/>
        public string Label(InterfaceOption option, string code) => rows.Label((int)option, code);
    }
}
