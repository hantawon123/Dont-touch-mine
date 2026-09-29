using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the 알림 settings in Unity's player preferences, beside the other
    /// tabs'.
    /// </summary>
    /// <inheritdoc cref="PlayerPrefsGraphicsSettingsStore"/>
    public sealed class PlayerPrefsNotificationSettingsStore : INotificationSettingsStore
    {
        private const string Prefix = "game.settings.notifications.";

        private static readonly NotificationOption[] Options =
            (NotificationOption[])Enum.GetValues(typeof(NotificationOption));

        public bool TryLoad(out NotificationSettings settings)
        {
            settings = NotificationSettings.Empty;
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

        public void Save(NotificationSettings settings)
        {
            foreach (var option in Options)
            {
                var code = settings.Get(option);
                if (string.IsNullOrWhiteSpace(code))
                {
                    Debug.LogWarning($"[Settings] Not saving a blank {option}.");
                    continue;
                }

                PlayerPrefs.SetString(Prefix + option, code.Trim());
            }

            PlayerPrefs.Save();
        }
    }
}
