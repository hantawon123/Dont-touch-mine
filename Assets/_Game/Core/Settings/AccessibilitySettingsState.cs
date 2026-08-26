using System;

namespace Game.Core.Settings
{
    public enum AccessibilitySettingsError
    {
        None,
        InvalidScale
    }

    public sealed class AccessibilitySettingsState
    {
        public const int MinScale = 0;
        public const int MaxScale = 100;
        public const int DefaultScale = 50;

        public const float MinUiMultiplier = 0.8f;
        public const float MaxUiMultiplier = 1.25f;
        public const float MinTextMultiplier = 0.8f;
        public const float MaxTextMultiplier = 1.5f;

        public AccessibilitySettingsState()
            : this(DefaultScale, DefaultScale, false)
        {
        }

        public AccessibilitySettingsState(int uiScale, int textScale, bool highContrastEnabled)
        {
            UiScale = ClampScale(uiScale);
            TextScale = ClampScale(textScale);
            HighContrastEnabled = highContrastEnabled;
        }

        public int UiScale { get; private set; }

        public int TextScale { get; private set; }

        public bool HighContrastEnabled { get; private set; }

        public float GetUiScaleMultiplier()
        {
            return ToMultiplier(UiScale, MinUiMultiplier, MaxUiMultiplier);
        }

        public float GetTextScaleMultiplier()
        {
            return ToMultiplier(TextScale, MinTextMultiplier, MaxTextMultiplier);
        }

        public bool TrySetUiScale(int percent, out AccessibilitySettingsError error)
        {
            return TrySetScale(percent, value => UiScale = value, out error);
        }

        public bool TrySetTextScale(int percent, out AccessibilitySettingsError error)
        {
            return TrySetScale(percent, value => TextScale = value, out error);
        }

        public bool TrySetHighContrastEnabled(bool enabled, out AccessibilitySettingsError error)
        {
            HighContrastEnabled = enabled;
            error = AccessibilitySettingsError.None;
            return true;
        }

        public static float ToMultiplier(int percent, float min, float max)
        {
            var clamped = ClampScale(percent);
            if (clamped <= DefaultScale)
            {
                return min + ((1f - min) * (clamped / (float)DefaultScale));
            }

            return 1f + ((max - 1f) * ((clamped - DefaultScale) / (float)(MaxScale - DefaultScale)));
        }

        private static bool TrySetScale(
            int percent,
            Action<int> assign,
            out AccessibilitySettingsError error)
        {
            if (percent < MinScale || percent > MaxScale)
            {
                error = AccessibilitySettingsError.InvalidScale;
                return false;
            }

            assign(percent);
            error = AccessibilitySettingsError.None;
            return true;
        }

        private static int ClampScale(int percent)
        {
            if (percent < MinScale)
            {
                return MinScale;
            }

            if (percent > MaxScale)
            {
                return MaxScale;
            }

            return percent;
        }
    }
}
