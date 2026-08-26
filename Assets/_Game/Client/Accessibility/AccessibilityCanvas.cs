using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibilityCanvas : MonoBehaviour
    {
        private CanvasScaler scaler;
        private Vector2 baseResolution;
        private float baseScaleFactor = 1f;
        private bool captured;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
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
            if (captured || scaler == null)
            {
                return;
            }

            baseResolution = scaler.referenceResolution;
            baseScaleFactor = scaler.scaleFactor;
            captured = true;
        }

        private void Apply(AccessibilitySettingsState settings)
        {
            if (scaler == null)
            {
                return;
            }

            CaptureBase();
            var multiplier = settings.GetUiScaleMultiplier();
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.referenceResolution = baseResolution / multiplier;
                return;
            }

            scaler.scaleFactor = baseScaleFactor * multiplier;
        }
    }
}
