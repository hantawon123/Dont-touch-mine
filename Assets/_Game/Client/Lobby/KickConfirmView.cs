using System;
using System.Collections;
using Game.Client.Character;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Ports;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// The kick question, drawn like the settings confirmation: the same
    /// plate, the same buttons, no subtitle.
    /// </summary>
    public sealed class KickConfirmView : MonoBehaviour, ILobbyConfirmView
    {
        public const string RootName = "KickConfirm";
        public const string CancelLabel = "취소";
        public const string ConfirmLabel = "강퇴하기";
        public const float ReasonHeight = 48f;
        public const float ReasonGap = 20f;
        public const float ReasonWidth = 490f;
        public const int ReasonRadius = 10;
        public const float ReasonOptionHeight = 40f;
        public const float ReasonFontSize = 20f;
        public const string ReasonRootName = "Reason";
        public const string ReasonFieldName = "Field";
        public const string ReasonOptionsName = "Options";
        public const float NoteHeight = 128f;
        public const float NoteGap = 16f;
        public const float NoteWidth = 490f;
        public const int NoteRadius = 10;
        public const float NoteFontSize = 18f;
        public const float NotePadding = 16f;
        public const float NoteCounterFontSize = 16f;
        public const float NoteCounterHeight = 20f;
        public const float NoteCounterInsetX = 12f;
        public const float NoteCounterInsetY = 8f;
        public const int NoteMaxLength = 500;
        public const int NoteRequiredLength = 5;
        public const string NoteRootName = "Note";
        public const string NoteCounterName = "Counter";
        public const string NotePlaceholder = "내용을 입력해주세요";
        public const int SortingOrder = PlaySettingsStyle.Overlay.SortingOrder + 20;

        public static readonly ReportReason[] Reasons =
        {
            ReportReason.Abuse,
            ReportReason.Cheating,
            ReportReason.Spam,
            ReportReason.InappropriateName,
            ReportReason.Other
        };

        public static readonly Vector2 ReportPanelSize = new Vector2(
            CharacterClosetStyle.Modal.PanelSize.x,
            CharacterClosetStyle.Modal.PanelSize.y
                + ReasonGap
                + ReasonHeight
                + NoteGap
                + NoteHeight);

        public static string FormatTitle(string displayName) =>
            $"{displayName} 님을\n강퇴하시겠습니까?";

        public static string ReasonLabel(ReportReason reason)
        {
            switch (reason)
            {
                case ReportReason.Abuse:
                    return "욕설/비하";
                case ReportReason.Cheating:
                    return "치팅";
                case ReportReason.Spam:
                    return "도배/광고";
                case ReportReason.InappropriateName:
                    return "부적절한 닉네임";
                default:
                    return "기타";
            }
        }

        private GameObject root;
        private KickConfirmEscape driver;
        private RawImage backdropImage;
        private RectTransform plate;
        private RectTransform declineButton;
        private RectTransform acceptButton;
        private Button acceptControl;
        private Image acceptFill;
        private HomeHoverHighlight acceptHover;
        private bool choosingReason;
        private GameObject reasonRoot;
        private GameObject reasonOptions;
        private GameObject noteRoot;
        private TMP_InputField noteInput;
        private TMP_Text noteCounter;
        private TMP_Text title;
        private TMP_Text acceptLabel;
        private TMP_Text reasonValue;
        private RenderTexture backdrop;
        private Coroutine backdropRoutine;

        public event Action Confirmed;
        public event Action Cancelled;

        public ReportReason SelectedReason { get; private set; } = ReportReason.Abuse;

        public string Note => noteInput != null ? noteInput.text : string.Empty;

        public void Show(string message)
        {
            Show(message, ConfirmLabel, false);
        }

        public void Show(string message, string confirmLabel)
        {
            Show(message, confirmLabel, false);
        }

        public void Show(string message, string confirmLabel, bool chooseReason)
        {
            EnsureLayout();
            if (title != null)
            {
                title.text = message ?? string.Empty;
            }

            if (acceptLabel != null)
            {
                acceptLabel.text = string.IsNullOrWhiteSpace(confirmLabel)
                    ? ConfirmLabel
                    : confirmLabel;
            }

            SelectedReason = ReportReason.Abuse;
            ApplyReason(SelectedReason);
            ResetNote();
            ApplyLayout(chooseReason);
            RefreshAcceptEnabled();
            if (chooseReason && noteInput != null)
            {
                noteInput.ActivateInputField();
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            BeginBackdrop();
        }

        public void Hide()
        {
            SetReasonOpen(false);
            if (noteInput != null)
            {
                noteInput.DeactivateInputField();
            }

            if (root != null)
            {
                root.SetActive(false);
            }

            EndBackdrop();
        }

        private void OnDestroy()
        {
            EndBackdrop();
        }

        public void EnsureLayout()
        {
            if (root != null)
            {
                return;
            }

            var host = GetComponentInParent<Canvas>()?.transform ?? transform;
            var overlay = new GameObject(
                RootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            overlay.transform.SetParent(host, false);
            var rect = (RectTransform)overlay.transform;
            Stretch(rect);

            var canvas = overlay.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            var blurRect = CreateRect("Backdrop", rect);
            Stretch(blurRect);
            backdropImage = blurRect.gameObject.AddComponent<RawImage>();
            backdropImage.raycastTarget = false;
            backdropImage.enabled = false;

            var dimRect = CreateRect("Dim", rect);
            Stretch(dimRect);
            AddImage(dimRect, CharacterClosetStyle.Palette.Dim, raycastTarget: true);

            CreatePanel(rect);
            ApplyLayout(false);

            overlay.SetActive(false);
            root = overlay;
            driver = overlay.AddComponent<KickConfirmEscape>();
            driver.Bind(this);
        }

        private void CreatePanel(RectTransform overlay)
        {
            plate = CreateRect("Panel", overlay);
            SetAnchor(plate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            plate.anchoredPosition = Vector2.zero;
            plate.sizeDelta = CharacterClosetStyle.Modal.PanelSize;
            AddImage(
                plate,
                CharacterClosetStyle.Palette.ModalFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.Modal.PanelRadius),
                raycastTarget: true);

            var titleHeight = CharacterClosetStyle.Modal.TitleFontSize * 1.4f * 2f;
            title = CreateText(
                "Title",
                plate,
                string.Empty,
                CharacterClosetStyle.Modal.TitleFontSize,
                CharacterClosetStyle.Palette.ModalTitle,
                TextAlignmentOptions.Center);
            var titleRect = title.rectTransform;
            SetAnchor(titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            titleRect.anchoredPosition = new Vector2(0f, -CharacterClosetStyle.Modal.TitleTop);
            titleRect.sizeDelta = new Vector2(0f, titleHeight);

            CreateReasonPicker(plate, titleHeight);
            CreateNoteField(plate, titleHeight);

            var buttonTop = KickButtonTop(titleHeight);
            var half = (CharacterClosetStyle.Modal.ButtonSize.x
                        + CharacterClosetStyle.Modal.ButtonGap) * 0.5f;

            CreateButton(
                plate,
                "DeclineButton",
                new Vector2(-half, -buttonTop),
                CancelLabel,
                CharacterClosetStyle.Palette.DeclineFill,
                CharacterClosetStyle.Palette.DeclineHoverFill,
                CharacterClosetStyle.Palette.DeclineLabel,
                () => Cancelled?.Invoke());
            declineButton = plate.Find("DeclineButton") as RectTransform;

            acceptLabel = CreateButton(
                plate,
                "AcceptButton",
                new Vector2(half, -buttonTop),
                ConfirmLabel,
                CharacterClosetStyle.Palette.AcceptFill,
                CharacterClosetStyle.Palette.AcceptHoverFill,
                CharacterClosetStyle.Palette.AcceptLabel,
                TryConfirm);
            acceptButton = plate.Find("AcceptButton") as RectTransform;
            if (acceptButton != null)
            {
                acceptControl = acceptButton.GetComponent<Button>();
                acceptFill = acceptButton.GetComponent<Image>();
                acceptHover = acceptButton.GetComponent<HomeHoverHighlight>();
            }

            CreateCloseButton(plate);
        }

        private void CreateReasonPicker(RectTransform host, float titleHeight)
        {
            var rootRect = CreateRect(ReasonRootName, host);
            SetAnchor(rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rootRect.anchoredPosition = new Vector2(
                0f,
                -(CharacterClosetStyle.Modal.TitleTop + titleHeight + ReasonGap));
            rootRect.sizeDelta = new Vector2(ReasonWidth, ReasonHeight);
            reasonRoot = rootRect.gameObject;

            var field = CreateRect(ReasonFieldName, rootRect);
            Stretch(field);
            var fieldFill = AddImage(
                field,
                CharacterClosetStyle.Palette.DeclineFill,
                HomeUiFonts.Rounded(ReasonRadius),
                raycastTarget: true);

            reasonValue = CreateText(
                "Value",
                field,
                ReasonLabel(ReportReason.Abuse),
                ReasonFontSize,
                CharacterClosetStyle.Palette.ModalTitle,
                TextAlignmentOptions.MidlineLeft);
            SetAnchor(reasonValue.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            reasonValue.rectTransform.offsetMin = new Vector2(16f, 0f);
            reasonValue.rectTransform.offsetMax = new Vector2(-36f, 0f);

            var chevron = CreateText(
                "Chevron",
                field,
                "▾",
                ReasonFontSize,
                CharacterClosetStyle.Palette.ModalTitle,
                TextAlignmentOptions.MidlineRight);
            SetAnchor(chevron.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            chevron.rectTransform.offsetMin = new Vector2(0f, 0f);
            chevron.rectTransform.offsetMax = new Vector2(-16f, 0f);

            var fieldHover = field.gameObject.AddComponent<HomeHoverHighlight>();
            fieldHover.Bind(
                fieldFill,
                null,
                CharacterClosetStyle.Palette.DeclineFill,
                CharacterClosetStyle.Palette.DeclineHoverFill);

            var fieldButton = field.gameObject.AddComponent<Button>();
            fieldButton.targetGraphic = fieldFill;
            fieldButton.transition = Selectable.Transition.None;
            fieldButton.onClick.AddListener(ToggleReasonOptions);

            var optionsRect = CreateRect(ReasonOptionsName, rootRect);
            SetAnchor(optionsRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f));
            optionsRect.anchoredPosition = Vector2.zero;
            optionsRect.sizeDelta = new Vector2(0f, ReasonOptionHeight * Reasons.Length);
            AddImage(
                optionsRect,
                CharacterClosetStyle.Palette.ModalFill,
                HomeUiFonts.Rounded(ReasonRadius),
                raycastTarget: true);
            reasonOptions = optionsRect.gameObject;

            for (var index = 0; index < Reasons.Length; index++)
            {
                var reason = Reasons[index];
                var option = CreateRect(reason.ToString(), optionsRect);
                SetAnchor(option, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
                option.anchoredPosition = new Vector2(0f, -index * ReasonOptionHeight);
                option.sizeDelta = new Vector2(0f, ReasonOptionHeight);

                var optionFill = AddImage(
                    option,
                    Color.clear,
                    raycastTarget: true);

                var label = CreateText(
                    "Label",
                    option,
                    ReasonLabel(reason),
                    ReasonFontSize,
                    CharacterClosetStyle.Palette.ModalTitle,
                    TextAlignmentOptions.MidlineLeft);
                SetAnchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                label.rectTransform.offsetMin = new Vector2(16f, 0f);
                label.rectTransform.offsetMax = new Vector2(-16f, 0f);

                var optionHover = option.gameObject.AddComponent<HomeHoverHighlight>();
                optionHover.Bind(
                    optionFill,
                    null,
                    Color.clear,
                    CharacterClosetStyle.Palette.DeclineHoverFill);

                var optionButton = option.gameObject.AddComponent<Button>();
                optionButton.targetGraphic = optionFill;
                optionButton.transition = Selectable.Transition.None;
                optionButton.onClick.AddListener(() => PickReason(reason));
            }

            reasonOptions.SetActive(false);
            reasonRoot.SetActive(false);
        }

        private void CreateNoteField(RectTransform host, float titleHeight)
        {
            var rootRect = CreateRect(NoteRootName, host);
            SetAnchor(rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rootRect.anchoredPosition = new Vector2(
                0f,
                -(CharacterClosetStyle.Modal.TitleTop
                    + titleHeight
                    + ReasonGap
                    + ReasonHeight
                    + NoteGap));
            rootRect.sizeDelta = new Vector2(NoteWidth, NoteHeight);
            noteRoot = rootRect.gameObject;

            AddImage(
                rootRect,
                SettingsStyle.Palette.FieldFill,
                HomeUiFonts.Rounded(NoteRadius),
                raycastTarget: true);

            var viewport = CreateRect("TextArea", rootRect);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(NotePadding, NotePadding + NoteCounterHeight);
            viewport.offsetMax = new Vector2(-NotePadding, -NotePadding);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = CreateText(
                "Text",
                viewport,
                string.Empty,
                NoteFontSize,
                SettingsStyle.Palette.FieldText,
                TextAlignmentOptions.TopLeft,
                HomeUiFonts.ApplyRegular());
            Stretch(text.rectTransform);
            text.raycastTarget = true;

            var placeholder = CreateText(
                "Placeholder",
                viewport,
                NotePlaceholder,
                NoteFontSize,
                SettingsStyle.Palette.FieldPlaceholder,
                TextAlignmentOptions.TopLeft,
                HomeUiFonts.ApplyRegular());
            Stretch(placeholder.rectTransform);

            noteCounter = CreateText(
                NoteCounterName,
                rootRect,
                string.Empty,
                NoteCounterFontSize,
                SettingsStyle.Palette.Counter,
                TextAlignmentOptions.MidlineRight,
                HomeUiFonts.ApplyRegular());
            var counterRect = noteCounter.rectTransform;
            SetAnchor(counterRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            counterRect.anchoredPosition = new Vector2(-NoteCounterInsetX, NoteCounterInsetY);
            counterRect.sizeDelta = new Vector2(120f, NoteCounterHeight);
            counterRect.SetAsLastSibling();
            ShowNoteCount(0);

            rootRect.gameObject.SetActive(false);
            var input = rootRect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = text.font;
            input.pointSize = NoteFontSize;
            input.richText = false;
            text.richText = false;
            placeholder.richText = false;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.characterLimit = NoteMaxLength;
            input.customCaretColor = true;
            input.caretColor = SettingsStyle.Palette.FieldText;
            input.selectionColor = SettingsStyle.Palette.Accent;
            input.onValueChanged.AddListener(OnNoteTyped);
            rootRect.gameObject.SetActive(true);
            noteRoot.SetActive(false);
            noteInput = input;
        }

        private void ResetNote()
        {
            if (noteInput != null)
            {
                noteInput.SetTextWithoutNotify(string.Empty);
                noteInput.DeactivateInputField();
            }

            ShowNoteCount(0);
            RefreshAcceptEnabled();
        }

        private void OnNoteTyped(string text)
        {
            ShowNoteCount(text != null ? text.Length : 0);
            RefreshAcceptEnabled();
        }

        private void TryConfirm()
        {
            if (!CanAccept)
            {
                return;
            }

            Confirmed?.Invoke();
        }

        private bool CanAccept =>
            !choosingReason
            || SelectedReason != ReportReason.Other
            || Note.Trim().Length >= NoteRequiredLength;

        private void RefreshAcceptEnabled()
        {
            var enabled = CanAccept;
            if (acceptControl != null)
            {
                acceptControl.interactable = enabled;
            }

            if (acceptHover != null)
            {
                acceptHover.Bind(
                    acceptFill,
                    null,
                    enabled
                        ? CharacterClosetStyle.Palette.AcceptFill
                        : SettingsStyle.Palette.ButtonOffFill,
                    enabled
                        ? CharacterClosetStyle.Palette.AcceptHoverFill
                        : SettingsStyle.Palette.ButtonOffFill);
            }

            if (acceptLabel != null)
            {
                acceptLabel.color = enabled
                    ? CharacterClosetStyle.Palette.AcceptLabel
                    : SettingsStyle.Palette.ButtonOffLabel;
            }
        }

        private void ShowNoteCount(int length)
        {
            if (noteCounter != null)
            {
                noteCounter.text = $"{length}/{NoteMaxLength}";
            }
        }

        private void ApplyLayout(bool chooseReason)
        {
            choosingReason = chooseReason;
            SetReasonOpen(false);
            if (reasonRoot != null)
            {
                reasonRoot.SetActive(chooseReason);
            }

            if (noteRoot != null)
            {
                noteRoot.SetActive(chooseReason);
            }

            if (plate != null)
            {
                plate.sizeDelta = chooseReason
                    ? ReportPanelSize
                    : CharacterClosetStyle.Modal.PanelSize;
            }

            var titleHeight = CharacterClosetStyle.Modal.TitleFontSize * 1.4f * 2f;
            var buttonTop = chooseReason
                ? ReportButtonTop(titleHeight)
                : KickButtonTop(titleHeight);
            var half = (CharacterClosetStyle.Modal.ButtonSize.x
                        + CharacterClosetStyle.Modal.ButtonGap) * 0.5f;
            if (declineButton != null)
            {
                declineButton.anchoredPosition = new Vector2(-half, -buttonTop);
            }

            if (acceptButton != null)
            {
                acceptButton.anchoredPosition = new Vector2(half, -buttonTop);
            }

            RefreshAcceptEnabled();
        }

        private void ApplyReason(ReportReason reason)
        {
            SelectedReason = reason;
            if (reasonValue != null)
            {
                reasonValue.text = ReasonLabel(reason);
            }

            RefreshAcceptEnabled();
        }

        private void PickReason(ReportReason reason)
        {
            ApplyReason(reason);
            SetReasonOpen(false);
        }

        private void ToggleReasonOptions()
        {
            if (reasonOptions == null)
            {
                return;
            }

            SetReasonOpen(!reasonOptions.activeSelf);
        }

        private void SetReasonOpen(bool open)
        {
            if (reasonOptions != null)
            {
                reasonOptions.SetActive(open);
            }

            if (open && reasonRoot != null)
            {
                reasonRoot.transform.SetAsLastSibling();
            }
        }

        private static float KickButtonTop(float titleHeight) =>
            CharacterClosetStyle.Modal.TitleTop
            + titleHeight
            + CharacterClosetStyle.Modal.ButtonGapAbove;

        private static float ReportButtonTop(float titleHeight) =>
            CharacterClosetStyle.Modal.TitleTop
            + titleHeight
            + ReasonGap
            + ReasonHeight
            + NoteGap
            + NoteHeight
            + CharacterClosetStyle.Modal.ButtonGapAbove;

        private TMP_Text CreateButton(
            RectTransform plate,
            string name,
            Vector2 position,
            string label,
            Color fillColor,
            Color hoverColor,
            Color labelColor,
            Action clicked)
        {
            var rect = CreateRect(name, plate);
            SetAnchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rect.anchoredPosition = position;
            rect.sizeDelta = CharacterClosetStyle.Modal.ButtonSize;

            var fill = AddImage(
                rect,
                fillColor,
                HomeUiFonts.Rounded(CharacterClosetStyle.Modal.ButtonRadius),
                raycastTarget: true);

            var text = CreateText(
                "Label",
                rect,
                label,
                CharacterClosetStyle.Modal.ButtonFontSize,
                labelColor,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            var hover = rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(fill, null, fillColor, hoverColor);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            return text;
        }

        private void CreateCloseButton(RectTransform plate)
        {
            var rect = CreateRect("CloseButton", plate);
            SetAnchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(
                -CharacterClosetStyle.Modal.CloseOffset.x,
                -CharacterClosetStyle.Modal.CloseOffset.y);
            rect.sizeDelta = new Vector2(
                CharacterClosetStyle.Modal.CloseSize, CharacterClosetStyle.Modal.CloseSize);

            var image = AddImage(rect, CharacterClosetStyle.Palette.CloseIcon, raycastTarget: true);
            image.sprite = BuildCloseIcon();
            image.type = Image.Type.Simple;
            image.preserveAspect = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Cancelled?.Invoke());
        }

        private void BeginBackdrop()
        {
            if (backdropImage == null || driver == null || !Application.isPlaying)
            {
                return;
            }

            EndBackdrop();
            backdropImage.enabled = false;
            backdropRoutine = driver.StartCoroutine(CaptureBackdrop());
        }

        private void EndBackdrop()
        {
            if (backdropRoutine != null)
            {
                if (driver != null)
                {
                    driver.StopCoroutine(backdropRoutine);
                }

                backdropRoutine = null;
            }

            if (backdrop == null)
            {
                return;
            }

            if (backdropImage != null)
            {
                backdropImage.texture = null;
            }

            backdrop.Release();
            Destroy(backdrop);
            backdrop = null;
        }

        private IEnumerator CaptureBackdrop()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (backdrop != null)
            {
                if (backdropImage != null)
                {
                    backdropImage.texture = null;
                }

                backdrop.Release();
                Destroy(backdrop);
                backdrop = null;
            }

            backdrop = ScreenBlur.Capture(
                CharacterClosetStyle.Modal.BackdropHalvings,
                CharacterClosetStyle.Modal.BackdropBlur);
            if (backdropImage != null)
            {
                backdropImage.texture = backdrop;
                backdropImage.uvRect = ScreenBlur.UvRect;
                backdropImage.enabled = true;
            }

            backdropRoutine = null;
        }

        internal void RaiseCancelled() => Cancelled?.Invoke();

        private static Sprite BuildCloseIcon()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            var center = (size - 1) * 0.5f;
            var radius = center - 2f;
            var arm = 11f;
            var thickness = 3.2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    if ((dx * dx) + (dy * dy) > radius * radius)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var onX = Mathf.Abs(dx - dy) <= thickness && Mathf.Abs(dx) <= arm
                        || Mathf.Abs(dx + dy) <= thickness && Mathf.Abs(dx) <= arm;
                    texture.SetPixel(x, y, onX ? new Color(0.14f, 0.14f, 0.14f, 1f) : Color.white);
                }
            }

            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var created = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            created.SetParent(parent, false);
            return created;
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
                image.pixelsPerUnitMultiplier = 1f;
            }

            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            TMP_FontAsset font = null)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : HomeUiFonts.Apply();
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

        /// <summary>
        /// Ticks on the overlay so Escape still closes the panel when this
        /// view lives on a hidden scene object.
        /// </summary>
        private sealed class KickConfirmEscape : MonoBehaviour
        {
            private KickConfirmView owner;

            public void Bind(KickConfirmView view) => owner = view;

            private void Update()
            {
                if (owner == null || !gameObject.activeSelf)
                {
                    return;
                }

                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    owner.RaiseCancelled();
                }
            }
        }
    }
}
