using System;

namespace Game.Core.Settings
{
    /// <summary>
    /// What the 일반 tab of the settings screen holds: the values themselves,
    /// with no opinion about where they are shown or stored.
    /// </summary>
    /// <remarks>
    /// A value type, so the screen can hold two of them — what is applied and
    /// what is being edited — and tell them apart with a single comparison.
    /// Adding a general option is a field here, a line in
    /// <see cref="Equals(GeneralSettings)"/>, and a default in
    /// <see cref="GeneralSettingsSystem.Defaults"/>.
    /// </remarks>
    public readonly struct GeneralSettings : IEquatable<GeneralSettings>
    {
        /// <summary>
        /// The language the interface is drawn in, as a code from the
        /// <see cref="LanguageCatalog"/>.
        /// </summary>
        public string LanguageCode { get; }

        public GeneralSettings(string languageCode)
        {
            LanguageCode = languageCode ?? string.Empty;
        }

        public GeneralSettings WithLanguage(string languageCode) =>
            new GeneralSettings(languageCode);

        public bool Equals(GeneralSettings other) =>
            string.Equals(LanguageCode, other.LanguageCode, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is GeneralSettings other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(LanguageCode);

        public static bool operator ==(GeneralSettings left, GeneralSettings right) => left.Equals(right);

        public static bool operator !=(GeneralSettings left, GeneralSettings right) => !left.Equals(right);

        public override string ToString() => $"General(language={LanguageCode})";
    }
}
