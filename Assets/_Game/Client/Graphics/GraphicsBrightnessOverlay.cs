using Game.Client.Home;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Graphics
{
    [DisallowMultipleComponent]
    public sealed class GraphicsBrightnessOverlay : MonoBehaviour
    {
        private Image veil;

        private void OnEnable()
        {
            GraphicsSettingsOutput.Changed += Apply;
            EnsureVeil();
            if (GraphicsSettingsOutput.Current != null)
            {
                Apply(GraphicsSettingsOutput.Current);
            }
        }

        private void OnDisable()
        {
            GraphicsSettingsOutput.Changed -= Apply;
        }

        private void LateUpdate()
        {
            if (veil != null)
            {
                veil.transform.SetAsLastSibling();
            }
        }

        private void EnsureVeil()
        {
            if (veil != null)
            {
                return;
            }

            if (GetComponent<Canvas>() == null)
            {
                return;
            }

            var veilObject = new GameObject(
                "BrightnessVeil",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            veilObject.transform.SetParent(transform, false);
            var rect = veilObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var layout = veilObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            veil = veilObject.GetComponent<Image>();
            veil.sprite = HomeUiFonts.WhiteSprite;
            veil.type = Image.Type.Simple;
            veil.raycastTarget = false;
            veil.maskable = false;
            veil.color = Color.clear;
        }

        private void Apply(GraphicsSettingsState settings)
        {
            EnsureVeil();
            if (veil == null)
            {
                return;
            }

            veil.transform.SetAsLastSibling();
            var t = (settings.Brightness - GraphicsSettingsState.DefaultBrightness) / 50f;
            if (Mathf.Approximately(t, 0f))
            {
                veil.color = Color.clear;
                return;
            }

            if (t < 0f)
            {
                veil.color = new Color(0f, 0f, 0f, -t * 0.75f);
                return;
            }

            veil.color = new Color(1f, 1f, 1f, t * 0.6f);
        }
    }
}
