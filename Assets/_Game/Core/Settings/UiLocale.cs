using System;

namespace Game.Core.Settings
{
    /// <summary>
    /// The words the interface should draw right now: the catalogue, read in
    /// the language last applied.
    /// </summary>
    /// <remarks>
    /// Follows <see cref="GeneralSettingsSystem"/>, not the settings screen's
    /// draft, so stepping the language picker changes nothing until 적용하기.
    /// <see cref="Changed"/> fires only when that applied language actually
    /// moves, so a screen can redraw without caring how settings are stored.
    /// </remarks>
    public sealed class UiLocale : IDisposable
    {
        private static UiLocale current;
        private readonly GeneralSettingsSystem general;
        private string languageCode;

        /// <summary>The locale last built for the running game, or null in bare tests.</summary>
        public static UiLocale Current => current;

        /// <summary>Looks up <paramref name="key"/> in the applied language.</summary>
        public static string Applied(string key) =>
            current != null
                ? current.Get(key)
                : UiTextCatalog.Shipped.Get(key, "ko");

        /// <summary>The applied language code, or Korean when none is live.</summary>
        public static string AppliedLanguage =>
            current != null ? current.LanguageCode : "ko";

        /// <param name="catalog">
        /// What the lookup offers. Null takes what the game ships with.
        /// </param>
        public UiLocale(GeneralSettingsSystem general, UiTextCatalog catalog = null)
        {
            this.general = general ?? throw new ArgumentNullException(nameof(general));
            Catalog = catalog ?? UiTextCatalog.Shipped;
            languageCode = general.Current.LanguageCode;
            current = this;
            general.Changed += OnApplied;
        }

        public UiTextCatalog Catalog { get; }

        /// <summary>The language last applied, as a code from the catalogue.</summary>
        public string LanguageCode => languageCode;

        public event Action Changed;

        /// <summary>
        /// The words for <paramref name="key"/> in the applied language.
        /// </summary>
        public string Get(string key) => Catalog.Get(key, languageCode);

        public void Dispose()
        {
            general.Changed -= OnApplied;
            if (current == this)
            {
                current = null;
            }
        }

        private void OnApplied(GeneralSettings settled)
        {
            if (string.Equals(settled.LanguageCode, languageCode, StringComparison.Ordinal))
            {
                return;
            }

            languageCode = settled.LanguageCode;
            Changed?.Invoke();
        }
    }
}
