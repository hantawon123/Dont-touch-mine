using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Common
{
    /// <summary>
    /// The notice that says something did not connect, and goes away by itself.
    /// </summary>
    /// <remarks>
    /// Shared because the same failures reach the player from two screens. Room
    /// entry is refused on the browser; making a room and finding a game are
    /// refused on Home, which used to have nowhere to put a failure and logged
    /// them instead — a button that appears to do nothing.
    /// <para>
    /// It builds itself rather than coming from a prefab, so a screen adopts it
    /// with one <c>AddComponent</c> and there is no asset to keep in step with
    /// two scenes.
    /// </para>
    /// <para>
    /// A success uses the same plate with the hue rotated to green, and a check
    /// to the left of the title, so thanks is not drawn in the same warmth as a
    /// refusal.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ConnectionToast : MonoBehaviour
    {
        public const string CheckIconName = "CheckIcon";
        public const string CheckIconResource = "UI/Icon_Check";

        public static class Style
        {
            public static readonly Color Base = new Color(0f, 0f, 0f, 0.97f);
            public static readonly Color Tint = new Color(1f, 0.604f, 0.416f, 0.2f);
            public static readonly Color Title = new Color(1f, 0.44f, 0.196f, 1f);

            /// <summary>
            /// The failure colours with the channels rotated toward green:
            /// same weight as the warm refusal, a success rather than a warning.
            /// </summary>
            public static readonly Color SuccessTint = new Color(0.416f, 1f, 0.604f, 0.2f);
            public static readonly Color SuccessTitle = new Color(0.196f, 1f, 0.44f, 1f);

            public static readonly Color Body = Color.white;

            public const int Radius = 20;
            public static readonly Vector2 Size = new Vector2(590f, 136f);
            public const float TopMargin = 48f;
            public const float TitleOffsetY = 26f;
            public const float BodyOffsetY = -22f;
            public const float TitleSize = 30f;
            public const float BodySize = 20f;
            public const float CheckIconSize = 40f;
            public const float CheckIconGap = 10f;

            /// <summary>
            /// How long it stays up. Long enough to read a line, short enough
            /// that it is gone before the player tries again.
            /// </summary>
            public const float Seconds = 3f;
        }

        private RectTransform root;
        private Image tint;
        private Image checkIcon;
        private TMP_Text titleText;
        private TMP_Text bodyText;
        private float hidesAt;

        /// <summary>
        /// Puts a toast on a screen. Call once, from the screen that owns it.
        /// </summary>
        /// <param name="parent">
        /// The rect it hangs from, normally the screen's full-screen root. It
        /// pins itself to the top centre of that rect.
        /// </param>
        public static ConnectionToast AttachTo(RectTransform parent)
        {
            var toast = parent.gameObject.AddComponent<ConnectionToast>();
            toast.Build(parent);
            return toast;
        }

        /// <summary>
        /// Shows the notice, and restarts its seconds if one is already up: the
        /// newest line is the one the player just caused.
        /// </summary>
        public void Show(string title, string body, bool success = false)
        {
            if (root == null)
            {
                return;
            }

            titleText.text = title ?? string.Empty;
            bodyText.text = body ?? string.Empty;
            ApplyTone(success);
            hidesAt = Time.unscaledTime + Style.Seconds;
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        /// <summary>Whether the notice is on screen. For tests.</summary>
        public bool IsShowing => root != null && root.gameObject.activeSelf;

        public string Body => bodyText == null ? string.Empty : bodyText.text;

        public static Sprite LoadCheckIcon()
        {
            var sprite = Resources.Load<Sprite>(CheckIconResource);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = Resources.Load<Texture2D>(CheckIconResource);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void Update()
        {
            // Nothing wakes this when the wait runs out, so it is checked.
            if (root != null && root.gameObject.activeSelf && Time.unscaledTime >= hidesAt)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void Build(RectTransform parent)
        {
            var panel = CreateImage("ConnectionToast", parent, Style.Base);
            root = panel.rectTransform;
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -Style.TopMargin);
            root.sizeDelta = Style.Size;

            // The warm cast over the base and under the text: the mock-up stacks
            // these two fills rather than blending them into one.
            tint = CreateImage("Tint", root, Style.Tint);
            tint.rectTransform.anchorMin = Vector2.zero;
            tint.rectTransform.anchorMax = Vector2.one;
            tint.rectTransform.offsetMin = Vector2.zero;
            tint.rectTransform.offsetMax = Vector2.zero;

            checkIcon = CreateCheckIcon(root);
            titleText = CreateText(
                "Title", root, Style.TitleSize, Style.Title, Style.TitleOffsetY);
            bodyText = CreateText(
                "Body", root, Style.BodySize, Style.Body, Style.BodyOffsetY);

            // It reports; the player carries on behind it.
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            root.gameObject.SetActive(false);
        }

        private void ApplyTone(bool success)
        {
            if (tint != null)
            {
                tint.color = success ? Style.SuccessTint : Style.Tint;
            }

            if (titleText != null)
            {
                titleText.color = success ? Style.SuccessTitle : Style.Title;
            }

            LayoutTitle(success);
        }

        /// <summary>
        /// A success sits the check to the left of the title and centres the
        /// two as one line. A refusal keeps the title alone in the middle.
        /// </summary>
        private void LayoutTitle(bool success)
        {
            if (titleText == null)
            {
                return;
            }

            var titleRect = titleText.rectTransform;
            if (!success || checkIcon == null)
            {
                if (checkIcon != null)
                {
                    checkIcon.gameObject.SetActive(false);
                }

                titleRect.anchoredPosition = new Vector2(0f, Style.TitleOffsetY);
                titleRect.sizeDelta = new Vector2(Style.Size.x, 40f);
                return;
            }

            checkIcon.gameObject.SetActive(true);
            titleText.ForceMeshUpdate();
            var titleWidth = Mathf.Max(titleText.preferredWidth, 1f);
            var cluster = Style.CheckIconSize + Style.CheckIconGap + titleWidth;
            var left = -cluster * 0.5f;
            checkIcon.rectTransform.anchoredPosition = new Vector2(
                left + Style.CheckIconSize * 0.5f,
                Style.TitleOffsetY);
            titleRect.anchoredPosition = new Vector2(
                left + Style.CheckIconSize + Style.CheckIconGap + titleWidth * 0.5f,
                Style.TitleOffsetY);
            titleRect.sizeDelta = new Vector2(titleWidth + 4f, 40f);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform))
                .GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = HomeUiFonts.Rounded(Style.Radius);
            image.type = Image.Type.Sliced;

            // The sprite is generated at one pixel per unit of radius, so the
            // slice must not be rescaled by the canvas reference PPU.
            image.pixelsPerUnitMultiplier = 1f;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateCheckIcon(Transform parent)
        {
            var rect = new GameObject(CheckIconName, typeof(RectTransform))
                .GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Style.CheckIconSize, Style.CheckIconSize);
            rect.anchoredPosition = new Vector2(0f, Style.TitleOffsetY);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadCheckIcon();
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return image;
        }

        private static TMP_Text CreateText(
            string name, Transform parent, float size, Color color, float offsetY)
        {
            var rect = new GameObject(name, typeof(RectTransform))
                .GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, offsetY);
            rect.sizeDelta = new Vector2(Style.Size.x, 40f);

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = HomeUiFonts.Apply();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
