using System;
using Game.Core.Settings;

namespace Game.Client.Accessibility
{
    public sealed class AccessibilitySettingsService : IAccessibilitySettings
    {
        private readonly IAccessibilitySettingsStore store;
        private readonly IAccessibilitySettingsApplier applier;

        public AccessibilitySettingsService(
            IAccessibilitySettingsStore store,
            IAccessibilitySettingsApplier applier)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
            Current = store.LoadOrDefault() ?? new AccessibilitySettingsState();
            ApplyCurrent();
        }

        public AccessibilitySettingsState Current { get; }

        public event Action<AccessibilitySettingsState> Changed;

        public bool TrySetUiScale(int percent, out AccessibilitySettingsError error)
        {
            if (Current.UiScale == percent)
            {
                error = AccessibilitySettingsError.None;
                return true;
            }

            if (!Current.TrySetUiScale(percent, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetTextScale(int percent, out AccessibilitySettingsError error)
        {
            if (Current.TextScale == percent)
            {
                error = AccessibilitySettingsError.None;
                return true;
            }

            if (!Current.TrySetTextScale(percent, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetHighContrastEnabled(bool enabled, out AccessibilitySettingsError error)
        {
            if (Current.HighContrastEnabled == enabled)
            {
                error = AccessibilitySettingsError.None;
                return true;
            }

            if (!Current.TrySetHighContrastEnabled(enabled, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        private void PersistAndApply()
        {
            store.Save(Current);
            ApplyCurrent();
            Changed?.Invoke(Current);
        }

        private void ApplyCurrent()
        {
            applier.Apply(Current);
            AccessibilitySettingsOutput.Publish(Current);
        }
    }
}
