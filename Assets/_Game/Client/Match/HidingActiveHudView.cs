using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    /// <summary>
    /// Edge HUD for the hiding phase: top timer, complete guide, and key list.
    /// Input wiring belongs to the presenter; this view only paints.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingActiveHudView : MonoBehaviour
    {
        public static string HintText =>
            UiTextCatalog.Shipped.Get(UiText.Match.HideHint, "ko");
        public static string WarningHintText =>
            UiTextCatalog.Shipped.Get(UiText.Match.HideWarning, "ko");
        public const float WarningSeconds = 10f;
        public const float TopPadding = 20f;
        public const float HeartbeatPeakScale = 1.08f;
        public const float HeartbeatPeriod = 0.9f;
        public static readonly Color WarningColor = new Color(1f, 0.54f, 0.24f, 1f);
        public static string CompleteText =>
            UiTextCatalog.Shipped.Get(UiText.Match.HideComplete, "ko");
        public const string CompleteKey = "Y";
        public const float TimerFontSize = 58.5f;
        public const float TimerWidth = 468f;
        public const float TimerHeight = 72.8f;
        public const float HintFontSize = 28f;
        public const float GuideFontSize = 24f;
        public const float ActionFontSize = 18f;
        public const float KeyChipWidth = 35f;
        public const float KeyChipHeight = 35f;
        public const float KeyChipFontSize = 18f;
        public const float KeyChipPaddingX = 10f;
        public const float KeyChipCornerRadius = 10f;
        public const float KeyIconSize = 24f;
        public static string ClickKeyLabel => KeySettingGuideView.ClickKeyLabel;
        public static readonly Color KeyChipColor = new Color(0f, 0f, 0f, 0.27f);
        public static string[] KeyGuideActions => KeySettingGuideView.Actions;
        public static string[] KeyGuideLabels => KeySettingGuideView.Labels;

        private static Sprite keyChipSprite;

        [SerializeField]
        private TMP_Text timerText;

        [SerializeField]
        private TMP_Text hintText;

        [SerializeField]
        private GameObject topPrompt;

        [SerializeField]
        private GameObject completeGuide;

        [SerializeField]
        [Tooltip("Shows the hiding HUD in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        [SerializeField]
        private float previewRemainingSeconds = 30f;

        private int lastTotalSeconds = -1;
        private double lastRemainingSeconds;
        private bool shown;
        private bool warningActive;
        private UiLocale chromeLocale;

        public void ShowChrome(UiLocale locale)
        {
            chromeLocale = locale;
            EnsureLayout();
            ApplyUrgency(lastRemainingSeconds);
            ApplyCompleteKeyStyle();
        }

        private string Copy(string key) =>
            chromeLocale != null
                ? chromeLocale.Get(key)
                : UiLocale.Applied(key);

        public static HidingActiveHudView Create(Transform parent)
        {
            var rootObject = new GameObject("HidingActiveHud", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<HidingActiveHudView>();
        }

        private void Awake()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show(previewRemainingSeconds, true, true);
                return;
            }

            if (!shown)
            {
                Hide();
            }
        }

        public void Show(double remainingSeconds, bool showTopPrompt, bool showCompleteGuide)
        {
            shown = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            SetRemainingSeconds(remainingSeconds);
            ApplyFonts();
            ApplyUrgency(remainingSeconds);
            SetTopPromptVisible(showTopPrompt);
            SetCompleteGuideVisible(showCompleteGuide);
        }

        public void Hide()
        {
            shown = false;
            warningActive = false;
            ResetPulseScale();
            SetTopPromptVisible(false);
            SetCompleteGuideVisible(false);
        }

        public void SetRemainingSeconds(double remainingSeconds)
        {
            lastRemainingSeconds = remainingSeconds;
            if (timerText == null)
            {
                return;
            }

            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds));
            if (totalSeconds != lastTotalSeconds)
            {
                timerText.text = HidingTurnStartView.FormatTimer(remainingSeconds);
                lastTotalSeconds = totalSeconds;
            }

            ApplyUrgency(remainingSeconds);
        }

        public static bool IsWarning(double remainingSeconds)
        {
            return Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds)) <= WarningSeconds;
        }

        public static float HeartbeatScale(float time)
        {
            var cycle = Mathf.Repeat(time, HeartbeatPeriod) / HeartbeatPeriod;
            var beat = Pulse(cycle) + (0.62f * Pulse(cycle - 0.22f));
            return 1f + ((HeartbeatPeakScale - 1f) * beat);
        }

        private void Update()
        {
            if (!shown || !warningActive || topPrompt == null || !topPrompt.activeSelf)
            {
                ResetPulseScale();
                return;
            }

            topPrompt.transform.localScale = Vector3.one * HeartbeatScale(Time.unscaledTime);
        }

        public void SetTopPromptVisible(bool visible)
        {
            if (topPrompt != null)
            {
                topPrompt.SetActive(visible);
            }
        }

        public void SetCompleteGuideVisible(bool visible)
        {
            if (completeGuide != null)
            {
                completeGuide.SetActive(visible);
            }
        }

        private void ApplyFonts()
        {
            var font = HomeUiFonts.Apply();
            if (timerText != null)
            {
                timerText.font = font;
                timerText.fontSize = TimerFontSize;
                timerText.fontStyle = FontStyles.Normal;
            }

            if (hintText != null)
            {
                hintText.font = font;
                hintText.fontSize = HintFontSize;
                hintText.fontStyle = FontStyles.Normal;
            }

            ApplyUrgency(lastRemainingSeconds);
            ApplyCompleteKeyStyle();
        }

        private void ApplyUrgency(double remainingSeconds)
        {
            warningActive = IsWarning(remainingSeconds);
            var color = warningActive ? WarningColor : Color.white;
            var hint = warningActive
                ? Copy(UiText.Match.HideWarning)
                : Copy(UiText.Match.HideHint);

            if (timerText != null)
            {
                timerText.color = color;
            }

            if (hintText != null)
            {
                hintText.text = hint;
                hintText.color = color;
            }

            if (!warningActive)
            {
                ResetPulseScale();
            }
        }

        private void ResetPulseScale()
        {
            if (topPrompt != null)
            {
                topPrompt.transform.localScale = Vector3.one;
            }
        }

        private static float Pulse(float offset)
        {
            var wrapped = Mathf.Repeat(offset + 1f, 1f);
            if (wrapped > 0.5f)
            {
                wrapped -= 1f;
            }

            var falloff = wrapped * 14f;
            return Mathf.Exp(-(falloff * falloff));
        }

        private void ApplyCompleteKeyStyle()
        {
            if (completeGuide == null)
            {
                return;
            }

            var chip = completeGuide.transform.Find("Key") as RectTransform;
            var label = completeGuide.transform.Find("Key/Label")?.GetComponent<TMP_Text>();
            if (chip != null)
            {
                ApplyKeyChipLook(chip.GetComponent<Image>());
                Place(
                    chip,
                    new Vector2(0f, 0.5f),
                    new Vector2(24f, 0f),
                    new Vector2(KeyChipWidth, KeyChipHeight));
            }

            if (label != null)
            {
                label.font = HomeUiFonts.ApplyLight();
                label.fontSize = KeyChipFontSize;
                label.fontStyle = FontStyles.Normal;
                label.color = Color.white;
            }

            var caption = completeGuide.transform.Find("Caption") as RectTransform;
            if (caption != null)
            {
                var captionText = caption.GetComponent<TMP_Text>();
                if (captionText != null)
                {
                    captionText.text = Copy(UiText.Match.HideComplete);
                }

                Place(
                    caption,
                    new Vector2(0f, 0.5f),
                    new Vector2(24f + KeyChipWidth + 12f, 0f),
                    new Vector2(240f, KeyChipHeight),
                    new Vector2(0f, 0.5f));
            }
        }

        private void EnsureLayout()
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            if (transform.Find("TopPrompt") == null)
            {
                BuildLayout();
            }

            if (timerText == null)
            {
                timerText = transform.Find("TopPrompt/Timer")?.GetComponent<TMP_Text>();
            }

            if (hintText == null)
            {
                hintText = transform.Find("TopPrompt/Hint")?.GetComponent<TMP_Text>();
            }

            if (topPrompt == null)
            {
                topPrompt = transform.Find("TopPrompt")?.gameObject;
            }

            if (completeGuide == null)
            {
                completeGuide = transform.Find("CompleteGuide")?.gameObject;
            }

            StripLegacyKeyGuide();
            ApplyTopPromptLayout();
        }

        private void ApplyTopPromptLayout()
        {
            if (topPrompt != null)
            {
                topPrompt.transform.SetAsFirstSibling();
                Place(
                    topPrompt.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -TopPadding),
                    new Vector2(980f, TimerHeight + 54f),
                    new Vector2(0.5f, 1f));
            }

            if (timerText != null)
            {
                Place(
                    timerText.rectTransform,
                    new Vector2(0.5f, 1f),
                    Vector2.zero,
                    new Vector2(TimerWidth, TimerHeight),
                    new Vector2(0.5f, 1f));
            }

            if (hintText != null)
            {
                Place(
                    hintText.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -TimerHeight),
                    new Vector2(920f, 40f),
                    new Vector2(0.5f, 1f));
            }
        }

        private void BuildLayout()
        {
            topPrompt = CreateRect(transform, "TopPrompt").gameObject;
            timerText = CreateText(topPrompt.transform, "Timer", "00:30", TimerFontSize);
            hintText = CreateText(topPrompt.transform, "Hint", Copy(UiText.Match.HideHint), HintFontSize);
            ApplyTopPromptLayout();

            completeGuide = CreateRect(transform, "CompleteGuide").gameObject;
            Place(
                completeGuide.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(48f, 72f),
                new Vector2(360f, 48f),
                new Vector2(0f, 0.5f));

            var completeKey = CreateImage(
                completeGuide.transform,
                "Key",
                KeyChipColor,
                KeyChipSprite);
            ApplyKeyChipLook(completeKey);
            Place(
                completeKey.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(24f, 0f),
                new Vector2(KeyChipWidth, KeyChipHeight));
            var completeKeyLabel = CreateText(
                completeKey.transform,
                "Label",
                CompleteKey,
                KeyChipFontSize,
                HomeUiFonts.ApplyLight());
            Stretch(completeKeyLabel.rectTransform);

            var completeLabel = CreateText(completeGuide.transform, "Caption", Copy(UiText.Match.HideComplete), GuideFontSize);
            completeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            Place(
                completeLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(24f + KeyChipWidth + 12f, 0f),
                new Vector2(240f, KeyChipHeight),
                new Vector2(0f, 0.5f));
        }

        private void StripLegacyKeyGuide()
        {
            var leftover = transform.Find("KeyGuide");
            if (leftover != null)
            {
                DestroyImmediate(leftover.gameObject);
            }
        }

        public static float MeasureKeyChipWidth(string label, float preferredWidth)
        {
            if (string.IsNullOrEmpty(label) || label.Length <= 1)
            {
                return KeyChipWidth;
            }

            return Mathf.Max(KeyChipWidth, preferredWidth + (KeyChipPaddingX * 2f));
        }

        public static Sprite KeyChipSprite
        {
            get
            {
                if (keyChipSprite != null)
                {
                    return keyChipSprite;
                }

                const int size = 64;
                var radius = Mathf.RoundToInt(KeyChipCornerRadius);
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
                keyChipSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));
                keyChipSprite.hideFlags = HideFlags.HideAndDontSave;
                return keyChipSprite;
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

        private static void ApplyKeyChipLook(Image chip)
        {
            if (chip == null)
            {
                return;
            }

            chip.color = KeyChipColor;
            chip.sprite = KeyChipSprite;
            chip.type = Image.Type.Sliced;
            chip.pixelsPerUnitMultiplier = 1f;
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
            float fontSize,
            TMP_FontAsset font = null)
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.font = font != null ? font : HomeUiFonts.Apply();
            text.fontStyle = FontStyles.Normal;
            return text;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
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
