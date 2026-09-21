using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IHidingIntroView
    {
        void Show(string itemDisplayName, string itemId = null);
        void Hide();
    }

    /// <summary>
    /// Full-screen hiding briefing: the assigned item and a one-line notice.
    /// Timing and when to show it belong to the presenter; this view only paints.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingIntroView : MonoBehaviour, IHidingIntroView
    {
        public const float VisibleSeconds = Game.Core.Match.MatchIntroTiming.VisibleSeconds;
        public const float MessageFontSize = 55f;
        public const float HintFontSize = 55f;
        public const float ContentAnchoredY = -174f;
        public const float MessageAnchoredY = 36f;
        public const float HintAnchoredY = -60f;
        public static string HintText =>
            UiTextCatalog.Shipped.Get(UiText.Match.IntroHint, "ko");

        private static string FallbackItemName(string language) =>
            UiTextCatalog.Shipped.Get(UiText.Match.ItemFallback, language);
        private const string ItemNameColor = "#F4A26B";
        private const string SemiBoldResource = "Fonts/Paperlogy-6SemiBold";

        private static TMP_FontAsset paperlogySemiBold;

        [SerializeField]
        private GameObject root;

        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private TMP_Text hintText;

        [SerializeField]
        private RawImage itemPreview;

        private HidingIntroItemPreview preview;
        private bool shown;
        private int shownAtFrame;
        private UiLocale chromeLocale;
        private string lastItemName;
        private string lastItemId;

        public void ShowChrome(UiLocale locale)
        {
            chromeLocale = locale;
            if (shown)
            {
                Show(lastItemName, lastItemId);
            }
        }

        private string Language =>
            chromeLocale != null ? chromeLocale.LanguageCode : UiLocale.AppliedLanguage;

        private string Copy(string key) =>
            chromeLocale != null
                ? chromeLocale.Get(key)
                : UiLocale.Applied(key);
        public bool IsPresented => shown && isActiveAndEnabled && Time.frameCount > shownAtFrame + 1;

        [SerializeField]
        [Tooltip("Preview the briefing in the editor. Match start wiring keeps this off.")]
        private bool previewOnAwake;

        [SerializeField]
        private string previewItemName = "탄산음료";

        public static string FormatMessage(string itemDisplayName) =>
            FormatMessage(itemDisplayName, "ko");

        public static string FormatMessage(string itemDisplayName, string language)
        {
            return string.Format(
                UiTextCatalog.Shipped.Get(UiText.Match.StolenItem, language),
                ResolveName(itemDisplayName, language));
        }

        public static string FormatRichMessage(string itemDisplayName) =>
            FormatRichMessage(itemDisplayName, "ko");

        public static string FormatRichMessage(string itemDisplayName, string language)
        {
            var name = ResolveName(itemDisplayName, language);
            return string.Format(
                UiTextCatalog.Shipped.Get(UiText.Match.StolenItem, language),
                $"<color={ItemNameColor}>{name}</color>");
        }

        public static HidingIntroView Create(Transform parent)
        {
            var rootObject = new GameObject("HidingIntro", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<HidingIntroView>();
        }

        private static string ResolveName(string itemDisplayName, string language = "ko")
        {
            return string.IsNullOrWhiteSpace(itemDisplayName)
                ? FallbackItemName(language)
                : ItemNameText.Localized(itemDisplayName, language);
        }

        private void Awake()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show(previewItemName);
                return;
            }

            if (!shown)
            {
                SetVisualsVisible(false);
            }
        }

        public void Show(string itemDisplayName, string itemId = null)
        {
            shown = true;
            shownAtFrame = Time.frameCount;
            lastItemName = itemDisplayName;
            lastItemId = itemId;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            transform.SetAsLastSibling();

            var name = ResolveName(itemDisplayName, Language);
            var font = ResolveFont();
            if (messageText != null)
            {
                messageText.font = font;
                messageText.fontSize = MessageFontSize;
                messageText.fontStyle = FontStyles.Normal;
                messageText.text = FormatRichMessage(name, Language);
            }

            if (hintText != null)
            {
                hintText.font = font;
                hintText.fontSize = HintFontSize;
                hintText.fontStyle = FontStyles.Normal;
                hintText.text = Copy(UiText.Match.IntroHint);
            }

            preview?.Show(itemId);
            SetVisualsVisible(true);
        }

        public void Hide()
        {
            shown = false;
            preview?.Clear();
            SetVisualsVisible(false);
        }

        private void OnDestroy()
        {
            preview?.Dispose();
            preview = null;
        }

        private void EnsureLayout()
        {
            DestroyLegacyModal();
            DestroyChild("Content/ItemPreview");

            if (root == null)
            {
                root = transform.Find("Background")?.gameObject ?? gameObject;
            }

            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            if (transform.Find("Background") == null)
            {
                BuildLayout();
            }

            if (messageText == null)
            {
                messageText = transform.Find("Content/Message")?.GetComponent<TMP_Text>();
            }

            if (hintText == null)
            {
                hintText = transform.Find("Content/Hint")?.GetComponent<TMP_Text>();
            }

            itemPreview = HidingIntroItemPreview.EnsureIntroSlot(transform);
            if (preview == null && itemPreview != null)
            {
                preview = new HidingIntroItemPreview(
                    itemPreview,
                    HidingIntroItemPreview.IntroTextureSize,
                    rotates: true);
            }

            ApplyCenteredPlacement();
        }

        private void ApplyCenteredPlacement()
        {
            var content = transform.Find("Content") as RectTransform;
            if (content != null)
            {
                Place(
                    content,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, ContentAnchoredY),
                    new Vector2(1200f, 640f));
            }

            if (messageText != null)
            {
                Place(
                    messageText.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, MessageAnchoredY),
                    new Vector2(1400f, 80f));
            }

            if (hintText != null)
            {
                Place(
                    hintText.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, HintAnchoredY),
                    new Vector2(1400f, 80f));
            }
        }

        private void DestroyLegacyModal()
        {
            DestroyChild("Dimmer");
            DestroyChild("Card");
        }

        private void DestroyChild(string childName)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
                return;
            }

            DestroyImmediate(child.gameObject);
        }

        private void BuildLayout()
        {
            EnsureOverlayCanvas();

            var background = CreatePanel(transform, "Background", Color.black, true);
            Stretch(background);

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);
            Place(
                content,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, ContentAnchoredY),
                new Vector2(1200f, 640f));

            messageText = CreateText(
                content,
                "Message",
                FormatRichMessage(previewItemName, Language),
                MessageFontSize,
                TextAlignmentOptions.Center);
            Place(
                messageText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, MessageAnchoredY),
                new Vector2(1400f, 80f));

            hintText = CreateText(
                content,
                "Hint",
                Copy(UiText.Match.IntroHint),
                HintFontSize,
                TextAlignmentOptions.Center);
            hintText.color = new Color(1f, 1f, 1f, 0.92f);
            Place(
                hintText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, HintAnchoredY),
                new Vector2(1400f, 80f));
        }

        private void SetVisualsVisible(bool visible)
        {
            var background = transform.Find("Background");
            if (background != null)
            {
                background.gameObject.SetActive(visible);
            }

            var content = transform.Find("Content");
            if (content != null)
            {
                content.gameObject.SetActive(visible);
            }

            if (itemPreview != null)
            {
                itemPreview.gameObject.SetActive(visible);
            }
        }

        private void EnsureOverlayCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 250;
            Game.Client.Common.HudScreenScale.Ensure(gameObject);

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static RectTransform CreatePanel(
            Transform parent,
            string name,
            Color color,
            bool raycastTarget)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return gameObject.GetComponent<RectTransform>();
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            TextAlignmentOptions alignment)
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
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.font = ResolveFont();
            text.fontStyle = FontStyles.Normal;
            return text;
        }

        private static TMP_FontAsset ResolveFont()
        {
            if (paperlogySemiBold != null)
            {
                return paperlogySemiBold;
            }

            paperlogySemiBold = HomeUiFonts.Apply();
            if (paperlogySemiBold != null)
            {
                return paperlogySemiBold;
            }

            var source = Resources.Load<Font>(SemiBoldResource);
            if (source != null)
            {
                paperlogySemiBold = HomeUiFonts.CreateRuntimeKorean(source);
            }

            if (paperlogySemiBold == null)
            {
                paperlogySemiBold = TMP_Settings.defaultFontAsset;
            }

            return paperlogySemiBold;
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
