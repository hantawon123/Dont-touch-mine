using System;
using Game.Client.Character;
using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// The panel 피드백 보내기 opens: a box to write in, a count of what has
    /// been written, and a way to send it or give up.
    /// </summary>
    /// <remarks>
    /// Over the same blurred still and dim the confirmations use, and built
    /// with the screen rather than when first asked for, so opening it is a
    /// flag rather than a construction.
    /// </remarks>
    public sealed partial class SettingsView
    {
        private GameObject feedbackRoot;
        private RawImage feedbackBackdrop;
        private TMP_InputField feedbackInput;
        private TMP_Text feedbackCounter;
        private Button feedbackSubmitButton;
        private Image feedbackSubmitFill;
        private TMP_Text feedbackSubmitLabel;
        private HomeHoverHighlight feedbackSubmitHover;
        private bool isFeedbackOpen;

        public event Action<string> FeedbackEdited;

        public event Action<string> FeedbackSubmitted;

        public event Action FeedbackDismissed;

        public void ShowFeedback()
        {
            if (feedbackRoot == null)
            {
                return;
            }

            isFeedbackOpen = true;
            feedbackRoot.SetActive(true);

            // Emptied on the way in rather than on the way out, so a panel
            // closed by Escape leaves nothing behind either.
            if (feedbackInput != null)
            {
                feedbackInput.SetTextWithoutNotify(string.Empty);
                feedbackInput.ActivateInputField();
            }

            ShowCount(0);
            BeginBackdrop(feedbackBackdrop);
        }

        public void HideFeedback()
        {
            isFeedbackOpen = false;
            if (feedbackInput != null)
            {
                feedbackInput.DeactivateInputField();
            }

            if (feedbackRoot != null)
            {
                feedbackRoot.SetActive(false);
            }

            EndBackdrop();
        }

        public void FeedbackSent()
        {
            HideFeedback();
            RaiseFeedbackDismissed();
        }

        /// <summary>
        /// Paints and arms 보내기. Both the colour and the interactable flag,
        /// and the hover with them: a plate that is merely grey still lights up
        /// under the pointer and still takes the click.
        /// </summary>
        public void SetFeedbackSubmitEnabled(bool enabled)
        {
            if (feedbackSubmitButton != null)
            {
                feedbackSubmitButton.interactable = enabled;
            }

            if (feedbackSubmitHover != null)
            {
                feedbackSubmitHover.Bind(
                    feedbackSubmitFill,
                    null,
                    enabled
                        ? CharacterClosetStyle.Palette.AcceptFill
                        : SettingsStyle.Palette.ButtonOffFill,
                    enabled
                        ? CharacterClosetStyle.Palette.AcceptHoverFill
                        : SettingsStyle.Palette.ButtonOffFill);
            }

            if (feedbackSubmitLabel != null)
            {
                feedbackSubmitLabel.color = enabled
                    ? CharacterClosetStyle.Palette.AcceptLabel
                    : SettingsStyle.Palette.ButtonOffLabel;
            }
        }

        private void RaiseFeedbackDismissed() => FeedbackDismissed?.Invoke();

        private void OnFeedbackTyped(string text)
        {
            ShowCount(text != null ? text.Length : 0);
            FeedbackEdited?.Invoke(text ?? string.Empty);
        }

        private void ShowCount(int length)
        {
            if (feedbackCounter != null)
            {
                feedbackCounter.text = $"{length}/{SettingsStyle.Feedback.MaxLength}";
            }
        }

        private void CreateFeedback(RectTransform canvas)
        {
            var root = CreateRect("Feedback", canvas);
            Stretch(root);

            var blurRect = CreateRect("Backdrop", root);
            Stretch(blurRect);
            feedbackBackdrop = blurRect.gameObject.AddComponent<RawImage>();
            feedbackBackdrop.raycastTarget = false;
            feedbackBackdrop.enabled = false;

            // Takes every click that misses the panel and does nothing with
            // it: closing by clicking away would throw out what was typed.
            var dimRect = CreateRect("Dim", root);
            Stretch(dimRect);
            AddImage(dimRect, CharacterClosetStyle.Palette.Dim, raycastTarget: true);

            CreateFeedbackPanel(root);

            root.gameObject.SetActive(false);
            feedbackRoot = root.gameObject;
        }

        private void CreateFeedbackPanel(RectTransform root)
        {
            var plate = CreateRect("Panel", root);
            SetAnchor(plate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            plate.anchoredPosition = Vector2.zero;
            plate.sizeDelta = SettingsStyle.Feedback.PanelSize;
            AddImage(
                plate,
                CharacterClosetStyle.Palette.ModalFill,
                HomeUiFonts.Rounded(SettingsStyle.Feedback.PanelRadius),
                raycastTarget: true);

            var title = CreateText(
                "Title",
                plate,
                SettingsStyle.Feedback.Title,
                SettingsStyle.Feedback.TitleFontSize,
                CharacterClosetStyle.Palette.ModalTitle,
                TextAlignmentOptions.Top);
            PlaceLine(title.rectTransform, SettingsStyle.Feedback.TitleTop, SettingsStyle.Feedback.TitleFontSize);

            var subtitle = CreateText(
                "Subtitle",
                plate,
                SettingsStyle.Feedback.Subtitle,
                SettingsStyle.Feedback.SubtitleFontSize,
                CharacterClosetStyle.Palette.ModalSubtitle,
                TextAlignmentOptions.Top,
                regularFont);
            PlaceLine(
                subtitle.rectTransform,
                SettingsStyle.Feedback.SubtitleTop,
                SettingsStyle.Feedback.SubtitleFontSize);

            CreateFeedbackField(plate);

            var counter = CreateText(
                "Counter",
                plate,
                string.Empty,
                SettingsStyle.Feedback.CounterFontSize,
                SettingsStyle.Palette.Counter,
                TextAlignmentOptions.MidlineRight,
                regularFont);
            var counterRect = counter.rectTransform;
            SetAnchor(counterRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            counterRect.anchoredPosition = new Vector2(
                -SettingsStyle.Feedback.SidePadding, -SettingsStyle.Feedback.CounterTop);
            counterRect.sizeDelta = new Vector2(160f, SettingsStyle.Feedback.CounterHeight);
            feedbackCounter = counter;
            ShowCount(0);

            var half = (CharacterClosetStyle.Modal.ButtonSize.x
                        + CharacterClosetStyle.Modal.ButtonGap) * 0.5f;
            var top = -SettingsStyle.Feedback.ButtonTop;

            CreateModalButton(
                plate,
                "CancelButton",
                new Vector2(-half, top),
                SettingsStyle.Feedback.CancelLabel,
                CharacterClosetStyle.Palette.DeclineFill,
                CharacterClosetStyle.Palette.DeclineHoverFill,
                CharacterClosetStyle.Palette.DeclineLabel,
                () => FeedbackDismissed?.Invoke(),
                out _,
                out _,
                out _);

            feedbackSubmitButton = CreateModalButton(
                plate,
                "SubmitButton",
                new Vector2(half, top),
                SettingsStyle.Feedback.SubmitLabel,
                CharacterClosetStyle.Palette.AcceptFill,
                CharacterClosetStyle.Palette.AcceptHoverFill,
                CharacterClosetStyle.Palette.AcceptLabel,
                () => FeedbackSubmitted?.Invoke(
                    feedbackInput != null ? feedbackInput.text : string.Empty),
                out feedbackSubmitFill,
                out feedbackSubmitLabel,
                out feedbackSubmitHover);

            // Nothing written yet, so there is nothing to send.
            SetFeedbackSubmitEnabled(false);

            CreateFeedbackCloseButton(plate);
        }

        /// <summary>
        /// The box itself: a plate, a window cut into it, and the text that
        /// scrolls inside the window.
        /// </summary>
        /// <remarks>
        /// The character limit is left to the field rather than enforced by the
        /// presenter, unlike the nickname on Home: there the refusal needs
        /// explaining, and here the counter reading 500/500 is the explanation.
        /// </remarks>
        private void CreateFeedbackField(RectTransform plate)
        {
            var field = CreateRect("FeedbackField", plate);
            SetAnchor(field, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            field.anchoredPosition = new Vector2(0f, -SettingsStyle.Feedback.FieldTop);
            field.sizeDelta = new Vector2(
                SettingsStyle.Feedback.FieldWidth, SettingsStyle.Feedback.FieldHeight);

            AddImage(
                field,
                SettingsStyle.Palette.FieldFill,
                HomeUiFonts.Rounded(SettingsStyle.Feedback.FieldRadius),
                raycastTarget: true);

            var viewport = CreateRect("TextArea", field);
            Stretch(viewport);
            var padding = SettingsStyle.Feedback.FieldPadding;
            viewport.offsetMin = new Vector2(padding, padding);
            viewport.offsetMax = new Vector2(-padding, -padding);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = CreateText(
                "Text",
                viewport,
                string.Empty,
                SettingsStyle.Feedback.FieldFontSize,
                SettingsStyle.Palette.FieldText,
                TextAlignmentOptions.TopLeft,
                regularFont);
            Stretch(text.rectTransform);
            text.raycastTarget = true;

            var placeholder = CreateText(
                "Placeholder",
                viewport,
                SettingsStyle.Feedback.Placeholder,
                SettingsStyle.Feedback.FieldFontSize,
                SettingsStyle.Palette.FieldPlaceholder,
                TextAlignmentOptions.TopLeft,
                regularFont);
            Stretch(placeholder.rectTransform);

            // TMP_InputField reads its parts as it wakes, so the object is kept
            // switched off until every one of them is in place.
            field.gameObject.SetActive(false);
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = text.font;
            input.pointSize = SettingsStyle.Feedback.FieldFontSize;

            // Enter puts in a new line rather than sending: this is a place to
            // write more than a sentence, and 보내기 is how it goes.
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.characterLimit = SettingsStyle.Feedback.MaxLength;
            input.customCaretColor = true;
            input.caretColor = SettingsStyle.Palette.FieldText;
            input.selectionColor = SettingsStyle.Palette.Accent;
            input.onValueChanged.AddListener(OnFeedbackTyped);
            field.gameObject.SetActive(true);
            feedbackInput = input;
        }

        private void CreateFeedbackCloseButton(RectTransform plate)
        {
            var rect = CreateRect("CloseButton", plate);
            SetAnchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(
                -CharacterClosetStyle.Modal.CloseOffset.x,
                -CharacterClosetStyle.Modal.CloseOffset.y);
            rect.sizeDelta = new Vector2(
                CharacterClosetStyle.Modal.CloseSize, CharacterClosetStyle.Modal.CloseSize);

            var image = AddImage(rect, CharacterClosetStyle.Palette.CloseIcon, raycastTarget: true);
            SettingsStyle.ApplyCloseIcon(image, closeIcon);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => FeedbackDismissed?.Invoke());
            buttons.Add(button);
        }

        /// <summary>
        /// A line of text across the panel, measured down from its top.
        /// </summary>
        private static void PlaceLine(RectTransform rect, float top, float fontSize)
        {
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, fontSize * 1.4f);
        }

        /// <summary>
        /// One of the pill buttons a modal on this screen puts along its
        /// bottom. Shared with the confirmations, which is why the colours are
        /// passed in rather than read from a palette here.
        /// </summary>
        private Button CreateModalButton(
            RectTransform panel,
            string name,
            Vector2 position,
            string label,
            Color fillColor,
            Color hoverColor,
            Color labelColor,
            Action clicked,
            out Image fill,
            out TMP_Text text,
            out HomeHoverHighlight hover)
        {
            var rect = CreateRect(name, panel);
            SetAnchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rect.anchoredPosition = position;
            rect.sizeDelta = CharacterClosetStyle.Modal.ButtonSize;

            fill = AddImage(
                rect,
                fillColor,
                HomeUiFonts.Rounded(CharacterClosetStyle.Modal.ButtonRadius),
                raycastTarget: true);

            text = CreateText(
                "Label",
                rect,
                label,
                CharacterClosetStyle.Modal.ButtonFontSize,
                labelColor,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            hover = rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(fill, null, fillColor, hoverColor);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            buttons.Add(button);
            return button;
        }
    }
}
