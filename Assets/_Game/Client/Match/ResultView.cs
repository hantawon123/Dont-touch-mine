using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IResultView
    {
        void SetText(string value);
        void SetOutcome(string headline, string subtitle);

        /// <summary>
        /// 문구 뒤의 불투명 배경 판. 유치장 무대처럼 3D 장면을 뒤에 보여줄 때는 끈다.
        /// </summary>
        void SetBackdropVisible(bool visible);
    }

    public sealed class ResultView : MonoBehaviour, IResultView
    {
        [SerializeField] private TMP_FontAsset font;
        private TMP_Text label;
        private TMP_Text headline;
        private TMP_Text subtitle;

        public void Initialize()
        {
            font = HomeUiFonts.Apply() ?? font;
            if (font == null)
            {
                throw new System.InvalidOperationException("ResultView: Paperlogy TMP 폰트를 찾지 못했습니다.");
            }

            if (label != null && headline != null && subtitle != null)
            {
                label.font = font;
                headline.font = HomeUiFonts.ApplyBlack() ?? font;
                subtitle.font = font;
                backdrop = label.transform.parent.Find("Result Background")?.GetComponent<Image>();
                ApplyOutcomePlacement();
                return;
            }

            var canvasObject = transform.Find("Result Canvas")?.gameObject;
            if (canvasObject == null)
            {
                canvasObject = new GameObject(
                    "Result Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));
                canvasObject.transform.SetParent(transform, false);
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            EnsureBackground(canvasObject.transform);
            backdrop = canvasObject.transform.Find("Result Background")?.GetComponent<Image>();

            label = canvasObject.transform.Find("Result Text")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                label = CreateText(
                    canvasObject.transform,
                    "Result Text",
                    40f,
                    Color.white,
                    font);
            }
            else
            {
                label.font = font;
            }

            headline = canvasObject.transform.Find("Result Headline")?.GetComponent<TMP_Text>();
            if (headline == null)
            {
                headline = CreateText(
                    canvasObject.transform,
                    "Result Headline",
                    MatchTimerView.TimerFontSize,
                    MatchTimerView.TimerColor,
                    HomeUiFonts.ApplyBlack() ?? font);
            }

            subtitle = canvasObject.transform.Find("Result Subtitle")?.GetComponent<TMP_Text>();
            if (subtitle == null)
            {
                subtitle = CreateText(
                    canvasObject.transform,
                    "Result Subtitle",
                    MatchTimerView.HintFontSize,
                    MatchTimerView.ResultSubtitleColor,
                    font);
            }

            ApplyOutcomePlacement();
            label.gameObject.SetActive(false);
        }

        public void SetText(string value)
        {
            if (headline != null)
            {
                headline.gameObject.SetActive(false);
            }

            if (subtitle != null)
            {
                subtitle.gameObject.SetActive(false);
            }

            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.text = value ?? string.Empty;
            }
        }

        public void SetOutcome(string headlineText, string subtitleText)
        {
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }

            if (headline != null)
            {
                headline.gameObject.SetActive(true);
                headline.text = headlineText ?? string.Empty;
                headline.font = HomeUiFonts.ApplyBlack() ?? headline.font;
                headline.color = MatchTimerView.TimerColor;
            }

            if (subtitle != null)
            {
                subtitle.gameObject.SetActive(true);
                subtitle.text = subtitleText ?? string.Empty;
                subtitle.color = MatchTimerView.ResultSubtitleColor;
            }

            ApplyOutcomePlacement();
        }

        private void ApplyOutcomePlacement()
        {
            if (headline != null)
            {
                PlaceTop(
                    headline.rectTransform,
                    new Vector2(0f, -HidingActiveHudView.TopPadding),
                    new Vector2(980f, MatchTimerView.TimerHeight));
                headline.fontSize = MatchTimerView.TimerFontSize;
                headline.alignment = TextAlignmentOptions.Center;
            }

            if (subtitle != null)
            {
                PlaceTop(
                    subtitle.rectTransform,
                    new Vector2(0f, -(HidingActiveHudView.TopPadding + MatchTimerView.TimerHeight)),
                    new Vector2(1200f, MatchTimerView.HintHeight));
                subtitle.fontSize = MatchTimerView.HintFontSize;
                subtitle.alignment = TextAlignmentOptions.Center;
            }

            if (label != null)
            {
                PlaceTop(
                    label.rectTransform,
                    new Vector2(0f, -240f),
                    new Vector2(1400f, 240f));
            }
        }

        private Image backdrop;

        public void SetBackdropVisible(bool visible)
        {
            if (backdrop != null) backdrop.enabled = visible;
        }

        private static void EnsureBackground(Transform parent)
        {
            var existing = parent.Find("Result Background");
            if (existing != null)
            {
                return;
            }

            var backgroundObject = new GameObject(
                "Result Background",
                typeof(RectTransform),
                typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            var background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.04f, 0.04f, 0.04f, 1f);
            background.raycastTarget = false;
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            float fontSize,
            Color color,
            TMP_FontAsset font)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.richText = false;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static void PlaceTop(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
