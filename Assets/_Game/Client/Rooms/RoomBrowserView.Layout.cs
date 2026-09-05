using System.Collections.Generic;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Builds the room browser screen from the 0904 wire-frame.
    /// </summary>
    /// <remarks>
    /// The screen is assembled in code rather than authored in the scene. Unity
    /// scenes and prefabs do not merge, and three people work this screen in
    /// sequence, so an authored hierarchy turns every hand-off into a conflict
    /// nobody can resolve by reading the diff. The same reason put the home menu
    /// in <see cref="Home.HomeMenuView"/> together with its layout.
    /// <para>
    /// Nothing here decides behaviour. The code panel is drawn and left inert:
    /// what a typed code does belongs to the story that wires it, and building
    /// its shape now means that story only has to fill in the reactions.
    /// </para>
    /// </remarks>
    public sealed partial class RoomBrowserView
    {
        [Header("Art")]
        [SerializeField]
        private Sprite backgroundSprite;

        [SerializeField]
        private Sprite searchIcon;

        [SerializeField]
        private Sprite refreshIcon;

        [Header("Fonts")]
        [SerializeField]
        private TMP_FontAsset mediumFont;

        [SerializeField]
        private TMP_FontAsset semiBoldFont;

        [SerializeField]
        private TMP_FontAsset boldFont;

        private ScrollRect roomScroll;
        private CanvasGroup refreshGroup;
        private CanvasGroup listGroup;
        private CanvasGroup toastGroup;
        private TMP_Text toastBody;
        private float toastHidesAt;
        private TMP_Text emptyStateText;
        private Image enterButtonBackground;
        private TMP_Text enterButtonLabel;
        private Button enterButton;

        private readonly List<TMP_Text> codeCellTexts = new List<TMP_Text>();

        /// <summary>
        /// One field behind all six cells rather than six fields.
        /// </summary>
        /// <remarks>
        /// Typing, backspace, holding a key down, pasting six characters at
        /// once and selecting them all are things a text field already does.
        /// Six fields would mean moving focus by hand on every one of those, and
        /// getting one of them wrong is a code that cannot be typed.
        /// </remarks>
        private TMP_InputField codeInput;

        /// <summary>
        /// Builds the whole screen under this object. Called first thing in
        /// <c>Awake</c>, because everything the view subscribes to is created
        /// here.
        /// </summary>
        private void BuildLayout()
        {
            EnsureEventSystem();
            ConfigureCanvas();

            var root = transform as RectTransform;
            if (root == null)
            {
                throw new MissingComponentException(
                    "RoomBrowserView draws itself and has to sit on a "
                    + "RectTransform under a Canvas.");
            }

            root.Stretch();

            BuildBackground(root);
            BuildBackButton(root);
            BuildCodePanel(root);
            BuildRoomListPanel(root);

            // Built last so it draws over the panels it reports about.
            BuildToast(root);
        }

        /// <summary>
        /// The desk photograph is envelope-fitted rather than stretched: a
        /// window that is not 16:9 should crop the desk, not squash the props
        /// on it.
        /// </summary>
        private void BuildBackground(RectTransform parent)
        {
            var rect = RoomBrowserUi.CreateRect("Background", parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = RoomBrowserStyle.ReferenceResolution;
            rect.SetAsFirstSibling();

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = backgroundSprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.color = backgroundSprite != null
                ? Color.white
                : RoomBrowserStyle.FromHex(0x0B1018);

            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = backgroundSprite != null && backgroundSprite.rect.height > 0f
                ? backgroundSprite.rect.width / backgroundSprite.rect.height
                : RoomBrowserStyle.ReferenceResolution.x / RoomBrowserStyle.ReferenceResolution.y;
        }

        private void BuildBackButton(RectTransform parent)
        {
            var rect = RoomBrowserUi.CreateRect("BackButton", parent).Anchor(
                new Vector2(0f, 1f),
                RoomBrowserStyle.Layout.BackButtonPosition,
                RoomBrowserStyle.Layout.BackButtonSize);

            // Transparent, but present: the label alone would leave the padding
            // around it dead to the pointer.
            var hitbox = rect.gameObject.AddComponent<Image>();
            hitbox.color = Color.clear;

            var label = RoomBrowserUi.CreateText(
                "Label",
                rect,
                ResolveFont(semiBoldFont),
                RoomBrowserStyle.FontSize.Back,
                Color.white,
                TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Stretch();
            label.text = "← 이전";
            label.raycastTarget = false;

            backButton = rect.gameObject.AddComponent<Button>();
            Tint(backButton, label, RoomBrowserStyle.Palette.TextPrimary);
        }

        /// <summary>
        /// The code panel as the mock-up draws it: six empty cells and a button
        /// that stays disabled until all six are filled. Filling them is the
        /// room-code story's work.
        /// </summary>
        private void BuildCodePanel(RectTransform parent)
        {
            var panel = RoomBrowserUi.CreateImage(
                "CodePanel",
                parent,
                RoomBrowserStyle.Palette.PanelFill,
                RoomBrowserUi.Rounded(RoomBrowserStyle.Radius.CodePanel));

            panel.rectTransform.Anchor(
                new Vector2(0f, 0.5f),
                RoomBrowserStyle.Layout.CodePanelPosition,
                RoomBrowserStyle.Layout.CodePanelSize);

            var title = RoomBrowserUi.CreateText(
                "Title",
                panel.transform,
                ResolveFont(semiBoldFont),
                RoomBrowserStyle.FontSize.CodeTitle,
                RoomBrowserStyle.Palette.TextPrimary,
                TextAlignmentOptions.Center);
            title.text = "방 코드로 입장";
            title.rectTransform.Anchor(
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, RoomBrowserStyle.Layout.CodeTitleOffsetY),
                new Vector2(RoomBrowserStyle.Layout.CodePanelSize.x, 32f));

            BuildCodeCells(panel.rectTransform);
            BuildEnterButton(panel.rectTransform);

            // Anywhere on the panel puts the caret in the code, not just the
            // hidden field behind the six cells. Handing focus over from the
            // search box is otherwise a click that lands on nothing.
            var focus = panel.gameObject.AddComponent<Button>();
            focus.transition = Selectable.Transition.None;
            focus.navigation = new Navigation { mode = Navigation.Mode.None };
            focus.onClick.AddListener(FocusCodeInput);
        }

        private void BuildCodeCells(RectTransform panel)
        {
            var cellSize = RoomBrowserStyle.Layout.CodeCellSize;
            var spacing = RoomBrowserStyle.Layout.CodeCellSpacing;
            var count = RoomBrowserStyle.Layout.CodeCellCount;
            var width = (cellSize * count) + (spacing * (count - 1));

            var row = RoomBrowserUi.CreateRect("CodeCells", panel).Anchor(
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, RoomBrowserStyle.Layout.CodeCellsOffsetY),
                new Vector2(width, cellSize));

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            codeCellTexts.Clear();

            BuildCodeInput(row);

            for (var index = 0; index < count; index++)
            {
                var cell = RoomBrowserUi.CreateImage(
                    $"Cell{index}",
                    row,
                    RoomBrowserStyle.Palette.CodeCellFill,
                    RoomBrowserUi.Rounded(RoomBrowserStyle.Radius.CodeCell));

                // A cell that took the click would stop it reaching the field
                // underneath, which is the thing that actually types.
                cell.raycastTarget = false;

                var element = cell.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = cellSize;
                element.preferredHeight = cellSize;

                var character = RoomBrowserUi.CreateText(
                    "Character",
                    cell.transform,
                    ResolveFont(boldFont),
                    RoomBrowserStyle.FontSize.CodeCell,
                    RoomBrowserStyle.Palette.CodeCellText,
                    TextAlignmentOptions.Center);
                character.rectTransform.Stretch();
                character.text = string.Empty;

                codeCellTexts.Add(character);
            }
        }

        /// <summary>
        /// The invisible field the cells show. It covers them, so a click
        /// anywhere on the row puts the caret in it.
        /// </summary>
        private void BuildCodeInput(RectTransform row)
        {
            var fieldRect = RoomBrowserUi.CreateRect("CodeInput", row).Stretch();

            // Behind the cells in the hierarchy but above nothing: it needs a
            // graphic to be clickable, and a clear one leaves the cells visible.
            fieldRect.SetAsFirstSibling();

            // The row lays its children out as cells, and this is not one. Left
            // in, it would be spaced like a seventh cell and squeeze the six.
            var ignore = fieldRect.gameObject.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;
            var background = fieldRect.gameObject.AddComponent<Image>();
            background.color = Color.clear;

            var textArea = RoomBrowserUi.CreateRect("TextArea", fieldRect).Stretch();
            textArea.gameObject.AddComponent<RectMask2D>();

            var text = RoomBrowserUi.CreateText(
                "Text",
                textArea,
                ResolveFont(boldFont),
                RoomBrowserStyle.FontSize.CodeCell,
                Color.clear,
                TextAlignmentOptions.Center);
            text.rectTransform.Stretch();

            fieldRect.gameObject.SetActive(false);
            codeInput = fieldRect.gameObject.AddComponent<TMP_InputField>();
            codeInput.textViewport = textArea;
            codeInput.textComponent = text;
            codeInput.targetGraphic = background;
            codeInput.fontAsset = ResolveFont(boldFont);
            codeInput.pointSize = RoomBrowserStyle.FontSize.CodeCell;
            codeInput.lineType = TMP_InputField.LineType.SingleLine;
            codeInput.richText = false;
            codeInput.characterLimit = RoomCodeFormat.CodeLength;

            // The cells draw the characters, so the field's own caret and
            // selection would be a second cursor on the same text.
            codeInput.caretWidth = 0;
            codeInput.customCaretColor = true;
            codeInput.caretColor = Color.clear;
            codeInput.selectionColor = Color.clear;

            // A room code is letters and digits, and a Korean keyboard hands
            // over composed syllables that are neither. They get filtered out
            // one by one, so typing looks broken while pasting works. Turning
            // the IME off while this field has focus is what makes the keys
            // arrive as the characters printed on them.
            codeInput.onSelect.AddListener(_ => SetImeEnabled(false));
            codeInput.onDeselect.AddListener(_ => SetImeEnabled(true));
            codeInput.onValidateInput += AcceptCodeCharacter;
            codeInput.onValueChanged.AddListener(OnCodeChanged);
            codeInput.onSubmit.AddListener(_ => SubmitCode());
            fieldRect.gameObject.SetActive(true);
        }

        /// <summary>
        /// Puts the caret at the end of whatever has been typed, so a click on
        /// the panel carries on rather than starting over.
        /// </summary>
        private void FocusCodeInput()
        {
            if (codeInput == null)
            {
                return;
            }

            codeInput.Select();
            codeInput.ActivateInputField();
            codeInput.caretPosition = codeInput.text.Length;
        }

        /// <summary>
        /// Leaves the keyboard as it was found. The player types Korean
        /// everywhere else on this screen, including the search box beside it.
        /// </summary>
        private static void SetImeEnabled(bool enabled)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboard.SetIMEEnabled(enabled);
            }
        }

        /// <summary>
        /// Keeps a typed character only if a code could contain it, in the case
        /// codes are issued in. Lower case is raised rather than refused: a code
        /// read aloud and typed in lower case is the same code.
        /// </summary>
        private static char AcceptCodeCharacter(string text, int index, char character)
        {
            var raised = char.ToUpperInvariant(character);
            return RoomCodeFormat.IsAllowed(raised) ? raised : ' ';
        }

        private void OnCodeChanged(string code)
        {
            for (var index = 0; index < codeCellTexts.Count; index++)
            {
                codeCellTexts[index].text = index < code.Length
                    ? code[index].ToString()
                    : string.Empty;
            }

            SetCodeEntryEnabled(code.Length == RoomCodeFormat.CodeLength);
        }

        /// <summary>
        /// Sends the code on. Six characters that all passed validation are
        /// well formed by construction, so the check is on length alone.
        /// </summary>
        private void SubmitCode()
        {
            if (codeInput == null ||
                codeInput.text.Length != RoomCodeFormat.CodeLength)
            {
                return;
            }

            lastEntrySource = RoomEntrySource.RoomCode;
            RoomCodeEntered?.Invoke(codeInput.text);
        }

        private void BuildEnterButton(RectTransform panel)
        {
            enterButtonBackground = RoomBrowserUi.CreateImage(
                "EnterButton",
                panel,
                RoomBrowserStyle.Palette.DisabledFill,
                RoomBrowserUi.Rounded(RoomBrowserStyle.Radius.EnterButton));

            enterButtonBackground.rectTransform.Anchor(
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, RoomBrowserStyle.Layout.EnterButtonOffsetY),
                RoomBrowserStyle.Layout.EnterButtonSize);

            enterButtonLabel = RoomBrowserUi.CreateText(
                "Label",
                enterButtonBackground.transform,
                ResolveFont(semiBoldFont),
                RoomBrowserStyle.FontSize.EnterLabel,
                RoomBrowserStyle.Palette.DisabledLabel,
                TextAlignmentOptions.Center);
            enterButtonLabel.rectTransform.Stretch();
            enterButtonLabel.text = "→ 입장";

            enterButton = enterButtonBackground.gameObject.AddComponent<Button>();
            enterButton.targetGraphic = enterButtonBackground;
            enterButton.navigation = new Navigation { mode = Navigation.Mode.None };
            enterButton.onClick.AddListener(SubmitCode);

            SetCodeEntryEnabled(false);
        }

        /// <summary>
        /// Switches the enter button between its filled and empty look. Public
        /// to the class rather than to the screen: only the code entry knows
        /// when six characters are in.
        /// </summary>
        private void SetCodeEntryEnabled(bool enabled)
        {
            if (enterButton == null)
            {
                return;
            }

            enterButton.interactable = enabled;
            enterButtonBackground.color = enabled
                ? RoomBrowserStyle.Palette.Accent
                : RoomBrowserStyle.Palette.DisabledFill;
            enterButtonLabel.color = enabled
                ? RoomBrowserStyle.Palette.TextPrimary
                : RoomBrowserStyle.Palette.DisabledLabel;
        }

        private void BuildRoomListPanel(RectTransform parent)
        {
            var panel = RoomBrowserUi.CreateImage(
                "RoomListPanel",
                parent,
                RoomBrowserStyle.Palette.PanelFill,
                RoomBrowserUi.Rounded(RoomBrowserStyle.Radius.ListPanel));

            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(
                RoomBrowserStyle.Layout.ListPanelWidth,
                -(RoomBrowserStyle.Layout.ListPanelTopMargin
                    + RoomBrowserStyle.Layout.ListPanelBottomMargin));

            // Half the difference between the two margins, which is how far the
            // centre of a top-95 bottom-105 band sits above the middle.
            rect.anchoredPosition = new Vector2(
                -RoomBrowserStyle.Layout.ListPanelRightMargin,
                (RoomBrowserStyle.Layout.ListPanelBottomMargin
                    - RoomBrowserStyle.Layout.ListPanelTopMargin) * 0.5f);

            BuildHeader(rect);
            BuildRoomList(rect);
        }

        private void BuildHeader(RectTransform panel)
        {
            var header = RoomBrowserUi.CreateRect("Header", panel);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, RoomBrowserStyle.Layout.HeaderHeight);

            var icon = RoomBrowserUi.CreateImage(
                "SearchIcon", header, RoomBrowserStyle.Palette.Accent);
            icon.sprite = searchIcon;
            icon.raycastTarget = false;
            icon.rectTransform.Anchor(
                new Vector2(0f, 0.5f),
                new Vector2(RoomBrowserStyle.Layout.HeaderLeftPadding, 0f),
                new Vector2(
                    RoomBrowserStyle.Layout.SearchIconSize,
                    RoomBrowserStyle.Layout.SearchIconSize));

            BuildSearchInput(header);
            BuildRefreshButton(header);
        }

        private void BuildSearchInput(RectTransform header)
        {
            var left = RoomBrowserStyle.Layout.HeaderLeftPadding
                + RoomBrowserStyle.Layout.SearchIconSize
                + RoomBrowserStyle.Layout.SearchIconGap;

            // Stops short of the refresh button with a gap of its own, so a long
            // search term never runs under it.
            var right = RoomBrowserStyle.Layout.RefreshRightPadding
                + RoomBrowserStyle.Layout.RefreshButtonSize.x
                + 20f;

            var fieldRect = RoomBrowserUi.CreateRect("SearchInput", header);
            fieldRect.anchorMin = new Vector2(0f, 0.5f);
            fieldRect.anchorMax = new Vector2(1f, 0.5f);
            fieldRect.pivot = new Vector2(0.5f, 0.5f);
            fieldRect.offsetMin = new Vector2(left, -20f);
            fieldRect.offsetMax = new Vector2(-right, 20f);

            var background = fieldRect.gameObject.AddComponent<Image>();
            background.color = Color.clear;

            var textArea = RoomBrowserUi.CreateRect("TextArea", fieldRect).Stretch();
            textArea.gameObject.AddComponent<RectMask2D>();

            var placeholder = RoomBrowserUi.CreateText(
                "Placeholder",
                textArea,
                ResolveFont(mediumFont),
                RoomBrowserStyle.FontSize.Search,
                RoomBrowserStyle.Palette.TextMuted,
                TextAlignmentOptions.MidlineLeft);
            placeholder.rectTransform.Stretch();
            placeholder.text = "방 이름 입력";

            var text = RoomBrowserUi.CreateText(
                "Text",
                textArea,
                ResolveFont(mediumFont),
                RoomBrowserStyle.FontSize.Search,
                RoomBrowserStyle.Palette.SearchText,
                TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Stretch();

            // TextMeshPro reads its parts when the component wakes, and a
            // component added to a live object wakes at once. Building it while
            // the object is off is what lets the fields be assigned first.
            fieldRect.gameObject.SetActive(false);
            searchInputField = fieldRect.gameObject.AddComponent<TMP_InputField>();
            searchInputField.textViewport = textArea;
            searchInputField.textComponent = text;
            searchInputField.placeholder = placeholder;
            searchInputField.targetGraphic = background;
            searchInputField.fontAsset = ResolveFont(mediumFont);
            searchInputField.pointSize = RoomBrowserStyle.FontSize.Search;
            searchInputField.lineType = TMP_InputField.LineType.SingleLine;
            searchInputField.richText = false;
            searchInputField.characterLimit = 20;
            searchInputField.caretColor = RoomBrowserStyle.Palette.Accent;
            searchInputField.customCaretColor = true;
            // Whatever the code panel did to the keyboard, the search box gets
            // it back: room names are typed in Korean.
            searchInputField.onSelect.AddListener(_ => SetImeEnabled(true));
            fieldRect.gameObject.SetActive(true);
        }

        private void BuildRefreshButton(RectTransform header)
        {
            var button = RoomBrowserUi.CreateImage(
                "RefreshButton",
                header,
                RoomBrowserStyle.Palette.RefreshFill,
                RoomBrowserUi.Rounded(RoomBrowserStyle.Radius.RefreshButton));

            button.rectTransform.Anchor(
                new Vector2(1f, 0.5f),
                new Vector2(-RoomBrowserStyle.Layout.RefreshRightPadding, 0f),
                RoomBrowserStyle.Layout.RefreshButtonSize);

            var content = RoomBrowserUi.CreateRect("Content", button.transform).Stretch();
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = RoomBrowserStyle.Layout.RefreshIconGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            refreshGroup = button.gameObject.AddComponent<CanvasGroup>();

            var icon = RoomBrowserUi.CreateImage(
                "Icon", content, RoomBrowserStyle.Palette.Accent);
            icon.sprite = refreshIcon;
            icon.raycastTarget = false;
            var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = RoomBrowserStyle.Layout.RefreshIconSize;
            iconLayout.preferredHeight = RoomBrowserStyle.Layout.RefreshIconSize;

            var label = RoomBrowserUi.CreateText(
                "Label",
                content,
                ResolveFont(semiBoldFont),
                RoomBrowserStyle.FontSize.Refresh,
                RoomBrowserStyle.Palette.RefreshLabel,
                TextAlignmentOptions.Midline);
            label.text = "새로고침";

            refreshButton = button.gameObject.AddComponent<Button>();
            refreshButton.targetGraphic = button;
            refreshButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        /// <summary>
        /// How long the refresh button stays locked after a press.
        /// </summary>
        /// <remarks>
        /// Matchmaking answers faster than this, so the wait is not about the
        /// request finishing. It is about how often the list is worth asking for:
        /// a player holding the button down would send a request a frame, and
        /// each one is a round trip to the cloud that returns much the same list.
        /// </remarks>
        private const float RefreshCooldownSeconds = 5f;

        /// <summary>
        /// Unscaled, because a paused game must not freeze the wait.
        /// </summary>
        private float refreshReadyAt;
        private bool isRefreshBusy;

        /// <summary>
        /// Stops a second room being picked while the first answer is on its
        /// way. Two entries in flight land the player in whichever room answers
        /// last, which is not the one they asked for second.
        /// </summary>
        private void SetListBusy(bool busy)
        {
            if (listGroup != null)
            {
                listGroup.interactable = !busy;
            }
        }

        /// <summary>
        /// Shows that a refresh is running by fading the button it came from.
        /// </summary>
        private void SetRefreshBusy(bool busy)
        {
            isRefreshBusy = busy;
            RefreshButtonState();
        }

        /// <summary>
        /// Starts the wait. Called as the press goes out rather than when the
        /// answer comes back, so the five seconds are counted between presses.
        /// </summary>
        private void BeginRefreshCooldown()
        {
            refreshReadyAt = Time.unscaledTime + RefreshCooldownSeconds;
            RefreshButtonState();
        }

        private void RefreshButtonState()
        {
            if (refreshButton == null)
            {
                return;
            }

            var ready = !isRefreshBusy && Time.unscaledTime >= refreshReadyAt;
            refreshButton.interactable = ready;

            if (refreshGroup != null)
            {
                refreshGroup.alpha = ready ? 1f : 0.6f;
            }
        }

        /// <summary>
        /// The keyboard is the whole application's, not this screen's, so it is
        /// handed back before the screen goes away.
        /// </summary>
        private void OnDisable()
        {
            SetImeEnabled(true);
        }

        private void Update()
        {
            // Nothing wakes the button when the wait runs out, so it is checked.
            if (refreshButton != null && !refreshButton.interactable)
            {
                RefreshButtonState();
            }

            if (toastGroup != null &&
                toastGroup.gameObject.activeSelf &&
                Time.unscaledTime >= toastHidesAt)
            {
                toastGroup.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The failure notice. Hidden until something goes wrong, and never
        /// clickable: it reports, and the player carries on behind it.
        /// </summary>
        private void BuildToast(RectTransform parent)
        {
            var toast = RoomBrowserUi.CreateImage(
                "Toast",
                parent,
                RoomBrowserStyle.Palette.ToastBase,
                RoomBrowserUi.Rounded(RoomBrowserStyle.Radius.Toast));

            toast.raycastTarget = false;
            toast.rectTransform.Anchor(
                new Vector2(0.5f, 1f),
                new Vector2(0f, -RoomBrowserStyle.Layout.ToastTopMargin),
                RoomBrowserStyle.Layout.ToastSize);

            // The warm cast over the base, and under the text: the mock-up
            // stacks these two fills rather than blending them into one.
            var tint = RoomBrowserUi.CreateImage(
                "Tint",
                toast.transform,
                RoomBrowserStyle.Palette.ToastTint,
                RoomBrowserUi.Rounded(RoomBrowserStyle.Radius.Toast));

            tint.raycastTarget = false;
            tint.rectTransform.Stretch();

            var title = RoomBrowserUi.CreateText(
                "Title",
                toast.transform,
                ResolveFont(semiBoldFont),
                RoomBrowserStyle.FontSize.ToastTitle,
                RoomBrowserStyle.Palette.ToastTitle,
                TextAlignmentOptions.Center);
            title.text = RoomEntryMessages.Title;
            title.rectTransform.Anchor(
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, RoomBrowserStyle.Layout.ToastTitleOffsetY),
                new Vector2(RoomBrowserStyle.Layout.ToastSize.x, 40f));

            toastBody = RoomBrowserUi.CreateText(
                "Body",
                toast.transform,
                ResolveFont(mediumFont),
                RoomBrowserStyle.FontSize.ToastBody,
                RoomBrowserStyle.Palette.ToastBody,
                TextAlignmentOptions.Center);
            toastBody.rectTransform.Anchor(
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, RoomBrowserStyle.Layout.ToastBodyOffsetY),
                new Vector2(RoomBrowserStyle.Layout.ToastSize.x, 32f));

            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            toast.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the notice, and restarts its three seconds if one is already up:
        /// the newest failure is the one the player just caused.
        /// </summary>
        private void ShowToast(string message)
        {
            if (toastGroup == null)
            {
                return;
            }

            toastBody.text = message;
            toastHidesAt = Time.unscaledTime + RoomBrowserStyle.Layout.ToastSeconds;
            toastGroup.gameObject.SetActive(true);
        }

        private void BuildRoomList(RectTransform panel)
        {
            var scrollRect = RoomBrowserUi.CreateRect("RoomList", panel).Stretch(
                RoomBrowserStyle.Layout.ListSidePadding,
                RoomBrowserStyle.Layout.ListSidePadding,
                RoomBrowserStyle.Layout.ListTopOffset,
                RoomBrowserStyle.Layout.ListBottomPadding);

            roomScroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            roomScroll.horizontal = false;
            roomScroll.vertical = true;
            roomScroll.movementType = ScrollRect.MovementType.Clamped;
            roomScroll.scrollSensitivity = 40f;

            var viewport = RoomBrowserUi.CreateRect("Viewport", scrollRect).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = RoomBrowserUi.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RoomBrowserStyle.Layout.RowSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            roomScroll.viewport = viewport;
            roomScroll.content = content;
            listContent = content;

            // Scrolling stays available while a request is out; picking a second
            // room does not, because the first answer is still on its way.
            listGroup = scrollRect.gameObject.AddComponent<CanvasGroup>();

            BuildScrollbar(panel);
            BuildEmptyState(viewport);
        }

        private void BuildScrollbar(RectTransform panel)
        {
            var rect = RoomBrowserUi.CreateRect("Scrollbar", panel);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(
                RoomBrowserStyle.Layout.ScrollbarWidth,
                -(RoomBrowserStyle.Layout.ListTopOffset
                    + RoomBrowserStyle.Layout.ListBottomPadding));
            rect.anchoredPosition = new Vector2(
                -RoomBrowserStyle.Layout.ScrollbarRightPadding,
                (RoomBrowserStyle.Layout.ListBottomPadding
                    - RoomBrowserStyle.Layout.ListTopOffset) * 0.5f);

            var slidingArea = RoomBrowserUi.CreateRect("Sliding Area", rect).Stretch();

            // A radius of half the width is as round as a 10 pixel bar gets; the
            // 18 the mock-up names cannot fit across it.
            var handle = RoomBrowserUi.CreateImage(
                "Handle",
                slidingArea,
                RoomBrowserStyle.Palette.ScrollbarHandle,
                RoomBrowserUi.Rounded(
                    Mathf.RoundToInt(RoomBrowserStyle.Layout.ScrollbarWidth * 0.5f)));
            handle.rectTransform.Stretch();

            var scrollbar = rect.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handle;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            roomScroll.verticalScrollbar = scrollbar;
            roomScroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>
        /// Built now and left hidden. The search story is what has an empty
        /// result to report; an empty room list at start-up is only the list
        /// still loading.
        /// </summary>
        private void BuildEmptyState(RectTransform viewport)
        {
            emptyStateText = RoomBrowserUi.CreateText(
                "EmptyState",
                viewport,
                ResolveFont(mediumFont),
                RoomBrowserStyle.FontSize.MapName,
                RoomBrowserStyle.Palette.TextMuted,
                TextAlignmentOptions.Center);
            emptyStateText.rectTransform.Stretch();
            SetEmptyStateVisible(false);
        }

        private void SetEmptyStateVisible(bool visible)
        {
            if (emptyStateText != null)
            {
                emptyStateText.gameObject.SetActive(visible);
            }
        }

        private void ShowEmptyState(string message)
        {
            if (emptyStateText == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                SetEmptyStateVisible(false);
                return;
            }

            emptyStateText.text = message;
            SetEmptyStateVisible(true);
        }

        /// <summary>
        /// Colour-tints one graphic on hover. Buttons here are text or a filled
        /// shape, never a sprite swap, so the tint is the whole transition.
        /// </summary>
        private static void Tint(Button button, Graphic graphic, Color normal)
        {
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = RoomBrowserStyle.Palette.Accent;
            colors.pressedColor = RoomBrowserStyle.Palette.Accent;
            colors.selectedColor = normal;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
        }

        /// <summary>
        /// Falls back to the project default so a screen whose fonts are not
        /// assigned yet still reads, rather than drawing nothing at all.
        /// </summary>
        private static TMP_FontAsset ResolveFont(TMP_FontAsset preferred)
        {
            return preferred != null ? preferred : TMP_Settings.defaultFontAsset;
        }

        private void ConfigureCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RoomBrowserStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
