using System;
using System.Collections.Generic;
using Game.Client.Common;
using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// The settings screen: the tabs down the left, the rows beside them, the
    /// two buttons underneath, all on one panel over the Home picture.
    /// </summary>
    /// <remarks>
    /// Built in code like the rest of the screens, so
    /// <see cref="SettingsStyle"/> is the only record of the design. One
    /// canvas: there is nothing in the world to keep in front of, so the
    /// picture is the first thing on it, exactly as Home does it.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed partial class SettingsView : MonoBehaviour, ISettingsView
    {
        private static readonly SettingsTab[] TabOrder =
        {
            SettingsTab.General,
            SettingsTab.Graphics,
            SettingsTab.Interface,
            SettingsTab.Sound,
            SettingsTab.Controls,
            SettingsTab.Notifications
        };

        [Header("Art")]
        [SerializeField]
        private Sprite backgroundSprite;

        [SerializeField]
        [Tooltip("The circling arrow beside 초기화 and 전체설정 초기화. Optional.")]
        private Sprite refreshIcon;

        [SerializeField]
        [Tooltip("The language picker's left arrow. Optional.")]
        private Sprite leftIcon;

        [SerializeField]
        [Tooltip("The language picker's right arrow. Optional.")]
        private Sprite rightIcon;

        [SerializeField]
        [Tooltip("The X that closes a confirmation. Optional.")]
        private Sprite closeIcon;

        [Header("Fonts")]
        [SerializeField]
        [Tooltip("SemiBold, for the arrow, the tabs and the confirmations.")]
        private TMP_FontAsset fontAsset;

        [SerializeField]
        [Tooltip("Regular, for the rows and the line at the top right. Falls " +
                 "back to the Resources copy when it is not assigned.")]
        private TMP_FontAsset regularFontAsset;

        [SerializeField]
        [Tooltip("Medium, for the two buttons under the panel. Falls back to " +
                 "the font above when it is not assigned.")]
        private TMP_FontAsset buttonFontAsset;

        private TMP_FontAsset font;
        private TMP_FontAsset regularFont;
        private TMP_FontAsset buttonFont;
        private RectTransform canvasRoot;
        private RectTransform panel;
        private ConnectionToast toast;
        private readonly List<Button> buttons = new List<Button>();

        private readonly SettingsTabHover[] tabHovers = new SettingsTabHover[TabOrder.Length];

        private Button resetButton;
        private Button applyButton;
        private Image resetFill;
        private TMP_Text resetLabel;
        private Image resetIconImage;
        private Image applyFill;
        private TMP_Text applyLabel;
        private GameObject feedbackRow;
        private RectTransform leaveGameButton;
        private bool lobbyOverlay;

        public event Action Opened;
        private void OnEnable() => Opened?.Invoke();

        public event Action BackRequested;
        public event Action Closed;

        public void RequestBack() => BackRequested?.Invoke();
        private void OnDisable() => Closed?.Invoke();

        public event Action ResetAllRequested;

        public event Action<SettingsTab> TabSelected;

        public event Action<int> LanguageStepRequested;

        public event Action<GraphicsOption, int> GraphicsStepRequested;

        public event Action<InterfaceOption, int> InterfaceStepRequested;

        public event Action<NotificationOption, int> NotificationStepRequested;

        // The sound page's own events live beside its builders, in
        // SettingsView.Sound.cs.

        public event Action FeedbackRequested;

        public event Action ResetRequested;

        public event Action ApplyRequested;

        public event Action LeaveGameRequested;

        /// <summary>
        /// Lobby overlay: no feedback row, no ← 이전, and 게임 나가기 as
        /// white text where that arrow sat. Call before the first activation
        /// when this view is built in code rather than placed in the Settings
        /// scene.
        /// </summary>
        public void ConfigureAsLobbyOverlay()
        {
            lobbyOverlay = true;
            if (canvasRoot != null)
            {
                ApplyLobbyChrome();
            }
        }

        public void ShowTab(SettingsTab tab)
        {
            for (var index = 0; index < TabOrder.Length; index++)
            {
                if (tabHovers[index] != null)
                {
                    tabHovers[index].SetSelected(TabOrder[index] == tab);
                }
            }

            ShowPage(tab);
        }

        /// <summary>
        /// Paints and arms the two buttons for whether there is anything to
        /// apply. Both the colour and the interactable flag, because the off
        /// state has to look unavailable and be unavailable: a plate that is
        /// merely grey still takes the click.
        /// </summary>
        public void SetActionsEnabled(bool enabled)
        {
            if (resetButton != null)
            {
                resetButton.interactable = enabled;
            }

            if (applyButton != null)
            {
                applyButton.interactable = enabled;
            }

            Paint(resetFill, enabled, SettingsStyle.Palette.ResetOnFill, SettingsStyle.Palette.ButtonOffFill);
            Paint(resetLabel, enabled, SettingsStyle.Palette.ResetOnLabel, SettingsStyle.Palette.ButtonOffLabel);
            Paint(resetIconImage, enabled, SettingsStyle.Palette.ResetOnLabel, SettingsStyle.Palette.ButtonOffLabel);
            Paint(applyFill, enabled, SettingsStyle.Palette.ApplyOnFill, SettingsStyle.Palette.ButtonOffFill);
            Paint(applyLabel, enabled, SettingsStyle.Palette.ApplyOnLabel, SettingsStyle.Palette.ButtonOffLabel);
        }

        public void ShowNotice(string title, string message)
        {
            toast?.Show(title, message);
        }

        private void Awake()
        {
            EnsureEventSystem();
            if (canvasRoot == null)
            {
                BuildLayout();
            }
        }

        private void OnDestroy()
        {
            ReleaseBackdrop();

            foreach (var button in buttons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
            }

            buttons.Clear();
        }

        private void BuildLayout()
        {
            font = HomeUiFonts.Apply(fontAsset);
            regularFont = HomeUiFonts.ApplyRegular(regularFontAsset);
            buttonFont = buttonFontAsset != null ? buttonFontAsset : font;
            ResolveArrowIcons();

            canvasRoot = CreateCanvas();
            CreateBackground(canvasRoot);
            CreateFrame(canvasRoot);
            CreateBackButton(canvasRoot);
            CreateResetAllButton(canvasRoot);
            CreateTabs(panel);
            CreateDivider(panel);
            CreateContent(panel);
            CreateActionBar(panel);
            ApplyLobbyChrome();

            // Over every control on the panel, and off until a key plate is
            // waiting for a press.
            CreateRebindBlocker(canvasRoot);

            // Over the screen but under the confirmations, and it never takes a
            // click, so being on top costs the controls beneath it nothing.
            toast = ConnectionToast.AttachTo(canvasRoot);

            // Last, so they draw over everything they are asked about. The
            // confirmations go on top of the writing panel, which is the order
            // the two are asked for in: leaving with something typed asks
            // about the settings, not about the feedback.
            if (!lobbyOverlay)
            {
                CreateFeedback(canvasRoot);
            }

            CreateConfirm(canvasRoot);

            ShowTab(SettingsTab.General);
            SetActionsEnabled(false);
        }

        private void ApplyLobbyChrome()
        {
            if (!lobbyOverlay)
            {
                return;
            }

            if (feedbackRow != null)
            {
                feedbackRow.SetActive(false);
            }

            var back = canvasRoot != null ? canvasRoot.Find("BackButton") : null;
            if (back != null)
            {
                back.gameObject.SetActive(false);
            }

            var plate = panel != null ? panel.Find("LeaveGameButton") : null;
            if (plate != null)
            {
                plate.gameObject.SetActive(false);
            }

            EnsureLeaveGameLabel();
        }

        /// <summary>
        /// The Settings scene assigns these in the inspector. The lobby
        /// overlay adds this component at runtime, so the fields stay empty
        /// unless they are loaded here — and an Image with no sprite draws
        /// nothing.
        /// </summary>
        private void ResolveArrowIcons()
        {
            if (leftIcon == null)
            {
                leftIcon = Resources.Load<Sprite>(SettingsStyle.ArrowLeftIconResource);
            }

            if (rightIcon == null)
            {
                rightIcon = Resources.Load<Sprite>(SettingsStyle.ArrowRightIconResource);
            }

            closeIcon = SettingsStyle.LoadCloseIcon(closeIcon);
        }

        /// <summary>
        /// The frontend coordinator keeps one event system alive across the
        /// screens and turns the rest off, so this is only ever the one that
        /// gets used when the screen is played on its own.
        /// </summary>
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

        private RectTransform CreateCanvas()
        {
            var canvasObject = new GameObject("SettingsCanvas", typeof(RectTransform));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = SettingsStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var rect = canvasObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// The Home picture, filling the window and cropped rather than
        /// stretched, the same way Home draws it.
        /// </summary>
        private void CreateBackground(RectTransform canvas)
        {
            var art = CreateRect("Background", canvas);
            if (lobbyOverlay)
            {
                Stretch(art);
                var dim = art.gameObject.AddComponent<Image>();
                dim.color = SettingsStyle.Palette.OverlayDim;
                dim.raycastTarget = true;
                return;
            }

            SetAnchor(art, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = SettingsStyle.ReferenceResolution;

            var image = art.gameObject.AddComponent<Image>();
            image.sprite = backgroundSprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            image.color = backgroundSprite != null
                ? Color.white
                : SettingsStyle.Palette.BackgroundFallback;

            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = backgroundSprite != null && backgroundSprite.rect.height > 0f
                ? backgroundSprite.rect.width / backgroundSprite.rect.height
                : SettingsStyle.ReferenceResolution.x / SettingsStyle.ReferenceResolution.y;
        }

        /// <summary>
        /// The glow and the panel on top of it. Two rectangles rather than one
        /// with a shadow, because a canvas image has no shadow: the glow is a
        /// sprite of its own, sized so its lit edge lands where the design's
        /// spread puts it.
        /// </summary>
        private void CreateFrame(RectTransform canvas)
        {
            var margin = SettingsSprites.GlowMargin(
                SettingsStyle.Frame.GlowSpread, SettingsStyle.Frame.GlowBlur);

            var glow = CreateRect("Glow", canvas);
            SetAnchor(glow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            glow.anchoredPosition = SettingsStyle.Frame.Position + new Vector2(-margin, margin);
            glow.sizeDelta = SettingsStyle.Frame.Size + new Vector2(margin * 2f, margin * 2f);
            AddImage(
                glow,
                SettingsStyle.Palette.Glow,
                SettingsSprites.Glow(
                    SettingsStyle.Frame.Radius,
                    SettingsStyle.Frame.GlowSpread,
                    SettingsStyle.Frame.GlowBlur));

            panel = CreateRect("Panel", canvas);
            SetAnchor(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            panel.anchoredPosition = SettingsStyle.Frame.Position;
            panel.sizeDelta = SettingsStyle.Frame.Size;

            // Takes the clicks that land on the panel and miss a control, so
            // they do not fall through to the picture.
            AddImage(
                panel,
                SettingsStyle.Palette.PanelFill,
                HomeUiFonts.Rounded(SettingsStyle.Frame.Radius),
                raycastTarget: true);

            if (lobbyOverlay)
            {
                SettingsStyle.Frame.ApplyLobbyScale(glow);
                SettingsStyle.Frame.ApplyLobbyScale(panel);
            }
        }

        private void CreateBackButton(RectTransform canvas)
        {
            if (lobbyOverlay)
            {
                EnsureLeaveGameLabel();
                return;
            }

            var rect = CreateRect("BackButton", canvas);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = SettingsStyle.Back.Position;
            rect.sizeDelta = SettingsStyle.Back.Size;

            // Transparent, but present: the label alone would leave the padding
            // around it dead to the pointer.
            AddImage(rect, Color.clear, raycastTarget: true);

            var label = CreateText(
                "Label",
                rect,
                SettingsStyle.Back.Label,
                SettingsStyle.Back.FontSize,
                Color.white,
                TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform);

            AddTintButton(rect, label, SettingsStyle.Palette.BackLabel, () => BackRequested?.Invoke());
        }

        /// <summary>
        /// The circling arrow and its words, laid out by a layout group so the
        /// arrow sits against the words however wide they measure.
        /// </summary>
        private void CreateResetAllButton(RectTransform canvas)
        {
            var rect = CreateRect("ResetAllButton", canvas);
            SetAnchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(
                -SettingsStyle.ResetAll.RightMargin, -SettingsStyle.ResetAll.CentreY);
            rect.sizeDelta = new Vector2(0f, SettingsStyle.ResetAll.Height);
            AddImage(rect, Color.clear, raycastTarget: true);

            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SettingsStyle.ResetAll.IconGap;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var icon = CreateIcon(rect, refreshIcon, SettingsStyle.ResetAll.IconSize, Color.white);

            var label = CreateText(
                "Label",
                rect,
                SettingsStyle.ResetAll.Label,
                SettingsStyle.ResetAll.FontSize,
                Color.white,
                TextAlignmentOptions.MidlineRight,
                regularFont);

            AddTintButton(
                rect, label, SettingsStyle.Palette.ResetAllLabel, () => ResetAllRequested?.Invoke());

            // The arrow follows the words: the tint only reaches one graphic,
            // so the icon is repainted by hand as the pointer comes and goes.
            if (icon != null)
            {
                var follow = rect.gameObject.AddComponent<HomeHoverHighlight>();
                follow.Bind(icon, null, SettingsStyle.Palette.ResetAllLabel, SettingsStyle.Palette.TextHover);
            }
        }

        private void CreateTabs(RectTransform parent)
        {
            for (var index = 0; index < TabOrder.Length; index++)
            {
                var tab = TabOrder[index];
                var rect = CreateRect(tab.ToString() + "Tab", parent);
                SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                rect.anchoredPosition = SettingsStyle.Tabs.Origin
                                        + new Vector2(0f, -index * SettingsStyle.Tabs.Pitch);
                rect.sizeDelta = SettingsStyle.Tabs.Size;

                var fill = AddImage(
                    rect,
                    Color.clear,
                    SettingsSprites.RoundedRect(
                        SettingsStyle.Tabs.Radius,
                        RoundedCorners.Left),
                    raycastTarget: true);

                var label = CreateText(
                    "Label",
                    rect,
                    SettingsStyle.TabLabel(tab),
                    SettingsStyle.Tabs.FontSize,
                    SettingsStyle.Palette.TabIdleLabel,
                    TextAlignmentOptions.MidlineLeft);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(SettingsStyle.Tabs.LabelLeft, 0f);

                var hover = rect.gameObject.AddComponent<SettingsTabHover>();
                hover.Bind(fill, label);

                var captured = tab;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => TabSelected?.Invoke(captured));
                buttons.Add(button);

                tabHovers[index] = hover;
            }
        }

        private void CreateDivider(RectTransform parent)
        {
            var rect = CreateRect("Divider", parent);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(SettingsStyle.Divider.X, -SettingsStyle.Divider.Top);
            rect.sizeDelta = new Vector2(SettingsStyle.Divider.Thickness, SettingsStyle.Divider.Length);
            AddImage(rect, SettingsStyle.Palette.Divider);
        }

        /// <summary>
        /// 초기화 and 적용하기 along the bottom. Both start off; whether there
        /// is anything to apply is the presenter's to decide, and it says so
        /// through <see cref="SetActionsEnabled"/>.
        /// </summary>
        private void CreateActionBar(RectTransform parent)
        {
            var reset = CreatePlate(
                parent,
                "ResetButton",
                SettingsStyle.Buttons.ResetLeft,
                SettingsStyle.Buttons.ResetLabel,
                refreshIcon,
                out resetFill,
                out resetLabel,
                out resetIconImage);
            resetButton = AddPlateButton(reset, resetFill, () => ResetRequested?.Invoke());

            var apply = CreatePlate(
                parent,
                "ApplyButton",
                SettingsStyle.Buttons.ApplyLeft,
                SettingsStyle.Buttons.ApplyLabel,
                null,
                out applyFill,
                out applyLabel,
                out _);
            applyButton = AddPlateButton(apply, applyFill, () => ApplyRequested?.Invoke());
        }

        private void EnsureLeaveGameLabel()
        {
            if (leaveGameButton != null || canvasRoot == null)
            {
                return;
            }

            var rect = CreateRect("LeaveGameLabel", canvasRoot);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = SettingsStyle.Back.Position;
            rect.sizeDelta = SettingsStyle.Back.LeaveSize;
            AddImage(rect, Color.clear, raycastTarget: true);

            var label = CreateText(
                "Label",
                rect,
                SettingsStyle.Buttons.LeaveLabel,
                SettingsStyle.Back.FontSize,
                Color.white,
                TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform);
            AddTintButton(rect, label, Color.white, () => LeaveGameRequested?.Invoke());
            leaveGameButton = rect;
        }

        /// <summary>
        /// One of the bottom buttons: a pill with a word on it and, for 초기화,
        /// the circling arrow beside the word. A layout group centres the pair
        /// as one, so the arrow never has to be nudged by hand.
        /// </summary>
        private RectTransform CreatePlate(
            RectTransform parent,
            string name,
            float left,
            string label,
            Sprite icon,
            out Image fill,
            out TMP_Text text,
            out Image iconImage)
        {
            var rect = CreateRect(name, parent);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(left, -SettingsStyle.Buttons.Top);
            rect.sizeDelta = SettingsStyle.Buttons.Size;

            fill = AddImage(
                rect,
                SettingsStyle.Palette.ButtonOffFill,
                HomeUiFonts.Rounded(SettingsStyle.Buttons.Radius),
                raycastTarget: true);

            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SettingsStyle.Buttons.IconGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            iconImage = CreateIcon(rect, icon, SettingsStyle.Buttons.IconSize, SettingsStyle.Palette.ButtonOffLabel);

            text = CreateText(
                "Label",
                rect,
                label,
                SettingsStyle.Buttons.FontSize,
                SettingsStyle.Palette.ButtonOffLabel,
                TextAlignmentOptions.Center,
                buttonFont);
            return rect;
        }

        private Button AddPlateButton(RectTransform plate, Image fill, Action clicked)
        {
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            buttons.Add(button);
            return button;
        }

        /// <summary>
        /// A glyph for a layout group to place, given a fixed size through a
        /// layout element. Null when there is no sprite, so a plate without an
        /// icon has nothing taking up its space.
        /// </summary>
        private static Image CreateIcon(RectTransform parent, Sprite sprite, float size, Color color)
        {
            if (sprite == null)
            {
                return null;
            }

            var rect = CreateRect("Icon", parent);
            rect.sizeDelta = new Vector2(size, size);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size;
            element.preferredHeight = size;
            element.minWidth = size;
            element.minHeight = size;

            var image = AddImage(rect, color);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>
        /// A text button that lights up on hover: the tint multiplies the
        /// label's own colour, so the label is left white and the palette lives
        /// in the colour block.
        /// </summary>
        private Button AddTintButton(RectTransform rect, TMP_Text label, Color normal, Action clicked)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = SettingsStyle.Palette.TextHover;
            colors.pressedColor = SettingsStyle.Palette.Accent;
            colors.selectedColor = normal;
            colors.disabledColor = SettingsStyle.Palette.TextMuted;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            buttons.Add(button);
            return button;
        }

        private static void Paint(Graphic graphic, bool enabled, Color on, Color off)
        {
            if (graphic != null)
            {
                graphic.color = enabled ? on : off;
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image AddImage(
            RectTransform rect, Color color, Sprite sprite = null, bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;

                // The generated sprites are one pixel per unit of radius, so
                // the slice must not be rescaled by the canvas.
                image.pixelsPerUnitMultiplier = 1f;
            }

            return image;
        }

        /// <param name="face">
        /// The weight this piece of the design is drawn in. Null takes the
        /// screen's own, which is the SemiBold the tabs and the arrow use.
        /// </param>
        private TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            TMP_FontAsset face = null)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = face != null ? face : font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.richText = false;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }
    }
}
