using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the 일반 settings in Unity's player preferences, beside the region
    /// and the profile.
    /// </summary>
    /// <remarks>
    /// Preferences for the same reason <see cref="PlayerPrefsServerRegionStore"/>
    /// uses them: this is a handful of short values, and a file would bring a
    /// format, a path and a migration story for no gain. One key per value
    /// rather than one blob, so adding a value later cannot invalidate what
    /// was saved before.
    /// </remarks>
    public sealed class PlayerPrefsGeneralSettingsStore : IGeneralSettingsStore
    {
        private const string LanguageKey = "game.settings.general.language";

        public bool TryLoad(out GeneralSettings settings)
        {
            settings = default;
            if (!PlayerPrefs.HasKey(LanguageKey))
            {
                return false;
            }

            var language = PlayerPrefs.GetString(LanguageKey);
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            settings = new GeneralSettings(language.Trim());
            return true;
        }

        public void Save(GeneralSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.LanguageCode))
            {
                // Keeping the last good value means a bad write cannot leave the
                // next run drawing in a language nobody chose.
                Debug.LogWarning("[Settings] Not saving a blank language code.");
                return;
            }

            PlayerPrefs.SetString(LanguageKey, settings.LanguageCode.Trim());

            // Written through immediately: Unity flushes on a clean quit, and a
            // crash right after applying is when losing it would be most
            // confusing.
            PlayerPrefs.Save();
        }
    }
}
