using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the 컨트롤 settings in Unity's player preferences, beside the
    /// other tabs'.
    /// </summary>
    /// <remarks>
    /// Every key is written, including the ones nobody has given a key to: an
    /// action left unbound on purpose has to survive a restart, and it can only
    /// be told from an action nobody has ever set if the blank is written down.
    /// </remarks>
    public sealed class PlayerPrefsControlSettingsStore : IControlSettingsStore
    {
        private const string KeyPrefix = "game.settings.controls.key.";
        private const string TogglePrefix = "game.settings.controls.invert.";
        private const string SensitivityPrefix = "game.settings.controls.sensitivity.";

        private static readonly ControlAction[] Actions =
            (ControlAction[])Enum.GetValues(typeof(ControlAction));

        private static readonly ControlToggle[] Toggles =
            (ControlToggle[])Enum.GetValues(typeof(ControlToggle));

        private static readonly ControlSensitivity[] Sensitivities =
            (ControlSensitivity[])Enum.GetValues(typeof(ControlSensitivity));

        public bool TryLoad(out ControlSettings settings)
        {
            settings = ControlSettings.Empty;
            var found = false;

            foreach (var action in Actions)
            {
                var key = KeyPrefix + action;
                if (!PlayerPrefs.HasKey(key))
                {
                    // Never saved, so the action takes the key it ships with.
                    settings = settings.With(action, ControlCatalog.Defaults.Get(action));
                    continue;
                }

                settings = settings.With(action, PlayerPrefs.GetString(key).Trim());
                found = true;
            }

            foreach (var toggle in Toggles)
            {
                var key = TogglePrefix + toggle;
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                settings = settings.With(toggle, PlayerPrefs.GetString(key).Trim());
                found = true;
            }

            foreach (var sensitivity in Sensitivities)
            {
                var key = SensitivityPrefix + sensitivity;
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                settings = settings.With(sensitivity, PlayerPrefs.GetInt(key, ControlCatalog.Unset));
                found = true;
            }

            return found;
        }

        public void Save(ControlSettings settings)
        {
            foreach (var action in Actions)
            {
                PlayerPrefs.SetString(KeyPrefix + action, settings.Get(action));
            }

            foreach (var toggle in Toggles)
            {
                var code = settings.Get(toggle);
                if (string.IsNullOrWhiteSpace(code))
                {
                    Debug.LogWarning($"[Settings] Not saving a blank {toggle}.");
                    continue;
                }

                PlayerPrefs.SetString(TogglePrefix + toggle, code);
            }

            foreach (var sensitivity in Sensitivities)
            {
                var percent = settings.Get(sensitivity);
                if (percent == ControlCatalog.Unset)
                {
                    Debug.LogWarning($"[Settings] Not saving an unset {sensitivity}.");
                    continue;
                }

                PlayerPrefs.SetInt(SensitivityPrefix + sensitivity, percent);
            }

            PlayerPrefs.Save();
        }
    }
}
