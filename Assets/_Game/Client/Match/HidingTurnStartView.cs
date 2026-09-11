using System;
using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IHidingTurnStartView
    {
        void Show(double remainingSeconds, string bannerText = null);
        void Hide();
        void SetRemainingSeconds(double remainingSeconds);
    }

    /// <summary>
    /// The first beat of a timed warning: a large stopwatch and a banner.
    /// Hiding uses this at the start of a turn; searching reuses it when the
    /// last thirty seconds begin. After one second the presenter hides it.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HidingTurnStartView : MonoBehaviour, IHidingTurnStartView
    {
        public const float VisibleSeconds = 1f;
        public const string BannerText = "제한 시간 안에 물건을 숨겨주세요!";
        public const string FinalWarningBannerText = "서둘러 자신의 물건을 확보하세요!";
        public const float TimerFontSize = 64f;
        public const float BannerFontSize = 55f;
        public const float BannerWidthPercent = 0.7f;
        public const float BannerHeight = 200f;
        public const float BannerCornerRadius = 40f;
        public static readonly Vector2 StopwatchSize = new Vector2(350f, 400f);
        public static readonly Vector2 BannerPosition = Vector2.zero;
        public static readonly Vector2 StopwatchPosition = new Vector2(0f, 140f);
        private const string TimerSpriteResource = "UI/image_timer";

        private static readonly Color TimerColor = new Color(1f, 0.54f, 0.24f, 1f);
        private static readonly Color BannerColor = new Color32(0x0B, 0x10, 0x18, 0xFF);
        private static Sprite bannerRoundedSprite;

        [SerializeField]
        private GameObject root;

        [SerializeField]
        private TMP_Text timerText;

        [SerializeField]
        private TMP_Text bannerText;

        [SerializeField]
        [Tooltip("Shows the overlay in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        [SerializeField]
        private float previewRemainingSeconds = 30f;

        private int lastTotalSeconds = -1;
        private bool shown;
        private string currentBannerText = BannerText;

        public static string FormatTimer(double remainingSeconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        public static HidingTurnStartView Create(Transform parent)
        {
            var rootObject = new GameObject("HidingTurnStart", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<HidingTurnStartView>();
        }

        private void OnEnable()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show(previewRemainingSeconds);
                return;
            }

            if (!shown)
            {
                SetVisualsVisible(false);
            }
        }

        public void Show(double remainingSeconds, string bannerText = null)
        {
            shown = true;
            currentBannerText = string.IsNullOrWhiteSpace(bannerText)
                ? BannerText
                : bannerText;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            transform.SetAsLastSibling();
            SetRemainingSeconds(remainingSeconds);
            ApplyBanner();
            SetVisualsVisible(true);
        }

        public void Hide()
        {
            shown = false;
            SetVisualsVisible(false);
        }

        public void SetRemainingSeconds(double remainingSeconds)
        {
            if (timerText == null)
            {
                return;
            }

            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds));
            if (totalSeconds == lastTotalSeconds)
            {
                return;
            }

            timerText.text = FormatTimer(remainingSeconds);
            lastTotalSeconds = totalSeconds;
        }

        private void ApplyBanner()
        {
            if (bannerText == null)
            {
                return;
            }

            ApplySemiBold(bannerText, BannerFontSize);
            bannerText.textWrappingMode = TextWrappingModes.NoWrap;
            bannerText.overflowMode = TextOverflowModes.Overflow;
            bannerText.text = currentBannerText;
            if (timerText != null)
            {
                ApplySemiBold(timerText, TimerFontSize);
                timerText.alignment = TextAlignmentOptions.Midline;
                timerText.color = TimerColor;
            }
        }

        private void EnsureLayout()
        {
            if (root == null)
            {
                root = transform.Find("Content")?.gameObject ?? gameObject;
            }

            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            if (transform.Find("Content/Stopwatch/Bezel") != null)
            {
                DestroyChild("Content");
            }

            if (transform.Find("Content") == null)
            {
                BuildLayout();
            }

            if (timerText == null)
            {
                timerText = transform.Find("Content/Stopwatch/Timer")?.GetComponent<TMP_Text>();
            }

            if (bannerText == null)
            {
                bannerText = transform.Find("Content/Banner/Label")?.GetComponent<TMP_Text>();
            }

            ApplyBannerCorner(transform.Find("Content/Banner")?.GetComponent<Image>());
            ApplyOverlayLayout();
            transform.Find("Content/Stopwatch")?.SetAsFirstSibling();
        }

        private void ApplyOverlayLayout()
        {
            EnsureOverlayCanvas();
            var content = transform.Find("Content") as RectTransform;
            var stopwatch = transform.Find("Content/Stopwatch") as RectTransform;
            var banner = transform.Find("Content/Banner") as RectTransform;
            var face = transform.Find("Content/Stopwatch/Face") as RectTransform;
            if (content == null || stopwatch == null || banner == null)
            {
                return;
            }

            Stretch(content);
            Place(stopwatch, new Vector2(0.5f, 0.5f), StopwatchPosition, StopwatchSize);
            PlaceBanner(banner);
            if (face != null)
            {
                Place(face, new Vector2(0.5f, 0.5f), Vector2.zero, StopwatchSize);
            }

            if (timerText != null)
            {
                Place(
                    timerText.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    StopwatchSize);
                timerText.alignment = TextAlignmentOptions.Midline;
                timerText.margin = Vector4.zero;
            }

            stopwatch.SetAsFirstSibling();
        }

        private void BuildLayout()
        {
            EnsureOverlayCanvas();

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);

            var stopwatch = CreateRect(content, "Stopwatch");
            var banner = CreateImage(content, "Banner", BannerColor, BannerRoundedSprite);
            ApplyBannerCorner(banner);

            bannerText = CreateText(banner.rectTransform, "Label", BannerText, BannerFontSize);
            bannerText.textWrappingMode = TextWrappingModes.NoWrap;
            bannerText.overflowMode = TextOverflowModes.Overflow;
            Stretch(bannerText.rectTransform, 24f);

            var face = CreateImage(stopwatch, "Face", Color.white, LoadTimerSprite() ?? HomeUiFonts.CircleSprite);
            face.preserveAspect = true;

            timerText = CreateText(stopwatch, "Timer", FormatTimer(previewRemainingSeconds), TimerFontSize);
            timerText.color = TimerColor;
            timerText.textWrappingMode = TextWrappingModes.NoWrap;
            timerText.overflowMode = TextOverflowModes.Overflow;
            ApplyOverlayLayout();
        }

        private static Sprite BannerRoundedSprite
        {
            get
            {
                if (bannerRoundedSprite != null)
                {
                    return bannerRoundedSprite;
                }

                const int size = 64;
                var radius = Mathf.RoundToInt(BannerCornerRadius);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        texture.SetPixel(x, y, IsInsideRoundedRect(x, y, size, radius)
                            ? Color.white
                            : Color.clear);
                    }
                }

                texture.Apply(false, false);
                bannerRoundedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));
                bannerRoundedSprite.hideFlags = HideFlags.HideAndDontSave;
                return bannerRoundedSprite;
            }
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
        {
            var innerMin = radius;
            var innerMax = size - radius;
            if (x >= innerMin && x < innerMax)
            {
                return true;
            }

            if (y >= innerMin && y < innerMax)
            {
                return true;
            }

            var cornerX = x < innerMin ? innerMin : innerMax;
            var cornerY = y < innerMin ? innerMin : innerMax;
            var dx = x - cornerX;
            var dy = y - cornerY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        private void SetVisualsVisible(bool visible)
        {
            var content = transform.Find("Content");
            if (content != null)
            {
                content.gameObject.SetActive(visible);
                return;
            }

            if (root != null && root != gameObject)
            {
                root.SetActive(visible);
            }
        }

        private static void ApplyBannerCorner(Image banner)
        {
            if (banner == null)
            {
                return;
            }

            banner.sprite = BannerRoundedSprite;
            banner.color = BannerColor;
            banner.type = Image.Type.Sliced;
            banner.pixelsPerUnitMultiplier = 1f;
        }

        private void EnsureOverlayCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            var parentCanvas = transform.parent != null
                ? transform.parent.GetComponentInParent<Canvas>()
                : null;
            if (parentCanvas != null && parentCanvas != canvas)
            {
                canvas.renderMode = parentCanvas.renderMode;
                canvas.worldCamera = parentCanvas.worldCamera;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 240;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static Sprite LoadTimerSprite()
        {
            return Resources.Load<Sprite>(TimerSpriteResource);
        }

        private void DestroyChild(string childName)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            DestroyImmediate(child.gameObject);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);

            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Midline;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            ApplySemiBold(text, fontSize);
            return text;
        }

        private const string SemiBoldResource = "Fonts/Paperlogy-6SemiBold";
        private static TMP_FontAsset paperlogySemiBold;

        private static TMP_FontAsset ResolveSemiBold()
        {
            if (paperlogySemiBold != null)
            {
                return paperlogySemiBold;
            }

            var applied = HomeUiFonts.Apply();
            if (applied != null &&
                applied.name.IndexOf("SemiBold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                paperlogySemiBold = applied;
                return paperlogySemiBold;
            }

            var source = Resources.Load<Font>(SemiBoldResource);
            if (source != null)
            {
                paperlogySemiBold = HomeUiFonts.CreateRuntimeKorean(source);
            }

            if (paperlogySemiBold == null)
            {
                paperlogySemiBold = applied;
            }

            return paperlogySemiBold;
        }

        private static void ApplySemiBold(TMP_Text text, float fontSize)
        {
            if (text == null)
            {
                return;
            }

            var font = ResolveSemiBold();
            if (font != null)
            {
                text.font = font;
                if (font.material != null)
                {
                    text.fontSharedMaterial = font.material;
                }
            }

            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
        }

        private static void PlaceBanner(RectTransform banner)
        {
            var side = (1f - BannerWidthPercent) * 0.5f;
            banner.anchorMin = new Vector2(side, 0.5f);
            banner.anchorMax = new Vector2(1f - side, 0.5f);
            banner.pivot = new Vector2(0.5f, 0.5f);
            banner.anchoredPosition3D = new Vector3(BannerPosition.x, BannerPosition.y, 0f);
            banner.sizeDelta = new Vector2(0f, BannerHeight);
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
