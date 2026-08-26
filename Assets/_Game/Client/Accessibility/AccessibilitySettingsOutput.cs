using System;
using Game.Core.Settings;

namespace Game.Client.Accessibility
{
    public static class AccessibilitySettingsOutput
    {
        public static AccessibilitySettingsState Current { get; private set; }

        public static event Action<AccessibilitySettingsState> Changed;

        internal static void Publish(AccessibilitySettingsState settings)
        {
            Current = settings ?? throw new ArgumentNullException(nameof(settings));
            Changed?.Invoke(settings);
        }
    }
}
