using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// The rows beside the tabs: one page per tab, seen through a window that
    /// scrolls when a page is taller than it.
    /// </summary>
    /// <remarks>
    /// Every page is built with the screen and switched off, and picking a tab
    /// switches one on. Building a page when its tab is first opened would
    /// save a little at startup and cost a stutter on the click, and there are
    /// only a few dozen rows in the whole screen.
    /// </remarks>
    public sealed partial class SettingsView
    {
        private static readonly GraphicsOption[] GraphicsRows =
            (GraphicsOption[])Enum.GetValues(typeof(GraphicsOption));

        private static readonly InterfaceOption[] InterfaceRows =
            (InterfaceOption[])Enum.GetValues(typeof(InterfaceOption));

        private static readonly NotificationOption[] NotificationRows =
            (NotificationOption[])Enum.GetValues(typeof(NotificationOption));

        private ScrollRect contentScroll;

        private readonly Dictionary<SettingsTab, RectTransform> pages =
            new Dictionary<SettingsTab, RectTransform>();

        private Stepper languageStepper;

        private readonly Dictionary<GraphicsOption, Stepper> graphicsSteppers =
            new Dictionary<GraphicsOption, Stepper>();

        private readonly Dictionary<InterfaceOption, Stepper> interfaceSteppers =
            new Dictionary<InterfaceOption, Stepper>();

        private readonly Dictionary<NotificationOption, Stepper> notificationSteppers =
            new Dictionary<NotificationOption, Stepper>();

        public void ShowLanguage(string label, bool canStep)
        {
            languageStepper?.Show(label, canStep);
        }

        public void ShowGraphics(GraphicsOption option, string label, bool canStep)
        {
            if (graphicsSteppers.TryGetValue(option, out var stepper))
            {
                stepper.Show(label, canStep);
            }
        }

        public void ShowInterface(InterfaceOption option, string label, bool canStep)
        {
            if (interfaceSteppers.TryGetValue(option, out var stepper))
            {
                stepper.Show(label, canStep);
            }
        }

        public void ShowNotification(NotificationOption option, string label, bool canStep)
        {
            if (notificationSteppers.TryGetValue(option, out var stepper))
            {
                stepper.Show(label, canStep);
            }
        }

        /// <summary>
        /// Brings a tab's page into the window, back at the top.
        /// </summary>
        /// <remarks>
        /// Scrolled home on every switch rather than remembering where each
        /// page was left: a tab opening halfway down its list looks like a
        /// screen that has lost its place, and the first row is the one the
        /// design draws.
        /// </remarks>
        private void ShowPage(SettingsTab tab)
        {
            RectTransform shown = null;
            foreach (var pair in pages)
            {
                var isShown = pair.Key == tab;
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(isShown);
                }

                if (isShown)
                {
                    shown = pair.Value;
                }
            }

            if (contentScroll == null)
            {
                return;
            }

            contentScroll.StopMovement();
            contentScroll.content = shown;
            if (shown != null)
            {
                shown.anchoredPosition = Vector2.zero;
                contentScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// The window, the handle beside it, and a page for every tab that has
        /// rows to show.
        /// </summary>
        /// <remarks>
        /// The scrolling rect is its own viewport, which is why the mask sits
        /// on the same object: there is nothing between the window and the
        /// pages that would want a rect of its own.
        /// </remarks>
        private void CreateContent(RectTransform parent)
        {
            var window = CreateRect("Content", parent);
            SetAnchor(window, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            window.anchoredPosition = SettingsStyle.Rows.Origin;
            window.sizeDelta = SettingsStyle.Rows.ViewportSize;

            // Nothing to see, but it takes the wheel: without a graphic here
            // the empty half of a short page would not scroll.
            AddImage(window, Color.clear, raycastTarget: true);
            window.gameObject.AddComponent<RectMask2D>();

            contentScroll = window.gameObject.AddComponent<SettingsScrollRect>();
            contentScroll.horizontal = false;
            contentScroll.vertical = true;
            contentScroll.movementType = ScrollRect.MovementType.Clamped;
            contentScroll.scrollSensitivity = SettingsStyle.Scroll.Sensitivity;
            contentScroll.verticalScrollbar = CreateScrollbar(parent);

            // Hidden on a page that fits, which is every tab but 그래픽 today.
            contentScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            CreateGeneralPage(window);
            CreateGraphicsPage(window);
            CreateInterfacePage(window);
            CreateSoundPage(window);
            CreateControlsPage(window);
            CreateNotificationsPage(window);

            // The four tabs still to be built get an empty page rather than
            // none, so picking one leaves the window with something to show
            // and the handle beside it with nothing to scroll.
            foreach (var tab in TabOrder)
            {
                if (!pages.ContainsKey(tab))
                {
                    CreatePage(window, tab, 0);
                }
            }
        }

        private Scrollbar CreateScrollbar(RectTransform panel)
        {
            var track = CreateRect("Scrollbar", panel);
            SetAnchor(track, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            track.anchoredPosition = new Vector2(
                -SettingsStyle.Scroll.RightMargin, SettingsStyle.Rows.Origin.y);
            track.sizeDelta = new Vector2(
                SettingsStyle.Scroll.Width, SettingsStyle.Rows.ViewportSize.y);

            var area = CreateRect("SlidingArea", track);
            Stretch(area);

            var handle = CreateRect("Handle", area);

            // Stretched to the track before the scrollbar takes it over: the
            // scrollbar drives the axis it scrolls and leaves the other one at
            // whatever the rect was built with, which by default is 100 pixels
            // of nothing.
            Stretch(handle);
            handle.sizeDelta = Vector2.zero;

            var handleImage = AddImage(
                handle,
                SettingsStyle.Palette.ScrollHandle,
                HomeUiFonts.Rounded(SettingsStyle.Scroll.Radius),
                raycastTarget: true);

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.transition = Selectable.Transition.None;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        private void CreateGeneralPage(RectTransform window)
        {
            var page = CreatePage(window, SettingsTab.General, lobbyOverlay ? 1 : 2);

            var language = CreateRow(page, "LanguageRow", 0, SettingsStyle.LanguageRow.Label);
            languageRowLabel = language.Find("Label")?.GetComponent<TMP_Text>();
            languageStepper = CreateStepper(
                language, steps => LanguageStepRequested?.Invoke(steps));

            if (lobbyOverlay)
            {
                return;
            }

            var feedback = CreateRow(page, "FeedbackRow", 1, SettingsStyle.FeedbackRow.Label);
            RememberRow(feedback, UiText.Settings.Feedback);
            feedbackRow = feedback.gameObject;
            CreateFeedbackButton(feedback);
        }

        private void CreateGraphicsPage(RectTransform window)
        {
            var page = CreatePage(window, SettingsTab.Graphics, GraphicsRows.Length);

            for (var index = 0; index < GraphicsRows.Length; index++)
            {
                var option = GraphicsRows[index];
                var row = CreateRow(
                    page,
                    option + "Row",
                    index,
                    SettingsStyle.GraphicsRowLabel(option));
                RememberRow(row, SettingsStyle.GraphicsRowKey(option));

                var captured = option;
                graphicsSteppers[option] = CreateStepper(
                    row, steps => GraphicsStepRequested?.Invoke(captured, steps));
            }
        }

        private void CreateInterfacePage(RectTransform window)
        {
            var page = CreatePage(window, SettingsTab.Interface, InterfaceRows.Length);

            for (var index = 0; index < InterfaceRows.Length; index++)
            {
                var option = InterfaceRows[index];
                var row = CreateRow(
                    page,
                    option + "Row",
                    index,
                    SettingsStyle.InterfaceRowLabel(option));
                RememberRow(row, SettingsStyle.InterfaceRowKey(option));

                var captured = option;
                interfaceSteppers[option] = CreateStepper(
                    row, steps => InterfaceStepRequested?.Invoke(captured, steps));
            }
        }

        /// <summary>
        /// The 알림 page. No headings, so its rows start where every other
        /// unheaded page's do.
        /// </summary>
        private void CreateNotificationsPage(RectTransform window)
        {
            var page = CreatePage(window, SettingsTab.Notifications, NotificationRows.Length);

            for (var index = 0; index < NotificationRows.Length; index++)
            {
                var option = NotificationRows[index];
                var row = CreateRow(
                    page,
                    option + "Row",
                    index,
                    SettingsStyle.Notifications.RowLabel(option));
                RememberRow(row, SettingsStyle.Notifications.RowKey(option));

                var captured = option;
                notificationSteppers[option] = CreateStepper(
                    row, steps => NotificationStepRequested?.Invoke(captured, steps));
            }
        }

        /// <summary>
        /// One tab's page: as wide as the window and as tall as its rows need,
        /// hung from the window's top-left so it scrolls up out of sight.
        /// </summary>
        private RectTransform CreatePage(RectTransform window, SettingsTab tab, int rows) =>
            CreatePage(window, tab, SettingsStyle.Rows.PageHeight(rows));

        /// <param name="height">
        /// How tall the page stands. A page laid out by row count knows this
        /// from the count; one with headings between its rows works it out as
        /// it goes and sets it afterwards.
        /// </param>
        private RectTransform CreatePage(RectTransform window, SettingsTab tab, float height)
        {
            var page = CreateRect(tab + "Page", window);
            SetAnchor(page, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            page.anchoredPosition = Vector2.zero;
            page.sizeDelta = new Vector2(SettingsStyle.Rows.Size.x, height);
            page.gameObject.SetActive(false);
            pages[tab] = page;
            return page;
        }

        /// <summary>
        /// One row: its name on the left and room for a control on the right,
        /// over a plate that appears while the pointer is on it.
        /// </summary>
        /// <remarks>
        /// Every row is bare until pointed at. The mock-up draws one row of
        /// each page on a grey plate, but that is this state rather than a
        /// plate the row keeps.
        /// <para>
        /// The plate takes the pointer, which is also what lets it light up.
        /// The control inside the row is its child and so sits on top, and
        /// pointing at that control lights the row as well: entering a child
        /// enters everything it hangs from.
        /// </para>
        /// </remarks>
        private RectTransform CreateRow(RectTransform page, string name, int index, string label) =>
            CreateRowAt(page, name, index * SettingsStyle.Rows.Pitch, label, SettingsStyle.Rows.LabelLeft);

        /// <param name="top">Down from the page's top to the row's top.</param>
        /// <param name="labelLeft">
        /// Where the name starts. Rows under a heading are indented past the
        /// heading; every other row starts at <see cref="SettingsStyle.Rows.LabelLeft"/>.
        /// </param>
        private RectTransform CreateRowAt(
            RectTransform page, string name, float top, string label, float labelLeft)
        {
            var rect = CreateRect(name, page);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = SettingsStyle.Rows.Size;

            var plate = AddImage(
                rect,
                SettingsStyle.Palette.IdleFill,
                SettingsSprites.RoundedRect(
                    SettingsStyle.Rows.Radius,
                    RoundedCorners.Right),
                raycastTarget: true);

            var hover = rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(plate, null, SettingsStyle.Palette.IdleFill, SettingsStyle.Palette.HoverFill);

            var text = CreateText(
                "Label",
                rect,
                label,
                SettingsStyle.Rows.LabelFontSize,
                SettingsStyle.Palette.RowLabel,
                TextAlignmentOptions.MidlineLeft,
                regularFont);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(labelLeft, 0f);
            return rect;
        }

        private void RememberRow(RectTransform row, string key)
        {
            if (row == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            var text = row.Find("Label")?.GetComponent<TMP_Text>();
            if (text != null)
            {
                rowLabels.Add((text, key));
            }
        }

        private void Remember(TMP_Text text, string key)
        {
            if (text != null && !string.IsNullOrEmpty(key))
            {
                rowLabels.Add((text, key));
            }
        }

        /// <summary>
        /// Two arrows with the chosen value between them, ranged against the
        /// row's right edge. Every row of every tab but the feedback one is
        /// one of these.
        /// </summary>
        /// <param name="wrapValue">
        /// Whether the value may run onto a second line, smaller. For the one
        /// row whose value the machine names rather than we do; every other
        /// row's is short enough for one line at full size.
        /// </param>
        private Stepper CreateStepper(RectTransform row, Action<int> stepped, bool wrapValue = false)
        {
            var picker = CreateRect("Picker", row);
            SetAnchor(picker, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            picker.anchoredPosition = new Vector2(-SettingsStyle.Rows.RightMargin, 0f);
            picker.sizeDelta = new Vector2(
                SettingsStyle.Stepper.Width, SettingsStyle.Rows.Size.y);

            var stepper = new Stepper();
            stepper.Left = CreateArrow(
                picker, "LeftArrow", leftIcon, new Vector2(0f, 0.5f),
                out var leftImage, () => stepped(-1));
            stepper.LeftIcon = leftImage;
            stepper.Right = CreateArrow(
                picker, "RightArrow", rightIcon, new Vector2(1f, 0.5f),
                out var rightImage, () => stepped(1));
            stepper.RightIcon = rightImage;

            var value = CreateText(
                "Value",
                picker,
                string.Empty,
                SettingsStyle.Stepper.ValueFontSize,
                SettingsStyle.Palette.Value,
                TextAlignmentOptions.Center,
                regularFont);
            // Shrunk to fit before being cut short, so a value a little too
            // long is still read rather than trimmed.
            value.enableAutoSizing = true;
            value.overflowMode = TextOverflowModes.Ellipsis;

            if (wrapValue)
            {
                // A box exactly as tall as the lines it may use, centred in the
                // row. The shrinking then has to find a size that fits the text
                // into those lines, rather than laying out a line more and
                // hiding it — which is what left the visible lines sitting high.
                var box = value.rectTransform;
                SetAnchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                box.anchoredPosition = Vector2.zero;
                box.sizeDelta = new Vector2(
                    SettingsStyle.Stepper.Width - (SettingsStyle.Stepper.ArrowHitWidth * 2f),
                    SettingsStyle.Stepper.WrappedValueHeight);

                value.enableWordWrapping = true;
                value.fontSizeMax = SettingsStyle.Stepper.WrappedValueFontSize;
                value.fontSizeMin = SettingsStyle.Stepper.WrappedValueMinFontSize;
            }
            else
            {
                Stretch(value.rectTransform);
                value.rectTransform.offsetMin = new Vector2(SettingsStyle.Stepper.ArrowHitWidth, 0f);
                value.rectTransform.offsetMax = new Vector2(-SettingsStyle.Stepper.ArrowHitWidth, 0f);

                value.enableWordWrapping = false;
                value.fontSizeMax = SettingsStyle.Stepper.ValueFontSize;
                value.fontSizeMin = SettingsStyle.Stepper.ValueMinFontSize;
            }

            stepper.Value = value;
            return stepper;
        }

        /// <summary>
        /// One arrow: a glyph drawn at the design's size inside a strip wide
        /// enough to click, hung on one end of the picker.
        /// </summary>
        private Button CreateArrow(
            RectTransform picker, string name, Sprite sprite, Vector2 anchor,
            out Image icon, Action clicked)
        {
            var rect = CreateRect(name, picker);
            SetAnchor(rect, anchor, anchor, anchor);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                SettingsStyle.Stepper.ArrowHitWidth, SettingsStyle.Rows.Size.y);
            var hit = AddImage(rect, Color.clear, raycastTarget: true);

            // The glyph sits at the outer edge of its strip, which is where the
            // design draws it: the strip grows inward, toward the value.
            var glyph = CreateRect("Icon", rect);
            SetAnchor(glyph, anchor, anchor, anchor);
            glyph.anchoredPosition = Vector2.zero;
            glyph.sizeDelta = new Vector2(
                SettingsStyle.Stepper.ArrowSize, SettingsStyle.Stepper.ArrowSize);
            icon = AddImage(glyph, SettingsStyle.Palette.ArrowEnabled);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            buttons.Add(button);
            return button;
        }

        private void CreateFeedbackButton(RectTransform row)
        {
            var rect = CreateRect("FeedbackButton", row);
            SetAnchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(-SettingsStyle.Rows.RightMargin, 0f);
            rect.sizeDelta = SettingsStyle.FeedbackRow.ButtonSize;
            feedbackButton = rect;

            var fill = AddImage(
                rect,
                SettingsStyle.Palette.FeedbackFill,
                HomeUiFonts.Rounded(SettingsStyle.FeedbackRow.ButtonRadius),
                raycastTarget: true);

            var label = CreateText(
                "Label",
                rect,
                SettingsStyle.FeedbackRow.ButtonLabel,
                SettingsStyle.FeedbackRow.ButtonFontSize,
                SettingsStyle.Palette.FeedbackLabel,
                TextAlignmentOptions.Center,
                regularFont);
            Stretch(label.rectTransform);

            var hover = rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(fill, null, SettingsStyle.Palette.FeedbackFill, SettingsStyle.Palette.FeedbackHoverFill);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            feedbackButtonLabel = label;
            button.onClick.AddListener(() => FeedbackRequested?.Invoke());
            buttons.Add(button);
            FitFeedbackButton();
        }

        private void FitFeedbackButton()
        {
            if (feedbackButton == null || feedbackButtonLabel == null)
            {
                return;
            }

            feedbackButtonLabel.textWrappingMode = TextWrappingModes.NoWrap;
            feedbackButtonLabel.overflowMode = TextOverflowModes.Overflow;
            feedbackButtonLabel.ForceMeshUpdate();
            feedbackButton.sizeDelta = new Vector2(
                feedbackButtonLabel.preferredWidth + (SettingsStyle.FeedbackRow.ButtonPaddingX * 2f),
                SettingsStyle.FeedbackRow.ButtonSize.y);
        }

        private static void SetArrow(Button button, Image icon, bool enabled)
        {
            if (button != null)
            {
                button.interactable = enabled;
            }

            if (icon != null)
            {
                icon.color = enabled
                    ? SettingsStyle.Palette.ArrowEnabled
                    : SettingsStyle.Palette.ArrowDisabled;
            }
        }

        /// <summary>
        /// The three pieces of one row's picker, so a row can be redrawn
        /// without the view keeping a field for each half of each arrow.
        /// </summary>
        private sealed class Stepper
        {
            public TMP_Text Value;
            public Button Left;
            public Button Right;
            public Image LeftIcon;
            public Image RightIcon;

            public void Show(string label, bool canStep)
            {
                if (Value != null)
                {
                    Value.text = label;
                }

                SetArrow(Left, LeftIcon, canStep);
                SetArrow(Right, RightIcon, canStep);
            }
        }
    }
}
