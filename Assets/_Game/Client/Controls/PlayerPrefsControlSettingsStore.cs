using System;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Client.Controls
{
    public sealed class PlayerPrefsControlSettingsStore : IControlSettingsStore
    {
        private const string KeyPrefix = "Game.Controls.";

        public ControlSettingsState LoadOrDefault()
        {
            var settings = new ControlSettingsState();
            var actions = (ControlAction[])Enum.GetValues(typeof(ControlAction));
            for (var index = 0; index < actions.Length; index++)
            {
                var action = actions[index];
                var saved = PlayerPrefs.GetString(KeyPrefix + action, string.Empty);
                if (!string.IsNullOrEmpty(saved))
                {
                    settings.TrySetPath(action, saved, out _);
                }
            }

            return settings;
        }

        public void Save(ControlSettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var actions = (ControlAction[])Enum.GetValues(typeof(ControlAction));
            for (var index = 0; index < actions.Length; index++)
            {
                var action = actions[index];
                PlayerPrefs.SetString(KeyPrefix + action, settings.GetPath(action));
            }

            PlayerPrefs.Save();
        }
    }
}
