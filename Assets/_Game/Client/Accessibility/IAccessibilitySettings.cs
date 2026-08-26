using System;
using Game.Core.Settings;

namespace Game.Client.Accessibility
{
    public interface IAccessibilitySettings
    {
        AccessibilitySettingsState Current { get; }

        event Action<AccessibilitySettingsState> Changed;

        bool TrySetUiScale(int percent, out AccessibilitySettingsError error);

        bool TrySetTextScale(int percent, out AccessibilitySettingsError error);

        bool TrySetHighContrastEnabled(bool enabled, out AccessibilitySettingsError error);
    }
}
