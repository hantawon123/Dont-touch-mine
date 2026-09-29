using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the 그래픽 settings in Unity's player preferences, beside the
    /// 일반 settings and the region.
    /// </summary>
    /// <remarks>
    /// One key per row rather than one blob, for the same reason
    /// <see cref="PlayerPrefsGeneralSettingsStore"/> does it: a row added later
    /// leaves everything already saved readable, and the missing one falls back
    /// to its default through <see cref="GraphicsCatalog.Normalise"/>.
    /// </remarks>
    public sealed class PlayerPrefsGraphicsSettingsStore : IGraphicsSettingsStore
    {
        private const string Prefix = "game.settings.graphics.";

        private static readonly GraphicsOption[] Options =
            (GraphicsOption[])Enum.GetValues(typeof(GraphicsOption));

        public bool TryLoad(out GraphicsSettings settings)
        {
            settings = GraphicsSettings.Empty;
            var found = false;

            foreach (var option in Options)
            {
                var key = Prefix + option;
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                var code = PlayerPrefs.GetString(key);
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                settings = settings.With(option, code.Trim());
                found = true;
            }

            return found;
        }

        public void Save(GraphicsSettings settings)
        {
            foreach (var option in Options)
            {
                var code = settings.Get(option);
                if (string.IsNullOrWhiteSpace(code))
                {
                    // Keeping the last good value means a half-filled write
                    // cannot leave the next run drawing with nothing chosen.
                    Debug.LogWarning($"[Settings] Not saving a blank {option}.");
                    continue;
                }

                PlayerPrefs.SetString(Prefix + option, code.Trim());
            }

            // Written through immediately: Unity flushes on a clean quit, and a
            // crash right after applying is when losing it would be most
            // confusing.
            PlayerPrefs.Save();
        }
    }
}
