using System;
using System.Collections.Generic;
using Game.Client.Common;
using Game.Core.Home;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Client.Home
{
    [DisallowMultipleComponent]
    public sealed partial class HomeMenuView : MonoBehaviour, IHomeMenuView
    {
        private static readonly Color CharacterColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color AvatarColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color ExperienceBackground = new Color(0.82f, 0.82f, 0.82f, 1f);
        private static readonly Color ExperienceFillColor = new Color(0.31f, 0.62f, 0.91f, 1f);
        private static readonly Color TextHover = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color TextPressed = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color MenuHover = new Color(0.18f, 0.47f, 0.98f, 1f);
        private static readonly Color MenuPressed = new Color(0.10f, 0.32f, 0.78f, 1f);
        private static readonly Color FriendRowColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color FriendStatusColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color FriendSeparatorColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        private static readonly Color PanelShadowColor = new Color(0.1f, 0.1f, 0.1f, 0.38f);
        private static readonly Color ItemShadowColor = new Color(0.12f, 0.12f, 0.12f, 0.32f);
        private static readonly Color SearchBarColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Color SearchButtonColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        private const float HeaderActionSize = 28f;

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildLayout()
        {
            koreanFont = HomeUiFonts.Apply(fontAsset);
            var canvas = CreateCanvas();
            CreateBackground(canvas);
            CreateTitle(canvas);
            CreateLeftMenu(canvas);
            CreateQuitButton(canvas);

            // The panels are built before the controls that open them, and Unity
            // hit-tests later siblings first. A panel's full-screen dismiss area
            // would otherwise swallow the press on the globe or the chip, so
            // pressing one while another panel was open would close that panel
            // and do nothing else.
            CreateFriendListRoot(canvas);
            CreateProfileSettingsRoot(canvas);
            CreateServerSettingsRoot(canvas);
            CreateRoomModalRoot(canvas);

            CreateProfileChip(canvas);
            CreateFriendButton(canvas);
            CreateServerButton(canvas);

            // On a canvas and a scene root of its own, so browsing rooms or
            // opening the closet does not switch the cards off with this
            // screen. See CreateInviteStack.
            CreateInviteStack();

            // Last, so it draws over the panels. It never takes a click, so
            // being on top costs the controls underneath nothing.
            connectionToast = ConnectionToast.AttachTo(canvas);

            // After everything, including the toast. Its scrim has to swallow the
            // clicks meant for the controls behind it, and Unity hit-tests later
            // siblings first - built earlier, the menu would still be pressable
            // underneath the notice (S15P21D205-924).
            BuildSuspendedNotice(canvas);
        }

        /// <summary>
        /// Says that something did not connect. Home has four ways to reach the
        /// network — making a room, finding one, the friend panel, a rename —
        /// and until this existed the first two only wrote to the log.
        /// </summary>
        public void ShowConnectionError(string message)
        {
            connectionToast?.Show(HomeStyle.ConnectionErrorTitle, message);
        }

        private RectTransform CreateCanvas()
        {
            var canvasObject = new GameObject("HomeCanvas", typeof(RectTransform));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasRect;
        }

        /// <summary>
        /// The night street the whole screen sits on, character included: the
        /// art is one image rather than a backdrop with a model in front of it.
        /// </summary>
        /// <remarks>
        /// Sized to the reference resolution and then grown to envelope the
        /// canvas, so a window of any shape is covered and the overflow is cut
        /// off rather than letterboxed. The character stands near the middle,
        /// which is the part that survives every crop.
        /// </remarks>
        private void CreateBackground(RectTransform canvas)
        {
            var rect = CreateRect("Background", canvas);
            SetAnchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = HomeStyle.ReferenceResolution;
            rect.SetAsFirstSibling();

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = backgroundSprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.color = backgroundSprite != null
                ? Color.white
                : HomeStyle.Palette.BackgroundFallback;

            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = backgroundSprite != null && backgroundSprite.rect.height > 0f
                ? backgroundSprite.rect.width / backgroundSprite.rect.height
                : HomeStyle.ReferenceResolution.x / HomeStyle.ReferenceResolution.y;
        }

        /// <summary>
        /// The player's own name, bottom right, over a rounded plate.
        /// </summary>
        /// <remarks>
        /// The plate is sized here rather than by a layout group because the
        /// design fixes what it may measure: it grows with the name between a
        /// floor and a ceiling, and <see cref="ResizeProfileChip"/> is what
        /// applies that every time the name changes.
        /// </remarks>
        private void CreateProfileChip(RectTransform canvas)
        {
            var chip = CreateRect("ProfileChip", canvas);
            SetAnchor(chip, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            chip.anchoredPosition = new Vector2(
                -(HomeStyle.Layout.BottomRightMargin
                    + HomeStyle.Layout.IconButtonSize
                    + HomeStyle.Layout.ChipToFriendButton),
                HomeStyle.Layout.BottomMargin);
            chip.sizeDelta = new Vector2(
                HomeStyle.Layout.ChipMinWidth, HomeStyle.Layout.ChipHeight);
            profileChip = chip;

            var fill = AddImage(
                chip,
                HomeStyle.Palette.ChipFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Chip),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            var stroke = CreateStroke(chip, HomeStyle.Radius.Chip);

            var avatar = CreateRect("Avatar", chip);
            SetAnchor(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            avatar.anchoredPosition = new Vector2(HomeStyle.Layout.ChipLeftPadding, 0f);
            avatar.sizeDelta = new Vector2(
                HomeStyle.Layout.ChipAvatarDiameter, HomeStyle.Layout.ChipAvatarDiameter);
            AddImage(avatar, AvatarColor, HomeUiFonts.CircleSprite);

            var nameRect = CreateRect("Nickname", chip);
            SetAnchor(nameRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            nameRect.anchoredPosition = new Vector2(NicknameLeft, 0f);
            nameRect.sizeDelta = new Vector2(
                HomeStyle.Layout.ChipMaxWidth - NicknameLeft - HomeStyle.Layout.ChipRightPadding,
                HomeStyle.Layout.ChipHeight);
            nicknameText = AddText(
                nameRect,
                "사용자닉네임",
                HomeStyle.FontSize.Nickname,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            ApplyMenuFont(nicknameText);
            nicknameText.color = HomeStyle.Palette.TextPrimary;

            AddButton(chip, fill, stroke, HomeStyle.Palette.ChipFill, HomeMenuAction.ProfileSettings);
            ResizeProfileChip();
        }

        /// <summary>
        /// Where the name starts: past the avatar and the gap after it.
        /// </summary>
        private const float NicknameLeft =
            HomeStyle.Layout.ChipLeftPadding
            + HomeStyle.Layout.ChipAvatarDiameter
            + HomeStyle.Layout.ChipAvatarToName;

        /// <summary>
        /// Fits the chip to the name it is showing, within the two widths the
        /// design allows.
        /// </summary>
        private void ResizeProfileChip()
        {
            if (profileChip == null || nicknameText == null)
            {
                return;
            }

            var width = Mathf.Clamp(
                NicknameLeft + nicknameText.preferredWidth + HomeStyle.Layout.ChipRightPadding,
                HomeStyle.Layout.ChipMinWidth,
                HomeStyle.Layout.ChipMaxWidth);
            profileChip.sizeDelta = new Vector2(width, HomeStyle.Layout.ChipHeight);
        }

        /// <summary>
        /// One of the two square icon buttons: friends at the bottom right,
        /// the server picker at the top right.
        /// </summary>
        private RectTransform CreateIconButton(
            RectTransform canvas,
            string name,
            Sprite icon,
            float iconSize,
            Vector2 anchor,
            Vector2 position,
            HomeMenuAction action)
        {
            var rect = CreateRect(name, canvas);
            SetAnchor(rect, anchor, anchor, anchor);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(
                HomeStyle.Layout.IconButtonSize, HomeStyle.Layout.IconButtonSize);

            var fill = AddImage(
                rect,
                HomeStyle.Palette.ButtonFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.IconButton),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            var stroke = CreateStroke(rect, HomeStyle.Radius.IconButton);

            var glyph = CreateRect("Icon", rect);
            SetAnchor(glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            glyph.anchoredPosition = Vector2.zero;
            glyph.sizeDelta = new Vector2(iconSize, iconSize);

            // The icons are drawn in the text colour already, so they are left
            // white here rather than tinted back to it.
            var glyphImage = AddImage(glyph, Color.white, icon);
            glyphImage.preserveAspect = true;
            glyphImage.enabled = icon != null;

            AddButton(rect, fill, stroke, HomeStyle.Palette.ButtonFill, action);
            return rect;
        }

        /// <summary>
        /// The hairline that appears on hover, drawn over the fill and hidden
        /// until <see cref="HomeHoverHighlight"/> asks for it.
        /// </summary>
        private static Image CreateStroke(RectTransform parent, int radius)
        {
            var rect = CreateRect("Stroke", parent);
            SetAnchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = AddImage(
                rect,
                HomeStyle.Palette.HoverStroke,
                HomeUiFonts.Outline(radius, HomeStyle.HoverStrokeThickness));
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.enabled = false;
            return image;
        }

        private void AddButton(
            RectTransform rect, Image fill, Image stroke, Color normal, HomeMenuAction action)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ActionClicked?.Invoke(action));
            menuButtons.Add(button);

            var highlight = rect.gameObject.AddComponent<HomeHoverHighlight>();
            highlight.Bind(fill, stroke, normal, HomeStyle.Palette.HoverFill);

            // Kept so the panels can point back at the button that opens them.
            // Rebuilding the layout re-registers over the same key.
            actionHighlights[action] = highlight;
        }

        /// <summary>
        /// The two-line game title above the home menu.
        /// </summary>
        private void CreateTitle(RectTransform canvas)
        {
            if (titleFont == null)
            {
                Debug.LogWarning("Home title font is not assigned.", this);
                return;
            }

            var rect = CreateRect("Title", canvas);
            SetAnchor(rect, Vector2.up, Vector2.up, Vector2.up);
            rect.anchoredPosition = new Vector2(HomeStyle.Layout.TitleLeft, -HomeStyle.Layout.TitleTop);
            rect.sizeDelta = new Vector2(HomeStyle.Layout.TitleWidth, HomeStyle.Layout.TitleHeight);

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = titleFont;
            text.fontSharedMaterial = titleFont.material;
            text.text = "Don't Touch\nMine";
            text.fontSize = HomeStyle.Layout.TitleFontSize;
            text.fontStyle = FontStyles.Italic;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = new Color32(255, 112, 50, 255);
            text.enableAutoSizing = false;
            text.lineSpacing = 0f;
            text.characterSpacing = 0f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        /// <summary>
        /// The four ways out of Home, stacked down the left of the character.
        /// </summary>
        /// <remarks>
        /// Placed one by one off the top-left corner rather than through a
        /// layout group. The design gives the first line's top and the gap
        /// between lines, which is what <see cref="HomeStyle.Layout.MenuPitch"/>
        /// adds up, and a layout group would re-derive that from whatever
        /// heights the labels happened to measure.
        /// </remarks>
        private void CreateLeftMenu(RectTransform canvas)
        {
            var menu = CreateRect("Menu", canvas);
            SetAnchor(menu, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            menu.anchoredPosition = new Vector2(
                HomeStyle.Layout.MenuLeft, -HomeStyle.Layout.MenuTop);
            menu.sizeDelta = Vector2.zero;

            CreateMenuItem(menu, "방 만들기", HomeMenuAction.CreateRoom, 0);
            CreateMenuItem(menu, "게임 찾기", HomeMenuAction.FindRoom, 1);
            CreateMenuItem(menu, "캐릭터", HomeMenuAction.Character, 2);
            CreateMenuItem(menu, "환경 설정", HomeMenuAction.Settings, 3);
        }

        /// <summary>
        /// One menu line, lit in the accent colour while the pointer is on it.
        /// </summary>
        /// <remarks>
        /// The label carries the click rather than a panel behind it, and the
        /// rect is fitted to the glyphs, so the line answers where it is legible
        /// and the empty stretch beside it stays part of the picture.
        /// <para>
        /// The tint multiplies the graphic's own colour, so the text is left
        /// white and the palette lives entirely in the colour block. Selected
        /// matches normal: a line that was clicked and then returned to should
        /// not stay lit while the pointer is elsewhere.
        /// </para>
        /// </remarks>
        private void CreateMenuItem(
            RectTransform parent, string label, HomeMenuAction action, int index)
        {
            var rect = CreateRect(action.ToString(), parent);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(0f, -index * HomeStyle.Layout.MenuPitch);
            rect.sizeDelta = new Vector2(0f, HomeStyle.Layout.MenuLineHeight);

            var text = AddText(
                rect,
                label,
                HomeStyle.FontSize.Menu,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                raycastTarget: true);
            ApplyMenuFont(text);
            text.color = Color.white;

            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            AddLabelButton(rect, text, action);
        }

        /// <summary>
        /// Makes a label clickable, lit in the accent colour and slightly grown
        /// while hovered.
        /// </summary>
        /// <remarks>
        /// The tint multiplies the graphic's own colour, so callers leave the
        /// text white and the palette lives entirely in the colour block.
        /// Selected matches normal: a label that was clicked and returned to
        /// should not stay lit while the pointer is elsewhere.
        /// <para>
        /// The growth is <see cref="HomeLabelPop"/>'s, on the same eighth of a
        /// second as the tint. Every label built here is pivoted at its leading
        /// edge, so each grows away from the edge it is aligned to and the
        /// column stays put.
        /// </para>
        /// </remarks>
        private void AddLabelButton(RectTransform rect, TMP_Text text, HomeMenuAction action)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;

            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = HomeStyle.Palette.TextPrimary;
            colors.highlightedColor = HomeStyle.Palette.Accent;
            colors.pressedColor = HomeStyle.Palette.Accent;
            colors.selectedColor = HomeStyle.Palette.TextPrimary;
            colors.disabledColor = HomeStyle.Palette.TextPrimary * 0.5f;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            button.onClick.AddListener(() => ActionClicked?.Invoke(action));
            menuButtons.Add(button);

            rect.gameObject.AddComponent<HomeLabelPop>()
                .Bind(rect, HomeStyle.Layout.MenuHoverScale, HomeStyle.Layout.MenuHoverSeconds);
        }

        /// <summary>
        /// Swaps a label onto the SemiBold face, material included: a TMP text
        /// keeps the material of the font it was built with, and leaving the two
        /// apart draws the new glyphs through the old atlas.
        /// </summary>
        private void ApplyMenuFont(TMP_Text text)
        {
            if (semiBoldFont == null)
            {
                return;
            }

            text.font = semiBoldFont;
            text.fontSharedMaterial = semiBoldFont.material;
        }

        /// <summary>
        /// The way out of the game, bottom left. A plain label like the menu
        /// rather than a plate like the chip beside it.
        /// </summary>
        private void CreateQuitButton(RectTransform canvas)
        {
            var quit = CreateRect("QuitButton", canvas);
            SetAnchor(quit, Vector2.zero, Vector2.zero, Vector2.zero);
            quit.anchoredPosition = new Vector2(
                HomeStyle.Layout.QuitLeft, HomeStyle.Layout.QuitBottom);
            quit.sizeDelta = new Vector2(0f, HomeStyle.FontSize.Quit * 1.2f);

            var text = AddText(
                quit,
                "게임 종료",
                HomeStyle.FontSize.Quit,
                FontStyles.Normal,
                TextAlignmentOptions.BottomLeft,
                raycastTarget: true);
            ApplyMenuFont(text);
            text.color = Color.white;

            var fitter = quit.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            AddLabelButton(quit, text, HomeMenuAction.Quit);
        }

        private void CreateFriendButton(RectTransform canvas)
        {
            var button = CreateIconButton(
                canvas,
                "FriendButton",
                friendIcon,
                HomeStyle.Layout.FriendIconSize,
                new Vector2(1f, 0f),
                new Vector2(-HomeStyle.Layout.BottomRightMargin, HomeStyle.Layout.BottomMargin),
                HomeMenuAction.Friends);

            var badge = CreateRect("RequestBadge", button);
            SetAnchor(badge, Vector2.one, Vector2.one, new Vector2(0.5f, 0.5f));
            badge.anchoredPosition = new Vector2(-2f, -2f);
            badge.sizeDelta = Vector2.one * HomeStyle.Friends.BadgeDiameter;
            AddImage(badge, HomeStyle.Palette.BadgeFill, HomeUiFonts.CircleSprite);

            var label = CreateRect("Count", badge);
            SetAnchor(label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            friendButtonBadgeText = AddText(
                label, string.Empty, HomeStyle.FontSize.Badge,
                FontStyles.Normal, TextAlignmentOptions.Center);
            ApplyMenuFont(friendButtonBadgeText);
            friendButtonBadgeText.color = HomeStyle.Palette.BadgeLabel;
            friendButtonBadge = badge.gameObject;
            friendButtonBadge.SetActive(false);
        }

        private void CreateServerButton(RectTransform canvas)
        {
            CreateIconButton(
                canvas,
                "ServerButton",
                serverIcon,
                HomeStyle.Layout.ServerIconSize,
                new Vector2(1f, 1f),
                new Vector2(
                    -HomeStyle.Layout.ServerRightMargin, -HomeStyle.Layout.ServerTopMargin),
                HomeMenuAction.ServerSettings);
        }

        private static void ClearButtons(List<Button> buttons)
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                if (buttons[index] != null)
                {
                    buttons[index].onClick.RemoveAllListeners();
                }
            }
        }

        private TMP_Text AddText(
            RectTransform target,
            string content,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            bool raycastTarget = false)
        {
            if (koreanFont == null)
            {
                throw new InvalidOperationException("Paperlogy TMP font is missing.");
            }

            target.gameObject.SetActive(false);
            var tmp = target.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = koreanFont;
            tmp.fontSharedMaterial = koreanFont.material;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = Color.black;
            tmp.raycastTarget = raycastTarget;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            target.gameObject.SetActive(true);
            return tmp;
        }

        private static Image AddImage(
            RectTransform rect,
            Color color,
            Sprite sprite = null,
            bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : HomeUiFonts.WhiteSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.layer = LayerMask.NameToLayer("UI");
            var rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetAnchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }
    }
}
