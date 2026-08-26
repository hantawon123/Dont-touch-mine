using Game.Core.Settings;

namespace Game.Client.Accessibility
{
    public interface IAccessibilitySettingsStore
    {
        AccessibilitySettingsState LoadOrDefault();

        void Save(AccessibilitySettingsState settings);
    }
}
