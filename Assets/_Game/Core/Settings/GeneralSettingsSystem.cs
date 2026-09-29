using System;
using Game.Core.Ports;

namespace Game.Core.Settings
{
    /// <summary>
    /// Remembers the general settings for as long as the process lives.
    /// </summary>
    /// <remarks>
    /// For containers built without a machine behind them — tests, and the
    /// service registration's own default — so that resolving the settings
    /// system never depends on player preferences existing.
    /// </remarks>
    public sealed class InMemoryGeneralSettingsStore : IGeneralSettingsStore
    {
        private GeneralSettings? saved;

        /// <summary>What was last saved, or null. For tests.</summary>
        public GeneralSettings? Saved => saved;

        public bool TryLoad(out GeneralSettings settings)
        {
            settings = saved ?? default;
            return saved.HasValue;
        }

        public void Save(GeneralSettings settings)
        {
            saved = settings;
        }
    }

    /// <summary>
    /// The 일반 settings in force, and remembering them.
    /// </summary>
    /// <remarks>
    /// Holds only what has been applied. The settings screen keeps its own
    /// draft while the player is still deciding, and hands it here through
    /// <see cref="Apply"/> when they press 적용하기 — so closing the screen
    /// without applying costs nothing more than the draft going away.
    /// <para>
    /// Reads the saved values once, at construction, because that is when the
    /// answer is needed: whatever draws in the chosen language asks for it
    /// before the settings screen is ever opened.
    /// </para>
    /// </remarks>
    public sealed class GeneralSettingsSystem
    {
        private readonly IGeneralSettingsStore store;

        /// <param name="languages">
        /// What the picker offers. Null takes what the game ships with.
        /// </param>
        public GeneralSettingsSystem(IGeneralSettingsStore store, LanguageCatalog languages = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Languages = languages ?? LanguageCatalog.Shipped;
            Current = store.TryLoad(out var saved) ? Normalise(saved) : Defaults;
        }

        public LanguageCatalog Languages { get; }

        public GeneralSettings Current { get; private set; }

        /// <summary>
        /// What a player who has never touched the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public GeneralSettings Defaults => new GeneralSettings(Languages.Default.Code);

        public event Action<GeneralSettings> Changed;

        /// <summary>
        /// Settles on these values and writes them down. Nothing happens, and
        /// nobody is told, when they are already the current ones.
        /// </summary>
        public void Apply(GeneralSettings settings)
        {
            var next = Normalise(settings);
            if (next == Current)
            {
                return;
            }

            Current = next;
            store.Save(next);
            Changed?.Invoke(next);
        }

        /// <summary>
        /// Brings a saved or offered value back within what the game can
        /// actually do. A language the catalogue no longer lists falls back to
        /// the default rather than being kept: the picker would otherwise show
        /// nothing selected while the game drew in something unnamed.
        /// </summary>
        public GeneralSettings Normalise(GeneralSettings settings)
        {
            var language = Languages.TryFind(settings.LanguageCode, out var listed)
                ? listed
                : Languages.Default;
            return settings.WithLanguage(language.Code);
        }
    }
}
