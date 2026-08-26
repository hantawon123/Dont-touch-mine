using Game.Core.Settings;
using TMPro;
using UnityEngine;

namespace Game.Client.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibilityText : MonoBehaviour
    {
        private TMP_Text text;
        private float baseSize;
        private Color baseColor;
        private FontStyles baseStyle;
        private TextOverflowModes baseOverflow;
        private bool captured;
        private bool contrastApplied;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
            CaptureBase();
        }

        private void OnEnable()
        {
            AccessibilitySettingsOutput.Changed += Apply;
            if (AccessibilitySettingsOutput.Current != null)
            {
                Apply(AccessibilitySettingsOutput.Current);
            }
        }

        private void OnDisable()
        {
            AccessibilitySettingsOutput.Changed -= Apply;
        }

        private void CaptureBase()
        {
            if (captured || text == null)
            {
                return;
            }

            baseSize = text.fontSize;
            baseColor = text.color;
            baseStyle = text.fontStyle;
            baseOverflow = text.overflowMode;
            captured = true;
        }

        private void Apply(AccessibilitySettingsState settings)
        {
            if (text == null)
            {
                return;
            }

            CaptureBase();
            var multiplier = settings.GetTextScaleMultiplier();
            text.fontSize = baseSize * multiplier;
            text.overflowMode = multiplier > 1.01f
                ? TextOverflowModes.Overflow
                : baseOverflow;
            if (settings.HighContrastEnabled)
            {
                ApplyContrast();
                contrastApplied = true;
                return;
            }

            if (contrastApplied)
            {
                RestoreContrast();
                contrastApplied = false;
            }
        }

        private void ApplyContrast()
        {
            var luminance = (0.299f * baseColor.r) + (0.587f * baseColor.g) + (0.114f * baseColor.b);
            var lightText = luminance >= 0.85f;
            text.color = lightText ? Color.white : Color.black;
            text.fontStyle = baseStyle | FontStyles.Bold;
            text.outlineWidth = 0.2f;
            text.outlineColor = lightText ? Color.black : Color.white;
        }

        private void RestoreContrast()
        {
            text.color = baseColor;
            text.fontStyle = baseStyle;
            text.outlineWidth = 0f;
        }
    }
}
