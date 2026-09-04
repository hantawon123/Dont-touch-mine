using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IHidingTurnStartView
    {
        void Show(double remainingSeconds);
        void Hide();
        void SetRemainingSeconds(double remainingSeconds);
    }

    /// <summary>
    /// The first beat of your hiding turn: a large stopwatch and a banner.
    /// After one second the presenter will swap this for the corner HUD.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HidingTurnStartView : MonoBehaviour, IHidingTurnStartView
    {
        public const float VisibleSeconds = 1f;
        public const string BannerText = "제한 시간 안에 물건을 숨겨주세요!";
        public const float TimerFontSize = 48f;
        public const float BannerFontSize = 55f;
        public const float BannerCornerRadius = 24f;
        private const string TimerSpriteResource = "UI/image_timer";

        private static readonly Color TimerColor = new Color(1f, 0.54f, 0.24f, 1f);
        private static readonly Color BannerColor = new Color(0.07f, 0.09f, 0.16f, 0.94f);
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

        public void Show(double remainingSeconds)
        {
            shown = true;
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

            var font = HomeUiFonts.Apply();
            bannerText.font = font;
            bannerText.fontSize = BannerFontSize;
            bannerText.fontStyle = FontStyles.Normal;
            bannerText.enableWordWrapping = false;
            bannerText.overflowMode = TextOverflowModes.Overflow;
            bannerText.text = BannerText;
            if (timerText != null)
            {
                timerText.font = font;
                timerText.fontSize = TimerFontSize;
                timerText.fontStyle = FontStyles.Normal;
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
        }

        private void BuildLayout()
        {
            EnsureOverlayCanvas();

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);
            Place(content, new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(1280f, 420f));

            var banner = CreateImage(content, "Banner", BannerColor, BannerRoundedSprite);
            ApplyBannerCorner(banner);
            Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1180f, 120f));

            bannerText = CreateText(banner.rectTransform, "Label", BannerText, BannerFontSize);
            bannerText.enableWordWrapping = false;
            bannerText.overflowMode = TextOverflowModes.Overflow;
            Stretch(bannerText.rectTransform, 24f);

            var stopwatch = CreateRect(content, "Stopwatch");
            Place(stopwatch, new Vector2(0.5f, 0.5f), new Vector2(0f, 88f), new Vector2(280f, 320f));

            var face = CreateImage(stopwatch, "Face", Color.white, LoadTimerSprite() ?? HomeUiFonts.CircleSprite);
            face.preserveAspect = true;
            Place(face.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 320f));

            timerText = CreateText(stopwatch, "Timer", FormatTimer(previewRemainingSeconds), TimerFontSize);
            timerText.color = TimerColor;
            timerText.enableWordWrapping = false;
            timerText.overflowMode = TextOverflowModes.Overflow;
            Place(timerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(168f, 56f));
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
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            text.enableWordWrapping = true;
            text.font = HomeUiFonts.Apply();
            text.fontStyle = FontStyles.Normal;
            return text;
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
            rect.anchoredPosition = anchoredPosition;
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
