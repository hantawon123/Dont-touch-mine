using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the 사운드 settings in Unity's player preferences, beside the
    /// other tabs'.
    /// </summary>
    /// <inheritdoc cref="PlayerPrefsGraphicsSettingsStore"/>
    public sealed class PlayerPrefsSoundSettingsStore : ISoundSettingsStore
    {
        private const string Prefix = "game.settings.sound.";
        private const string DeviceKey = Prefix + "device";
        private const string InputModeKey = Prefix + "inputMode";

        private static readonly SoundVolume[] Volumes =
            (SoundVolume[])Enum.GetValues(typeof(SoundVolume));

        public bool TryLoad(out SoundSettings settings)
        {
            settings = SoundSettings.Empty;
            var found = false;

            foreach (var volume in Volumes)
            {
                var key = Prefix + volume;
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                settings = settings.With(volume, PlayerPrefs.GetInt(key, SoundCatalog.Unset));
                found = true;
            }

            if (PlayerPrefs.HasKey(DeviceKey))
            {
                settings = settings.WithDevice(PlayerPrefs.GetString(DeviceKey));
                found = true;
            }

            if (PlayerPrefs.HasKey(InputModeKey))
            {
                settings = settings.WithInputMode(PlayerPrefs.GetString(InputModeKey));
                found = true;
            }

            return found;
        }

        public void Save(SoundSettings settings)
        {
            foreach (var volume in Volumes)
            {
                var percent = settings.Get(volume);
                if (percent == SoundCatalog.Unset)
                {
                    Debug.LogWarning($"[Settings] Not saving an unset {volume} volume.");
                    continue;
                }

                PlayerPrefs.SetInt(Prefix + volume, percent);
            }

            if (!string.IsNullOrWhiteSpace(settings.DeviceName))
            {
                PlayerPrefs.SetString(DeviceKey, settings.DeviceName);
            }

            if (!string.IsNullOrWhiteSpace(settings.InputMode))
            {
                PlayerPrefs.SetString(InputModeKey, settings.InputMode);
            }

            PlayerPrefs.Save();
        }
    }
}
