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
        public const float VisibleSeconds = 3f;
        public const float MessageFontSize = 55f;
        public const float HintFontSize = 55f;
        public const string HintText =
            "다른 도둑들에게 빼앗기지 않도록 비밀 장소에 잘 챙겨두세요.";

        private const string FallbackItemName = "물건";
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

        [SerializeField]
        [Tooltip("Preview the briefing in the editor. Match start wiring keeps this off.")]
        private bool previewOnAwake;

        [SerializeField]
        private string previewItemName = "탄산음료";

        public static string FormatMessage(string itemDisplayName)
        {
            return $"당신이 훔친 물건은 {ResolveName(itemDisplayName)}입니다.";
        }

        public static string FormatRichMessage(string itemDisplayName)
        {
            return $"당신이 훔친 물건은 <color={ItemNameColor}>{ResolveName(itemDisplayName)}</color>입니다.";
        }

        public static HidingIntroView Create(Transform parent)
        {
            var rootObject = new GameObject("HidingIntro", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<HidingIntroView>();
        }

        private static string ResolveName(string itemDisplayName)
        {
            return string.IsNullOrWhiteSpace(itemDisplayName)
                ? FallbackItemName
                : itemDisplayName.Trim();
        }

        private void Awake()
        {
            EnsureLayout();
            if (previewOnAwake)
            {
                Show(previewItemName);
                return;
            }

            Hide();
        }

        public void Show(string itemDisplayName, string itemId = null)
        {
            EnsureLayout();
            transform.SetAsLastSibling();

            var name = ResolveName(itemDisplayName);
            var font = ResolveFont();
            if (messageText != null)
            {
                messageText.font = font;
                messageText.fontSize = MessageFontSize;
                messageText.fontStyle = FontStyles.Normal;
                messageText.text = FormatRichMessage(name);
            }

            if (hintText != null)
            {
                hintText.font = font;
                hintText.fontSize = HintFontSize;
                hintText.fontStyle = FontStyles.Normal;
                hintText.text = HintText;
            }

            preview?.Show(itemId);

            if (root != null)
            {
                root.SetActive(true);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            preview?.Clear();
            if (root != null && root != gameObject)
            {
                root.SetActive(false);
                return;
            }

            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            preview?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            preview?.Dispose();
            preview = null;
        }

        private void EnsureLayout()
        {
            DestroyLegacyModal();

            if (root == null)
            {
                root = gameObject;
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

            if (itemPreview == null)
            {
                itemPreview = transform.Find("Content/ItemPreview")?.GetComponent<RawImage>();
            }

            if (preview == null && itemPreview != null)
            {
                preview = new HidingIntroItemPreview(itemPreview);
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
            Place(content, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1200f, 640f));

            itemPreview = CreateRawImage(content, "ItemPreview");
            Place(itemPreview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(360f, 360f));
            preview = new HidingIntroItemPreview(itemPreview);

            messageText = CreateText(
                content,
                "Message",
                FormatRichMessage(previewItemName),
                MessageFontSize,
                TextAlignmentOptions.Center);
            Place(
                messageText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -80f),
                new Vector2(1400f, 80f));

            hintText = CreateText(
                content,
                "Hint",
                HintText,
                HintFontSize,
                TextAlignmentOptions.Center);
            hintText.color = new Color(1f, 1f, 1f, 0.92f);
            Place(
                hintText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -170f),
                new Vector2(1400f, 80f));
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

        private static RawImage CreateRawImage(Transform parent, string name)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
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
            text.enableWordWrapping = true;
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

            var source = Resources.Load<Font>(SemiBoldResource);
            if (source != null)
            {
                paperlogySemiBold = TMP_FontAsset.CreateFontAsset(source);
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
