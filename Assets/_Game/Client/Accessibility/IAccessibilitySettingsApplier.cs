using Game.Core.Settings;

namespace Game.Client.Accessibility
{
    public interface IAccessibilitySettingsApplier
    {
        void Apply(AccessibilitySettingsState settings);
    }
}
