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
        private readonly GeneralSettingsSystem general;
        private string languageCode;

        /// <param name="catalog">
        /// What the lookup offers. Null takes what the game ships with.
        /// </param>
        public UiLocale(GeneralSettingsSystem general, UiTextCatalog catalog = null)
        {
            this.general = general ?? throw new ArgumentNullException(nameof(general));
            Catalog = catalog ?? UiTextCatalog.Shipped;
            languageCode = general.Current.LanguageCode;
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
