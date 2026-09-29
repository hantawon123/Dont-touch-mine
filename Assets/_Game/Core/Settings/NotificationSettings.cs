using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// The rows of the 알림 tab, in the order they are drawn.
    /// </summary>
    /// <remarks>
    /// One so far. The design's mock-up draws three — 게임중, 친구 요청 and
    /// 게임 초대 — and only the invite is asked for yet; the other two are
    /// their own stories. Adding one is an entry here and an entry in
    /// <see cref="NotificationCatalog.Shipped"/>.
    /// </remarks>
    public enum NotificationOption
    {
        GameInvite
    }

    /// <summary>
    /// What the 알림 tab holds: whether each kind of notice is wanted.
    /// </summary>
    /// <inheritdoc cref="GraphicsSettings"/>
    public readonly struct NotificationSettings : IEquatable<NotificationSettings>
    {
        internal static readonly int RowCount = Enum.GetValues(typeof(NotificationOption)).Length;

        private readonly OptionValues values;

        internal NotificationSettings(OptionValues values)
        {
            this.values = values;
        }

        public static NotificationSettings Empty => default;

        internal OptionValues Values => values;

        public string Get(NotificationOption option) => values.Get((int)option);

        public NotificationSettings With(NotificationOption option, string code)
        {
            if ((int)option < 0 || (int)option >= RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(option));
            }

            return new NotificationSettings(values.With((int)option, code));
        }

        /// <summary>Whether this kind of notice is wanted.</summary>
        public bool IsOn(NotificationOption option) =>
            string.Equals(Get(option), InterfaceCatalog.On, StringComparison.Ordinal);

        public bool Equals(NotificationSettings other) => values == other.values;

        public override bool Equals(object obj) => obj is NotificationSettings other && Equals(other);

        public override int GetHashCode() => values.GetHashCode();

        public static bool operator ==(NotificationSettings left, NotificationSettings right) =>
            left.Equals(right);

        public static bool operator !=(NotificationSettings left, NotificationSettings right) =>
            !left.Equals(right);

        public override string ToString()
        {
            var parts = new string[RowCount];
            for (var index = 0; index < RowCount; index++)
            {
                var option = (NotificationOption)index;
                parts[index] = $"{option}={Get(option)}";
            }

            return "Notifications(" + string.Join(", ", parts) + ")";
        }
    }

    /// <summary>
    /// What every row of the 알림 tab offers, and what it starts on.
    /// </summary>
    public sealed class NotificationCatalog
    {
        private readonly OptionCatalog rows;

        public NotificationCatalog(IReadOnlyDictionary<NotificationOption, OptionChoices> choices)
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

            rows = new OptionCatalog(NotificationSettings.RowCount, byIndex);
        }

        /// <summary>
        /// What the game ships with.
        /// </summary>
        /// <remarks>
        /// The arrows step 끄기 then 켜기, the order the design lists them, and
        /// they start on 켜기, which is what its mock-up shows. On is also the
        /// only sensible place to start: a player who has never opened this tab
        /// has not decided to stop hearing about invitations, and one they
        /// never see looks like a friend who never asked.
        /// </remarks>
        public static NotificationCatalog Shipped { get; } = new NotificationCatalog(
            new Dictionary<NotificationOption, OptionChoices>
            {
                [NotificationOption.GameInvite] = new OptionChoices(
                    InterfaceCatalog.On,
                    new OptionChoice(InterfaceCatalog.Off, UiText.Settings.Off),
                    new OptionChoice(InterfaceCatalog.On, UiText.Settings.On))
            });

        public OptionChoices For(NotificationOption option) => rows.For((int)option);

        /// <inheritdoc cref="OptionCatalog.Defaults"/>
        public NotificationSettings Defaults => new NotificationSettings(rows.Defaults);

        /// <inheritdoc cref="OptionCatalog.Normalise"/>
        public NotificationSettings Normalise(NotificationSettings settings) =>
            new NotificationSettings(rows.Normalise(settings.Values));

        /// <inheritdoc cref="OptionCatalog.Label"/>
        public string Label(NotificationOption option, string code, string languageCode = "ko") =>
            UiTextCatalog.Shipped.Get(rows.Label((int)option, code), languageCode);
    }
}
