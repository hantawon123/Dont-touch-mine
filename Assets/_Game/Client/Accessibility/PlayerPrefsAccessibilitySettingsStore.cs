using System;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Client.Accessibility
{
    public sealed class PlayerPrefsAccessibilitySettingsStore : IAccessibilitySettingsStore
    {
        private const string UiScaleKey = "Game.Accessibility.UiScale";
        private const string TextScaleKey = "Game.Accessibility.TextScale";
        private const string HighContrastKey = "Game.Accessibility.HighContrast";

        public AccessibilitySettingsState LoadOrDefault()
        {
            return new AccessibilitySettingsState(
                PlayerPrefs.GetInt(UiScaleKey, AccessibilitySettingsState.DefaultScale),
                PlayerPrefs.GetInt(TextScaleKey, AccessibilitySettingsState.DefaultScale),
                PlayerPrefs.GetInt(HighContrastKey, 0) != 0);
        }

        public void Save(AccessibilitySettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetInt(UiScaleKey, settings.UiScale);
            PlayerPrefs.SetInt(TextScaleKey, settings.TextScale);
            PlayerPrefs.SetInt(HighContrastKey, settings.HighContrastEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
