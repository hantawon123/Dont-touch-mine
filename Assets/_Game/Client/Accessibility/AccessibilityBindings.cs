using TMPro;
using UnityEngine;

namespace Game.Client.Accessibility
{
    public static class AccessibilityBindings
    {
        public static void EnsureCanvas(GameObject canvasObject)
        {
            if (canvasObject == null)
            {
                return;
            }

            if (canvasObject.GetComponent<AccessibilityCanvas>() == null)
            {
                canvasObject.AddComponent<AccessibilityCanvas>();
            }
        }

        public static void EnsureText(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            if (text.GetComponent<AccessibilityText>() == null)
            {
                text.gameObject.AddComponent<AccessibilityText>();
            }
        }

        public static void EnsureLayout(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (target.GetComponent<AccessibilityLayout>() == null)
            {
                target.AddComponent<AccessibilityLayout>();
            }
        }
    }
}
