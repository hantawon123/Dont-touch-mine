using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibilityLayout : MonoBehaviour
    {
        private LayoutElement layout;
        private RectTransform rect;
        private float preferredHeight;
        private float minHeight;
        private float preferredWidth;
        private float minWidth;
        private Vector2 sizeDelta;
        private Vector2 anchoredPosition;
        private Vector2 offsetMin;
        private Vector2 offsetMax;
        private bool captured;

        private void Awake()
        {
            layout = GetComponent<LayoutElement>();
            rect = transform as RectTransform;
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
            if (captured)
            {
                return;
            }

            if (layout != null)
            {
                preferredHeight = layout.preferredHeight;
                minHeight = layout.minHeight;
                preferredWidth = layout.preferredWidth;
                minWidth = layout.minWidth;
            }

            if (rect != null)
            {
                sizeDelta = rect.sizeDelta;
                anchoredPosition = rect.anchoredPosition;
                offsetMin = rect.offsetMin;
                offsetMax = rect.offsetMax;
            }

            captured = true;
        }

        private void Apply(AccessibilitySettingsState settings)
        {
            CaptureBase();
            var multiplier = settings.GetTextScaleMultiplier();
            if (layout != null)
            {
                if (preferredHeight > 0f)
                {
                    layout.preferredHeight = preferredHeight * multiplier;
                }

                if (minHeight > 0f)
                {
                    layout.minHeight = minHeight * multiplier;
                }

                if (preferredWidth > 0f)
                {
                    layout.preferredWidth = preferredWidth * multiplier;
                }

                if (minWidth > 0f)
                {
                    layout.minWidth = minWidth * multiplier;
                }

                return;
            }

            if (rect == null)
            {
                return;
            }

            var nextSize = sizeDelta;
            if (Mathf.Abs(nextSize.y) > 0.01f)
            {
                nextSize.y = sizeDelta.y * multiplier;
            }

            rect.sizeDelta = nextSize;

            var nextPosition = anchoredPosition;
            if (Mathf.Abs(nextPosition.y) > 0.01f)
            {
                nextPosition.y = anchoredPosition.y * multiplier;
            }

            rect.anchoredPosition = nextPosition;

            var nextMin = offsetMin;
            var nextMax = offsetMax;
            if (Mathf.Abs(nextMin.y) > 0.01f)
            {
                nextMin.y = offsetMin.y * multiplier;
            }

            if (Mathf.Abs(nextMax.y) > 0.01f)
            {
                nextMax.y = offsetMax.y * multiplier;
            }

            rect.offsetMin = nextMin;
            rect.offsetMax = nextMax;
        }
    }
}
