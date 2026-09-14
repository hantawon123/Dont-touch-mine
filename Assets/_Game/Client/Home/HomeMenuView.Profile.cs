using System;
using Game.Core.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// The nickname panel: the search-allow toggle, the name field, and the
    /// apply button under them.
    /// </summary>
    /// <remarks>
    /// Kept apart from the rest of the screen because the rules are its own.
    /// What may be typed, how long it may be and when apply lights up are all
    /// decided here; the presenter is told only when the player asks for
    /// something that leaves the screen.
    /// </remarks>
    public sealed partial class HomeMenuView
    {
        /// <summary>
        /// Raised when the search-allow toggle is flipped, with its new state.
        /// </summary>
        public event Action<bool> NicknameSearchAllowedChanged;

        private void CreateProfileSettingsRoot(RectTransform canvas)
        {
            var root = CreateRect("ProfileSettingsRoot", canvas);
            root.gameObject.SetActive(false);
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // The panel has no close button, so the rest of the screen is the
            // way out. Barely-there rather than fully clear: a transparent
            // graphic takes no clicks, and Home must not be dimmed behind it.
            var dismissRect = CreateRect("DismissArea", root);
            SetAnchor(dismissRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            dismissRect.offsetMin = Vector2.zero;
            dismissRect.offsetMax = Vector2.zero;
            var dismissImage = AddImage(
                dismissRect, new Color(0f, 0f, 0f, 0.01f), raycastTarget: true);
            var dismiss = dismissRect.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = dismissImage;
            dismiss.transition = Selectable.Transition.None;
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
            dismiss.onClick.AddListener(() => ProfileSettingsDismissed?.Invoke());
            menuButtons.Add(dismiss);

            var panel = CreateRect("ProfileSettingsPanel", root);
            SetAnchor(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            panel.anchoredPosition = new Vector2(
                -HomeStyle.Profile.PanelRightMargin, HomeStyle.Profile.PanelBottomMargin);
            panel.sizeDelta = HomeStyle.Profile.PanelSize;
            profilePanel = panel;

            var fill = AddImage(
                panel,
                HomeStyle.Palette.PanelFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Panel, SquareCorner.BottomRight),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            CreateSearchAllowToggle(panel);
            CreateNicknameField(panel);
            CreateNicknameMessageRow(panel);
            CreateApplyButton(panel);

            profileSettingsRoot = root.gameObject;
            SetNicknameSearchAllowed(false);
            ClearNicknameMessage();
            UpdateNicknameApplyEnabled();
        }

        private void CreateSearchAllowToggle(RectTransform panel)
        {
            var label = CreateRect("SearchAllowLabel", panel);
            SetAnchor(label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            label.anchoredPosition = new Vector2(
                HomeStyle.Profile.SidePadding, HomeStyle.Profile.ToggleRowCentreY);
            label.sizeDelta = new Vector2(HomeStyle.Profile.ToggleLeft, 30f);
            var labelText = AddText(
                label,
                "닉네임 검색 허용",
                HomeStyle.FontSize.ToggleLabel,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            ApplyMenuFont(labelText);
            labelText.color = HomeStyle.Palette.TextPrimary;

            var toggle = CreateRect("SearchAllowToggle", panel);
            SetAnchor(toggle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            toggle.anchoredPosition = new Vector2(
                HomeStyle.Profile.ToggleLeft, HomeStyle.Profile.ToggleRowCentreY);
            toggle.sizeDelta = HomeStyle.Profile.ToggleSize;

            searchAllowFill = AddImage(
                toggle,
                HomeStyle.Palette.ToggleOffFill,
                HomeUiFonts.PillSprite,
                raycastTarget: true);
            searchAllowFill.type = Image.Type.Sliced;
            searchAllowFill.pixelsPerUnitMultiplier = 1f;

            var strokeRect = CreateRect("Stroke", toggle);
            SetAnchor(strokeRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            strokeRect.offsetMin = Vector2.zero;
            strokeRect.offsetMax = Vector2.zero;
            var stroke = AddImage(
                strokeRect,
                HomeStyle.Palette.ToggleStroke,
                HomeUiFonts.Outline(
                    Mathf.RoundToInt(HomeStyle.Profile.ToggleSize.y * 0.5f),
                    HomeStyle.Profile.ToggleStrokeThickness));
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = 1f;

            var knobDiameter = HomeStyle.Profile.ToggleSize.y
                - (HomeStyle.Profile.ToggleKnobInset * 2f);
            var knob = CreateRect("Knob", toggle);
            SetAnchor(knob, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            knob.sizeDelta = new Vector2(knobDiameter, knobDiameter);
            searchAllowKnob = knob;
            searchAllowKnobImage = AddImage(
                knob, HomeStyle.Palette.ToggleOffKnob, HomeUiFonts.CircleSprite);

            var button = toggle.gameObject.AddComponent<Button>();
            button.targetGraphic = searchAllowFill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(ToggleSearchAllowed);
            menuButtons.Add(button);

            // To the right of the toggle, on its row. Empty unless the server
            // refused, so it costs nothing when everything works.
            var message = CreateRect("SearchAllowMessage", panel);
            SetAnchor(message, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            message.anchoredPosition = new Vector2(
                -HomeStyle.Profile.SidePadding, HomeStyle.Profile.ToggleRowCentreY);
            message.sizeDelta = new Vector2(
                HomeStyle.Profile.PanelSize.x
                    - HomeStyle.Profile.ToggleLeft
                    - HomeStyle.Profile.ToggleSize.x
                    - (HomeStyle.Profile.SidePadding * 2f),
                HomeStyle.Profile.MessageHeight);
            searchAllowMessageText = AddText(
                message,
                string.Empty,
                HomeStyle.FontSize.Message,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineRight);
            ApplyMenuFont(searchAllowMessageText);
            searchAllowMessageText.color = HomeStyle.Palette.MessageRejected;
        }

        /// <summary>
        /// Says the server would not take the new setting. An empty message
        /// clears it.
        /// </summary>
        public void SetNicknameSearchAllowedError(string message)
        {
            if (searchAllowMessageText == null)
            {
                return;
            }

            searchAllowMessageText.text = message ?? string.Empty;
        }

        private void CreateNicknameField(RectTransform panel)
        {
            var field = CreateRect("NicknameField", panel);
            SetAnchor(field, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            field.anchoredPosition = new Vector2(
                HomeStyle.Profile.SidePadding, HomeStyle.Profile.InputTop);
            field.sizeDelta = HomeStyle.Profile.InputSize;

            var background = AddImage(
                field,
                HomeStyle.Palette.InputFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Input),
                raycastTarget: true);
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;

            var viewport = CreateRect("TextArea", field);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = new Vector2(HomeStyle.Profile.InputTextPadding, 4f);
            viewport.offsetMax = new Vector2(-HomeStyle.Profile.InputTextPadding, -4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var textRect = CreateRect("Text", viewport);
            SetAnchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = AddText(
                textRect,
                string.Empty,
                HomeStyle.FontSize.NicknameInput,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                raycastTarget: true);
            text.color = HomeStyle.Palette.TextPrimary;

            // TMP_InputField reads its parts as it wakes, so the object is kept
            // switched off until every one of them is in place.
            field.gameObject.SetActive(false);
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.fontAsset = koreanFont;
            input.pointSize = HomeStyle.FontSize.NicknameInput;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = HomeStyle.Palette.TextPrimary;
            input.selectionColor = HomeStyle.Palette.Accent;

            // The limit is enforced below rather than by characterLimit, which
            // drops the extra keystroke without a word about why.
            input.characterLimit = 0;
            input.onValueChanged.AddListener(OnNicknameEdited);
            field.gameObject.SetActive(true);
            profileNicknameInput = input;
        }

        /// <summary>
        /// The row under the field: why the name was refused on the left, how
        /// many characters have been typed on the right.
        /// </summary>
        private void CreateNicknameMessageRow(RectTransform panel)
        {
            var messageRect = CreateRect("NicknameMessage", panel);
            SetAnchor(messageRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            messageRect.anchoredPosition = new Vector2(
                HomeStyle.Profile.TextInset, HomeStyle.Profile.MessageTop);
            messageRect.sizeDelta = new Vector2(
                HomeStyle.Profile.InputSize.x * 0.65f, HomeStyle.Profile.MessageHeight);
            nicknameMessageText = AddText(
                messageRect,
                string.Empty,
                HomeStyle.FontSize.Message,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

            var counterRect = CreateRect("NicknameCounter", panel);
            SetAnchor(counterRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            counterRect.anchoredPosition = new Vector2(
                -HomeStyle.Profile.TextInset, HomeStyle.Profile.MessageTop);
            counterRect.sizeDelta = new Vector2(120f, HomeStyle.Profile.MessageHeight);
            nicknameCounterText = AddText(
                counterRect,
                $"0/{NicknamePolicy.MaxLength}",
                HomeStyle.FontSize.Counter,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineRight);
            nicknameCounterText.color = HomeStyle.Palette.Counter;
        }

        private void CreateApplyButton(RectTransform panel)
        {
            var apply = CreateRect("ApplyButton", panel);
            SetAnchor(apply, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            apply.anchoredPosition = new Vector2(
                HomeStyle.Profile.SidePadding, HomeStyle.Profile.ApplyTop);
            apply.sizeDelta = HomeStyle.Profile.ApplySize;

            applyFill = AddImage(
                apply,
                HomeStyle.Palette.ApplyOffFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Input),
                raycastTarget: true);
            applyFill.type = Image.Type.Sliced;
            applyFill.pixelsPerUnitMultiplier = 1f;

            var labelRect = CreateRect("Label", apply);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            applyLabel = AddText(
                labelRect,
                "적용하기",
                HomeStyle.FontSize.Apply,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            ApplyMenuFont(applyLabel);

            applyButton = apply.gameObject.AddComponent<Button>();
            applyButton.targetGraphic = applyFill;
            applyButton.transition = Selectable.Transition.None;
            applyButton.onClick.AddListener(OnApplyClicked);
            menuButtons.Add(applyButton);
            SetNicknameApplyEnabled(false);

            CreateConfirmRow(panel);
        }

        /// <summary>
        /// The second press: cancel on the left, where apply used to be, and
        /// confirm beside it.
        /// </summary>
        /// <remarks>
        /// The order is deliberate. People double-click buttons, and a change
        /// that cannot be undone must not be reachable by the second half of a
        /// double-click. Putting cancel under the cursor makes that stray press
        /// harmless.
        /// </remarks>
        private void CreateConfirmRow(RectTransform panel)
        {
            var row = CreateRect("ConfirmRow", panel);
            SetAnchor(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            row.anchoredPosition = new Vector2(
                HomeStyle.Profile.SidePadding, HomeStyle.Profile.ApplyTop);
            row.sizeDelta = HomeStyle.Profile.ApplySize;
            confirmRow = row.gameObject;

            var half = (HomeStyle.Profile.ApplySize.x - HomeStyle.Profile.ConfirmGap) * 0.5f;

            CreateConfirmHalf(
                row, "Cancel", "취소", 0f, half,
                HomeStyle.Palette.ApplyOffFill, HomeStyle.Palette.ApplyOffLabel,
                CancelNicknameConfirm);

            CreateConfirmHalf(
                row, "Confirm", "변경",
                half + HomeStyle.Profile.ConfirmGap, half,
                HomeStyle.Palette.ApplyOnFill, HomeStyle.Palette.ApplyOnLabel,
                ConfirmNicknameChange);

            row.gameObject.SetActive(false);
        }

        private void CreateConfirmHalf(
            RectTransform row,
            string name,
            string label,
            float left,
            float width,
            Color fillColour,
            Color labelColour,
            Action onClicked)
        {
            var half = CreateRect(name, row);
            SetAnchor(half, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            half.anchoredPosition = new Vector2(left, 0f);
            half.sizeDelta = new Vector2(width, 0f);

            var fill = AddImage(
                half,
                fillColour,
                HomeUiFonts.Rounded(HomeStyle.Radius.Input),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            var labelRect = CreateRect("Label", half);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = AddText(
                labelRect,
                label,
                HomeStyle.FontSize.Apply,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            ApplyMenuFont(text);
            text.color = labelColour;

            var button = half.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClicked());
            menuButtons.Add(button);
        }

        private void OnApplyClicked()
        {
            var typed = profileNicknameInput != null
                ? profileNicknameInput.text
                : string.Empty;

            // Half-composed Hangul is allowed into the field so a Korean
            // keyboard can build a syllable; it must not leave it.
            if (!NicknamePolicy.IsValid(typed))
            {
                ShowNicknameMessage(
                    HomeStyle.Profile.BadCharacterMessage, HomeStyle.Palette.MessageRejected);
                return;
            }

            SetNicknameConfirming(true);
            ShowNicknameMessage(
                $"\"{typed}\"로 정할까요? 되돌릴 수 없어요",
                HomeStyle.Palette.MessageRejected);
        }

        private void CancelNicknameConfirm()
        {
            SetNicknameConfirming(false);
            ClearNicknameMessage();
        }

        private void ConfirmNicknameChange()
        {
            SetNicknameConfirming(false);
            NicknameChangeRequested?.Invoke(
                profileNicknameInput != null ? profileNicknameInput.text : string.Empty);
        }

        private void SetNicknameConfirming(bool confirming)
        {
            isConfirmingNickname = confirming;
            if (applyButton != null)
            {
                // Once the change is spent there is no apply row to come back
                // to, so cancelling a confirm must not bring the button back.
                applyButton.gameObject.SetActive(!confirming && !currentNicknameSet);
            }

            if (confirmRow != null)
            {
                confirmRow.SetActive(confirming);
            }
        }

        public void SetNicknameSearchAllowed(bool allowed)
        {
            isSearchAllowed = allowed;
            if (searchAllowFill == null || searchAllowKnob == null)
            {
                return;
            }

            searchAllowFill.color = allowed
                ? HomeStyle.Palette.ToggleOnFill
                : HomeStyle.Palette.ToggleOffFill;
            searchAllowKnobImage.color = allowed
                ? HomeStyle.Palette.ToggleOnKnob
                : HomeStyle.Palette.ToggleOffKnob;

            var travel = HomeStyle.Profile.ToggleSize.x
                - searchAllowKnob.sizeDelta.x
                - (HomeStyle.Profile.ToggleKnobInset * 2f);
            searchAllowKnob.anchoredPosition = new Vector2(
                HomeStyle.Profile.ToggleKnobInset + (allowed ? travel : 0f), 0f);
        }

        /// <summary>
        /// Asks for the other setting. The knob does not move yet.
        /// </summary>
        /// <remarks>
        /// Moving it here and putting it back on a refusal makes the toggle
        /// flick, and on a slow connection it sits in the wrong position for as
        /// long as the call takes. The server owns this value, so the knob
        /// waits for <see cref="SetNicknameSearchAllowed"/> to carry its answer.
        /// </remarks>
        private void ToggleSearchAllowed()
        {
            SetNicknameSearchAllowedError(string.Empty);
            NicknameSearchAllowedChanged?.Invoke(!isSearchAllowed);
        }

        private void SetNicknameApplyEnabled(bool enabled)
        {
            if (applyButton == null)
            {
                return;
            }

            applyButton.interactable = enabled;
            applyFill.color = enabled
                ? HomeStyle.Palette.ApplyOnFill
                : HomeStyle.Palette.ApplyOffFill;
            applyLabel.color = enabled
                ? HomeStyle.Palette.ApplyOnLabel
                : HomeStyle.Palette.ApplyOffLabel;
        }

        /// <summary>
        /// Filters what was typed and reports on it, without letting anything
        /// the rules forbid stay in the field.
        /// </summary>
        /// <remarks>
        /// Rejecting by rewriting the field re-enters this handler, so the
        /// second pass is skipped rather than allowed to fight the first.
        /// </remarks>
        private void OnNicknameEdited(string value)
        {
            if (isRewritingNickname)
            {
                return;
            }

            var accepted = NicknamePolicy.Filter(
                value, out var hadBadCharacter, out var wasTooLong);
            if (accepted != value)
            {
                isRewritingNickname = true;
                profileNicknameInput.text = accepted;
                profileNicknameInput.caretPosition = accepted.Length;
                isRewritingNickname = false;
            }

            // Editing takes back a pending confirmation: what was about to be
            // agreed to is no longer what the field says.
            SetNicknameConfirming(false);

            if (hadBadCharacter)
            {
                ShowNicknameMessage(
                    HomeStyle.Profile.BadCharacterMessage, HomeStyle.Palette.MessageRejected);
            }
            else if (wasTooLong)
            {
                ShowNicknameMessage(
                    HomeStyle.Profile.TooLongMessage, HomeStyle.Palette.MessageRejected);
            }
            else
            {
                ClearNicknameMessage();
            }

            UpdateNicknameCounter(accepted);
            UpdateNicknameApplyEnabled();
            NicknameEdited?.Invoke(accepted);
        }

        private void UpdateNicknameCounter(string value)
        {
            if (nicknameCounterText != null)
            {
                nicknameCounterText.text =
                    $"{value.Length}/{NicknamePolicy.MaxLength}";
            }
        }

        private void ShowNicknameMessage(string message, Color color)
        {
            if (nicknameMessageText == null)
            {
                return;
            }

            nicknameMessageText.text = message;
            nicknameMessageText.color = color;
        }

        private void ClearNicknameMessage()
        {
            ShowNicknameMessage(
                currentNicknameSet
                    ? HomeStyle.Profile.AlreadySetMessage
                    : HomeStyle.Profile.OneChangeMessage,
                HomeStyle.Palette.Counter);
        }

        /// <summary>
        /// Says why a rename did not happen. An empty message clears the line.
        /// </summary>
        /// <remarks>
        /// The server is the one that knows: the panel lets a name through on
        /// its own rules and <c>HomeProfileBridge</c> reports back what the
        /// account said.
        /// </remarks>
        public void SetNicknameError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                ClearNicknameMessage();
                return;
            }

            ShowNicknameMessage(message, HomeStyle.Palette.MessageRejected);
        }

        /// <summary>
        /// Locks the panel once the one change has been spent.
        /// </summary>
        /// <remarks>
        /// The field is switched off rather than hidden: the player should see
        /// the name they settled on, and that it can no longer be edited.
        /// <para>
        /// The apply button and the character counter go, though. Both are
        /// about typing a new name, and a greyed-out button under a field that
        /// cannot be edited reads as something the player has yet to unlock.
        /// The panel shrinks to the message line so it does not keep the empty
        /// row.
        /// </para>
        /// </remarks>
        public void SetNicknameSettled(bool settled)
        {
            currentNicknameSet = settled;
            if (profileNicknameInput != null)
            {
                profileNicknameInput.interactable = !settled;
            }

            if (settled)
            {
                SetNicknameConfirming(false);
            }

            if (applyButton != null)
            {
                applyButton.gameObject.SetActive(!settled && !isConfirmingNickname);
            }

            if (nicknameCounterText != null)
            {
                nicknameCounterText.gameObject.SetActive(!settled);
            }

            if (profilePanel != null)
            {
                profilePanel.sizeDelta = new Vector2(
                    HomeStyle.Profile.PanelSize.x,
                    settled ? HomeStyle.Profile.SettledPanelHeight : HomeStyle.Profile.PanelSize.y);
            }

            ClearNicknameMessage();
            UpdateNicknameApplyEnabled();
        }

        /// <summary>
        /// Apply is open for any name the rule allows that is not the one
        /// already in use, and only while the change is still available.
        /// </summary>
        /// <remarks>
        /// Excluding the current name matters: asking whether your own nickname
        /// is taken comes back yes, and the panel would answer a press with
        /// "이미 존재하는 닉네임입니다" about the name you already own.
        /// </remarks>
        private void UpdateNicknameApplyEnabled()
        {
            if (currentNicknameSet)
            {
                SetNicknameApplyEnabled(false);
                return;
            }

            var typed = profileNicknameInput != null
                ? profileNicknameInput.text
                : string.Empty;

            SetNicknameApplyEnabled(
                NicknamePolicy.IsValid(typed)
                && !string.Equals(typed, currentNickname, StringComparison.Ordinal));
        }
    }
}
