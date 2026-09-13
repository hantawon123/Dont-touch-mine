using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface ISearchingIntroView
    {
        void Show(string itemDisplayName);
        void Hide();
    }

    /// <summary>
    /// Full-screen searching briefing: the assigned item and the same three
    /// lines for every player. Timing belongs to the presenter; this view only paints.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SearchingIntroView : MonoBehaviour, ISearchingIntroView
    {
        public const float VisibleSeconds = Game.Core.Match.MatchIntroTiming.VisibleSeconds;
        public const float FontSize = 55f;
        public const string TitleText = "숨기기 시간이 끝났습니다.";
        public const string BodyText =
            "이제부터 서로의 물건을 노리는 진짜 탐색전이 시작됩니다.";

        private const string FallbackItemName = "물건";
        private const string ItemNameColor = "#F4A26B";
        private const string SemiBoldResource = "Fonts/Paperlogy-6SemiBold";

        private static TMP_FontAsset paperlogySemiBold;

        [SerializeField]
        private GameObject root;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text bodyText;

        [SerializeField]
        private TMP_Text hintText;

        private bool shown;
        private int shownAtFrame;
        public bool IsPresented => shown && isActiveAndEnabled && Time.frameCount > shownAtFrame + 1;

        [SerializeField]
        [Tooltip("Preview the briefing in the editor. Match start wiring keeps this off.")]
        private bool previewOnAwake;

        [SerializeField]
        private string previewItemName = "사과";

        public static string FormatHint(string itemDisplayName)
        {
            var name = ResolveName(itemDisplayName);
            return $"마지막 순간에 {name}{ObjectParticle(name)} 꼭 손에 쥐고 계세요!";
        }

        public static string FormatRichHint(string itemDisplayName)
        {
            var name = ResolveName(itemDisplayName);
            return $"마지막 순간에 <color={ItemNameColor}>{name}</color>{ObjectParticle(name)} 꼭 손에 쥐고 계세요!";
        }

        public static SearchingIntroView Create(Transform parent)
        {
            var rootObject = new GameObject("SearchingIntro", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<SearchingIntroView>();
        }

        private static string ResolveName(string itemDisplayName)
        {
            return string.IsNullOrWhiteSpace(itemDisplayName)
                ? FallbackItemName
                : itemDisplayName.Trim();
        }

        private static string ObjectParticle(string itemDisplayName)
        {
            var name = ResolveName(itemDisplayName);
            var last = name[name.Length - 1];
            if (last < '\uAC00' || last > '\uD7A3')
            {
                return "를";
            }

            return (last - '\uAC00') % 28 == 0 ? "를" : "을";
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

        public void Show(string itemDisplayName)
        {
            shown = true;
            shownAtFrame = Time.frameCount;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            transform.SetAsLastSibling();

            var name = ResolveName(itemDisplayName);
            var font = ResolveFont();
            ApplyText(titleText, font, TitleText);
            ApplyText(bodyText, font, BodyText);
            ApplyText(hintText, font, FormatRichHint(name));
            SetVisualsVisible(true);
        }

        public void Hide()
        {
            shown = false;
            SetVisualsVisible(false);
        }

        private void EnsureLayout()
        {
            DestroyChild("Content/ItemPreview");
            DestroyChild("ItemPreview");

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

            if (titleText == null)
            {
                titleText = transform.Find("Content/Title")?.GetComponent<TMP_Text>();
            }

            if (bodyText == null)
            {
                bodyText = transform.Find("Content/Body")?.GetComponent<TMP_Text>();
            }

            if (hintText == null)
            {
                hintText = transform.Find("Content/Hint")?.GetComponent<TMP_Text>();
            }
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
            Place(content, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1800f, 720f));

            titleText = CreateText(content, "Title", TitleText, FontSize, TextAlignmentOptions.Center);
            Place(
                titleText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 90f),
                new Vector2(1800f, 80f));

            bodyText = CreateText(content, "Body", BodyText, FontSize, TextAlignmentOptions.Center);
            Place(
                bodyText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(1800f, 80f));

            hintText = CreateText(
                content,
                "Hint",
                FormatRichHint(previewItemName),
                FontSize,
                TextAlignmentOptions.Center);
            Place(
                hintText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -90f),
                new Vector2(1800f, 80f));
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

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static void ApplyText(TMP_Text text, TMP_FontAsset font, string content)
        {
            if (text == null)
            {
                return;
            }

            text.font = font;
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Normal;
            text.text = content;
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
