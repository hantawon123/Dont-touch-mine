using System;
using Game.Client.Common;
using Game.Client.Settings;
using Game.Core.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// The room-creation modal: who may join, what it is called, how many fit.
    /// </summary>
    /// <remarks>
    /// The only thing on this screen that dims Home behind it. The other panels
    /// hang off the control that opened them and leave the picture readable;
    /// this one is a question that has to be answered or dismissed, so it takes
    /// the screen and every press outside it lands on the dim rather than on
    /// whatever is under there.
    /// </remarks>
    public sealed partial class HomeMenuView
    {
        /// <summary>
        /// Raised with everything the player filled in.
        /// </summary>
        public event Action<string, bool, int> RoomCreationRequested;

        public event Action CreateRoomDismissed;

        private void CreateRoomModalRoot(RectTransform canvas)
        {
            var root = CreateRect("CreateRoomRoot", canvas);
            root.gameObject.SetActive(false);
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var dimRect = CreateRect("Dim", root);
            SetAnchor(dimRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            // Takes clicks but does nothing with them. The dim is there to stop
            // Home being operated behind the modal, and the X is the only way
            // out: a form with something typed in it should not be thrown away
            // by a stray click beside it.
            AddImage(dimRect, HomeStyle.Palette.Dim, raycastTarget: true);

            var modal = CreateRect("CreateRoomModal", root);
            SetAnchor(modal, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            modal.anchoredPosition = Vector2.zero;
            modal.sizeDelta = HomeStyle.CreateRoom.ModalSize;

            var fill = AddImage(
                modal,
                HomeStyle.Palette.ModalFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Modal),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            CreateRoomHeader(modal);
            CreateScopeRow(modal, RowTop(0));
            CreateRoomNameRow(modal, RowTop(1));
            CreatePlayerCountRow(modal, RowTop(2));
            CreateCreateButton(modal);

            createRoomRoot = root.gameObject;
            ResetRoomForm();
        }

        /// <summary>
        /// Where a row's top sits, counted down from the title.
        /// </summary>
        private static float RowTop(int index)
        {
            return HomeStyle.CreateRoom.VerticalPadding
                + HomeStyle.FontSize.ModalTitle
                + HomeStyle.CreateRoom.RowGap
                + (index * (HomeStyle.CreateRoom.RowHeight + HomeStyle.CreateRoom.RowGap));
        }

        private void CreateRoomHeader(RectTransform modal)
        {
            var title = CreateRect("Title", modal);
            SetAnchor(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            title.anchoredPosition = new Vector2(
                HomeStyle.CreateRoom.SidePadding, -HomeStyle.CreateRoom.VerticalPadding);
            title.sizeDelta = new Vector2(240f, HomeStyle.FontSize.ModalTitle * 1.3f);
            var text = AddText(
                title,
                Copy(UiText.Home.CreateRoom),
                HomeStyle.FontSize.ModalTitle,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            ApplyMenuFont(text);
            text.color = HomeStyle.Palette.TextPrimary;
            Remember(text, UiText.Home.CreateRoom);

            var close = CreateRect("Close", modal);
            SetAnchor(close, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            close.anchoredPosition = new Vector2(
                -HomeStyle.CreateRoom.SidePadding, -HomeStyle.CreateRoom.VerticalPadding);
            close.sizeDelta = new Vector2(
                HomeStyle.CreateRoom.CloseIconSize, HomeStyle.CreateRoom.CloseIconSize);
            var icon = AddImage(close, Color.white, raycastTarget: true);
            SettingsStyle.ApplyCloseIcon(icon, closeIcon);

            var button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => CreateRoomDismissed?.Invoke());
            menuButtons.Add(button);
        }

        /// <summary>
        /// The label and the strip beside it that every row shares.
        /// </summary>
        private RectTransform CreateFormRow(RectTransform modal, string name, string key, float top)
        {
            var labelRect = CreateRect(name, modal);
            SetAnchor(labelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            labelRect.anchoredPosition = new Vector2(HomeStyle.CreateRoom.RowsLeft, -top);
            labelRect.sizeDelta = new Vector2(
                HomeStyle.CreateRoom.LabelWidth, HomeStyle.CreateRoom.RowHeight);
            var text = AddText(
                labelRect,
                Copy(key),
                HomeStyle.FontSize.RowLabel,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            ApplyMenuFont(text);
            text.color = HomeStyle.Palette.TextPrimary;
            Remember(text, key);

            var control = CreateRect($"{name}Control", modal);
            SetAnchor(control, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            control.anchoredPosition = new Vector2(
                HomeStyle.CreateRoom.RowsLeft
                    + HomeStyle.CreateRoom.LabelWidth
                    + HomeStyle.CreateRoom.LabelToControl,
                -top);
            control.sizeDelta = new Vector2(
                HomeStyle.CreateRoom.ControlWidth, HomeStyle.CreateRoom.RowHeight);
            return control;
        }

        /// <summary>
        /// One track with a lit half sliding in it, as the design draws it.
        /// </summary>
        /// <remarks>
        /// Not two buttons meeting in the middle: the grey is a single bar and
        /// the orange is inset within it on all four sides, so the two never
        /// share an edge and the selected half reads as sitting on top rather
        /// than as half the control.
        /// </remarks>
        private void CreateScopeRow(RectTransform modal, float top)
        {
            var control = CreateFormRow(modal, "방 범위", UiText.Home.RoomScope, top);

            var track = AddImage(
                control,
                HomeStyle.Palette.SegmentOffFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Control));
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 1f;

            var indicator = CreateRect("Indicator", control);
            indicator.sizeDelta = Vector2.zero;
            scopeIndicator = indicator;
            var lit = AddImage(
                indicator,
                HomeStyle.Palette.SegmentOnFill,
                HomeUiFonts.Rounded(
                    Mathf.RoundToInt(
                        (HomeStyle.CreateRoom.RowHeight
                            - (HomeStyle.CreateRoom.SegmentInset * 2f)) * 0.5f)));
            lit.type = Image.Type.Sliced;
            lit.pixelsPerUnitMultiplier = 1f;

            privateSegment = CreateSegmentLabel(control, "PRIVATE", 0f, 0.5f, () => SetRoomPublic(false));
            publicSegment = CreateSegmentLabel(control, "PUBLIC", 0.5f, 1f, () => SetRoomPublic(true));
        }

        private TMP_Text CreateSegmentLabel(
            RectTransform parent, string label, float min, float max, Action onClicked)
        {
            var half = CreateRect(label, parent);
            SetAnchor(half, new Vector2(min, 0f), new Vector2(max, 1f), new Vector2(0.5f, 0.5f));
            half.offsetMin = Vector2.zero;
            half.offsetMax = Vector2.zero;

            var text = AddText(
                half,
                label,
                HomeStyle.FontSize.Segment,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                raycastTarget: true);
            ApplyMenuFont(text);

            var button = half.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClicked());
            menuButtons.Add(button);
            return text;
        }

        private void CreateRoomNameRow(RectTransform modal, float top)
        {
            var control = CreateFormRow(modal, "방 이름", UiText.Home.RoomTitle, top);

            var background = AddImage(
                control,
                HomeStyle.Palette.FieldFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Control),
                raycastTarget: true);
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;

            var viewport = CreateRect("TextArea", control);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = new Vector2(HomeStyle.CreateRoom.FieldPadding, 2f);
            viewport.offsetMax = new Vector2(-HomeStyle.CreateRoom.FieldPadding, -2f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var textRect = CreateRect("Text", viewport);
            SetAnchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = AddText(
                textRect,
                string.Empty,
                HomeStyle.FontSize.RoomName,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                raycastTarget: true);
            text.color = HomeStyle.Palette.TextPrimary;

            var placeholderRect = CreateRect("Placeholder", viewport);
            SetAnchor(placeholderRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholder = AddText(
                placeholderRect,
                Copy(UiText.Home.RoomTitlePlaceholder),
                HomeStyle.FontSize.RoomName,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            placeholder.color = HomeStyle.Palette.Placeholder;
            Remember(placeholder, UiText.Home.RoomTitlePlaceholder);

            control.gameObject.SetActive(false);
            var input = control.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = koreanFont;
            input.pointSize = HomeStyle.FontSize.RoomName;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = HomeStyle.Palette.TextPrimary;

            // Focus must not select everything: the box is given focus back by
            // code after a refused syllable, and the next key would otherwise
            // replace the whole title. A click puts the caret where it landed.
            input.onFocusSelectAll = false;

            // A room title takes anything typeable, so the only rule is length
            // and the field can enforce it itself.
            input.characterLimit = HomeStyle.CreateRoom.MaxTitleLength;
            input.onValueChanged.AddListener(OnRoomNameEdited);
            input.onSelect.AddListener(_ => WatchRoomNameComposition(true));
            input.onDeselect.AddListener(_ => WatchRoomNameComposition(false));
            control.gameObject.SetActive(true);
            roomNameInput = input;

            var counterRect = CreateRect("Counter", modal);
            SetAnchor(counterRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f));
            counterRect.anchoredPosition = new Vector2(
                control.anchoredPosition.x + HomeStyle.CreateRoom.ControlWidth,
                -(top + HomeStyle.CreateRoom.RowHeight));
            counterRect.sizeDelta = new Vector2(80f, HomeStyle.FontSize.RoomNameCounter * 1.6f);
            roomNameCounter = AddText(
                counterRect,
                $"0/{HomeStyle.CreateRoom.MaxTitleLength}",
                HomeStyle.FontSize.RoomNameCounter,
                FontStyles.Normal,
                TextAlignmentOptions.TopRight);
            roomNameCounter.color = HomeStyle.Palette.CounterFaint;
        }

        private void CreatePlayerCountRow(RectTransform modal, float top)
        {
            var control = CreateFormRow(modal, "인원", UiText.Home.RoomPlayers, top);

            var background = AddImage(
                control,
                HomeStyle.Palette.FieldFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Control));
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;

            decreaseButton = CreateStepper(control, "-", -1, false);
            increaseButton = CreateStepper(control, "+", 1, true);

            var valueRect = CreateRect("Value", control);
            SetAnchor(valueRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            playerCountText = AddText(
                valueRect,
                HomeStyle.CreateRoom.DefaultPlayers.ToString(),
                HomeStyle.FontSize.PlayerCount,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            playerCountText.color = HomeStyle.Palette.TextPrimary;
        }

        private Button CreateStepper(RectTransform parent, string label, int step, bool onRight)
        {
            var rect = CreateRect(step > 0 ? "Increase" : "Decrease", parent);
            var edge = onRight ? 1f : 0f;
            SetAnchor(rect, new Vector2(edge, 0.5f), new Vector2(edge, 0.5f), new Vector2(edge, 0.5f));
            rect.anchoredPosition = new Vector2(
                onRight ? -HomeStyle.CreateRoom.StepperPadding : HomeStyle.CreateRoom.StepperPadding,
                0f);
            rect.sizeDelta = new Vector2(
                HomeStyle.CreateRoom.RowHeight, HomeStyle.CreateRoom.RowHeight);

            var text = AddText(
                rect,
                label,
                HomeStyle.FontSize.Stepper,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                raycastTarget: true);
            text.color = HomeStyle.Palette.TextPrimary;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => StepPlayerCount(step));
            menuButtons.Add(button);
            return button;
        }

        private void CreateCreateButton(RectTransform modal)
        {
            var rect = CreateRect("CreateRoomButton", modal);
            SetAnchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            rect.anchoredPosition = new Vector2(0f, HomeStyle.CreateRoom.VerticalPadding);
            rect.sizeDelta = HomeStyle.CreateRoom.CreateSize;

            createRoomFill = AddImage(
                rect,
                HomeStyle.Palette.ApplyOffFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Control),
                raycastTarget: true);
            createRoomFill.type = Image.Type.Sliced;
            createRoomFill.pixelsPerUnitMultiplier = 1f;

            var labelRect = CreateRect("Label", rect);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            createRoomLabel = AddText(
                labelRect,
                Copy(UiText.Home.RoomSubmit),
                HomeStyle.FontSize.Create,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            ApplyMenuFont(createRoomLabel);
            Remember(createRoomLabel, UiText.Home.RoomSubmit);

            createRoomButton = rect.gameObject.AddComponent<Button>();
            createRoomButton.targetGraphic = createRoomFill;
            createRoomButton.transition = Selectable.Transition.None;
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            menuButtons.Add(createRoomButton);
        }

        public void SetCreateRoomVisible(bool visible)
        {
            if (createRoomRoot == null)
            {
                return;
            }

            if (visible)
            {
                ResetRoomForm();
            }

            createRoomRoot.SetActive(visible);
        }

        /// <summary>
        /// Puts the form back to its opening state.
        /// </summary>
        /// <remarks>
        /// Done on the way in rather than on the way out, so a modal that was
        /// dismissed mid-typing cannot come back holding the abandoned answer.
        /// </remarks>
        private void ResetRoomForm()
        {
            playerCount = HomeStyle.CreateRoom.DefaultPlayers;
            SetRoomPublic(true);
            if (roomNameInput != null)
            {
                roomNameInput.text = string.Empty;
            }

            roomNameComposing = string.Empty;
            UpdateRoomNameCounter();
            UpdatePlayerCount();
            UpdateCreateEnabled();
        }

        private void SetRoomPublic(bool isPublic)
        {
            this.isPublicRoom = isPublic;
            if (scopeIndicator == null)
            {
                return;
            }

            var inset = HomeStyle.CreateRoom.SegmentInset;
            scopeIndicator.anchorMin = new Vector2(isPublic ? 0.5f : 0f, 0f);
            scopeIndicator.anchorMax = new Vector2(isPublic ? 1f : 0.5f, 1f);
            scopeIndicator.offsetMin = new Vector2(inset, inset);
            scopeIndicator.offsetMax = new Vector2(-inset, -inset);

            publicSegment.color = isPublic
                ? HomeStyle.Palette.SegmentOnLabel
                : HomeStyle.Palette.SegmentOffLabel;
            privateSegment.color = isPublic
                ? HomeStyle.Palette.SegmentOffLabel
                : HomeStyle.Palette.SegmentOnLabel;
        }

        private void OnRoomNameEdited(string value)
        {
            // Whatever was being composed is now part of the text, or was
            // refused by the limit; either way it is no longer pending.
            roomNameComposing = string.Empty;
            UpdateRoomNameCounter();
            UpdateCreateEnabled();
        }

        /// <summary>
        /// How much is in the box, counting the syllable still being composed.
        /// </summary>
        /// <remarks>
        /// A Korean keyboard hands a syllable over only once the next keystroke
        /// settles it. Until then the field draws the part-built glyph but
        /// keeps it out of <c>text</c>, so a counter fed from <c>text</c> alone
        /// runs one glyph behind what the player can see. The composition is
        /// read off the keyboard here and off the browser's own field on
        /// WebGL, and counted as if it were already in.
        /// </remarks>
        private int TypedRoomNameLength =>
            (roomNameInput != null ? roomNameInput.text.Length : 0) + roomNameComposing.Length;

        private void UpdateRoomNameCounter()
        {
            if (roomNameCounter == null)
            {
                return;
            }

            // The field never keeps more than the limit, so the counter never
            // claims more either, even while a refused glyph is still drawn.
            var shown = Mathf.Min(TypedRoomNameLength, HomeStyle.CreateRoom.MaxTitleLength);
            roomNameCounter.text = $"{shown}/{HomeStyle.CreateRoom.MaxTitleLength}";
        }

        /// <summary>
        /// Reads the syllable being built off the same place the field draws
        /// it from, once a frame while the box has focus.
        /// </summary>
        /// <remarks>
        /// Not the Input System's <c>onIMECompositionChange</c>: that only
        /// fires once IME has been switched on through the Input System, and
        /// TextMeshPro switches it on through <c>Input.imeCompositionMode</c>
        /// instead, so the event stayed silent while the glyph was plainly on
        /// screen. <c>Input.compositionString</c> is what the field itself
        /// reads, so the two cannot disagree.
        /// </remarks>
        private void PollRoomNameComposition()
        {
            if (roomNameInput == null)
            {
                return;
            }

            if (roomNameRefocusPending)
            {
                roomNameRefocusPending = false;
                if (roomNameInput.isActiveAndEnabled)
                {
                    roomNameInput.Select();
                    roomNameInput.ActivateInputField();
                    roomNameInput.caretPosition = roomNameInput.text.Length;
                }

                return;
            }

            if (!roomNameInput.isFocused)
            {
                return;
            }

            SetRoomNameComposing(Input.compositionString ?? string.Empty);
        }

        private void OnBrowserComposing(TMP_InputField field, string composing)
        {
            if (field == roomNameInput)
            {
                SetRoomNameComposing(composing);
            }
        }

        private void SetRoomNameComposing(string composing)
        {
            if (roomNameInput == null || !roomNameInput.isFocused)
            {
                return;
            }

            // Seeing the IME idle, even when nothing else changed, is what
            // lets the same syllable be dropped again the next time it starts.
            if (composing.Length == 0)
            {
                roomNameDroppedComposing = string.Empty;
            }

            if (string.Equals(composing, roomNameComposing, StringComparison.Ordinal))
            {
                return;
            }

            roomNameComposing = composing;
            UpdateRoomNameCounter();

            // A syllable started in a box that is already full is the one the
            // limit is about to refuse, so it is ended now rather than left
            // drawn on the end until focus moves. Once only per syllable: if
            // it is still there after the drop, leaving it is better than
            // taking focus away every other frame.
            if (composing.Length > 0
                && roomNameInput.text.Length >= HomeStyle.CreateRoom.MaxTitleLength
                && !string.Equals(composing, roomNameDroppedComposing, StringComparison.Ordinal))
            {
                roomNameDroppedComposing = composing;
                DropRoomNameComposition();
            }
        }

        /// <summary>
        /// Ends the syllable being built the way a click outside the box does,
        /// then puts focus back a frame later.
        /// </summary>
        /// <remarks>
        /// Deselecting is the one path known to leave the IME clean here: the
        /// field commits what was being built, the limit refuses it, and the
        /// IME is stood down along with the field. Switching the IME off
        /// directly instead left the half-built syllable cached, and the
        /// field drew it in every box that opened afterwards, undeletable
        /// because it was never text.
        /// </remarks>
        private void DropRoomNameComposition()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || roomNameRefocusPending)
            {
                return;
            }

            roomNameRefocusPending = true;
            eventSystem.SetSelectedGameObject(null);
        }

        /// <summary>
        /// Listens to the browser's field only while the box has focus. The
        /// keyboard side needs no listener: it is polled while focused.
        /// </summary>
        private void WatchRoomNameComposition(bool watching)
        {
            roomNameComposing = string.Empty;
            WebTextInput.ComposingChanged -= OnBrowserComposing;
            if (watching)
            {
                WebTextInput.ComposingChanged += OnBrowserComposing;
            }

            UpdateRoomNameCounter();
        }

        private void StepPlayerCount(int step)
        {
            playerCount = Mathf.Clamp(
                playerCount + step,
                HomeStyle.CreateRoom.MinPlayers,
                HomeStyle.CreateRoom.MaxPlayers);
            UpdatePlayerCount();
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null)
            {
                playerCountText.text = playerCount.ToString();
            }

            SetStepperEnabled(decreaseButton, playerCount > HomeStyle.CreateRoom.MinPlayers);
            SetStepperEnabled(increaseButton, playerCount < HomeStyle.CreateRoom.MaxPlayers);
        }

        private static void SetStepperEnabled(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabled;
            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = enabled
                    ? HomeStyle.Palette.TextPrimary
                    : HomeStyle.Palette.SegmentOffLabel;
            }
        }

        /// <summary>
        /// A title needs one character that is not a space.
        /// </summary>
        /// <remarks>
        /// Spaces inside a name are fine, but a name made only of them is not
        /// a name: two such rooms are indistinguishable in the list, and
        /// <c>RoomCreateRequest</c> refuses one anyway. Leaving the button lit
        /// for it would fail after the modal had already closed.
        /// </remarks>
        private void UpdateCreateEnabled()
        {
            var typed = roomNameInput != null ? roomNameInput.text : string.Empty;
            var enabled = !string.IsNullOrWhiteSpace(typed);

            if (createRoomButton == null)
            {
                return;
            }

            createRoomButton.interactable = enabled;
            createRoomFill.color = enabled
                ? HomeStyle.Palette.ApplyOnFill
                : HomeStyle.Palette.ApplyOffFill;
            createRoomLabel.color = enabled
                ? HomeStyle.Palette.ApplyOnLabel
                : HomeStyle.Palette.ApplyOffLabel;
        }

        private void OnCreateRoomClicked()
        {
            var title = roomNameInput != null ? roomNameInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            RoomCreationRequested?.Invoke(title, isPublicRoom, playerCount);
        }
    }
}
