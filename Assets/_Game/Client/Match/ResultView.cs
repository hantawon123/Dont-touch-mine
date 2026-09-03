using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IResultView
    {
        void SetText(string value);
    }

    public sealed class ResultView : MonoBehaviour, IResultView
    {
        [SerializeField] private TMP_FontAsset font;
        private TMP_Text label;

        public void Initialize()
        {
            if (font == null) throw new System.InvalidOperationException("ResultView: 결과 표시용 TMP 폰트를 연결하세요.");
            if (label != null) return;
            var canvasObject = new GameObject("Result Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var backgroundObject = new GameObject(
                "Result Background",
                typeof(RectTransform),
                typeof(Image));
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            var background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.04f, 0.04f, 0.04f, 1f);
            background.raycastTarget = false;
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            var textObject = new GameObject("Result Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 40;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.richText = false;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void SetText(string value) => label.text = value ?? string.Empty;
    }
}
