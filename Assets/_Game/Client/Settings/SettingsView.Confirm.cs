using System;
using System.Collections;
using Game.Client.Character;
using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// The confirmation over the settings screen: the blurred still, the dim,
    /// and the panel asking the question.
    /// </summary>
    /// <remarks>
    /// The design draws this panel exactly as the closet's, so the shape comes
    /// from <see cref="CharacterClosetStyle.Modal"/> and the softened still
    /// from <see cref="ScreenBlur"/>; only the words are this screen's own.
    /// One panel serves the three questions, and which was asked is carried by
    /// <see cref="SettingsConfirmKind"/> rather than by which panel spoke.
    /// </remarks>
    public sealed partial class SettingsView
    {
        private GameObject confirmRoot;
        private RawImage confirmBackdrop;
        private TMP_Text confirmTitle;
        private TMP_Text confirmSubtitle;
        private TMP_Text declineLabel;
        private TMP_Text acceptLabel;
        private bool isConfirmOpen;
        private SettingsConfirmKind? paintedConfirm;
        private SettingsTab paintedConfirmTab;
        private string leaveConfirmTitle;

        /// <summary>
        /// The softened still, and whose panel it is currently behind. Shared
        /// by every modal on this screen: only one is ever up, and a capture
        /// each would be two full-screen textures to hold and to free.
        /// </summary>
        private RawImage activeBackdrop;

        private RenderTexture backdrop;
        private Coroutine backdropRoutine;

        public event Action ConfirmAccepted;

        public event Action ConfirmDeclined;

        public event Action ConfirmDismissed;

        /// <summary>
        /// True while a confirmation or the feedback panel is up, so Esc
        /// belongs to that panel rather than to the overlay that opened this
        /// screen.
        /// </summary>
        public bool BlocksEscape => isConfirmOpen || isFeedbackOpen;

        /// <summary>
        /// True after this view ate Esc on this frame, so a LateTick overlay
        /// does not also close the whole screen.
        /// </summary>
        public bool ConsumedEscapeThisFrame { get; private set; }

        public void ShowConfirm(SettingsConfirmKind kind, SettingsTab tab)
        {
            if (confirmRoot == null)
            {
                return;
            }

            paintedConfirm = kind;
            paintedConfirmTab = tab;
            leaveConfirmTitle = null;
            PaintConfirm();
            OpenConfirm();
        }

        public void ShowLeaveConfirmation(string title)
        {
            if (confirmRoot == null)
            {
                return;
            }

            paintedConfirm = SettingsConfirmKind.LeaveGame;
            leaveConfirmTitle = title ?? string.Empty;
            PaintConfirm();
            OpenConfirm();
        }

        private void PaintConfirm()
        {
            if (confirmTitle == null || !paintedConfirm.HasValue)
            {
                return;
            }

            if (leaveConfirmTitle != null)
            {
                confirmTitle.text = leaveConfirmTitle;
                confirmSubtitle.text = SettingsStyle.Modal.LeaveGameSubtitle;
                declineLabel.text = Copy(UiText.Settings.Cancel);
                acceptLabel.text = Copy(UiText.Settings.Leave);
                LayoutConfirm();
                return;
            }

            switch (paintedConfirm.Value)
            {
                case SettingsConfirmKind.ResetAll:
                    confirmTitle.text = Copy(UiText.Settings.ResetAllTitle);
                    confirmSubtitle.text = Copy(UiText.Settings.ResetAllSubtitle);
                    declineLabel.text = Copy(UiText.Settings.Cancel);
                    acceptLabel.text = Copy(UiText.Settings.Reset);
                    break;
                case SettingsConfirmKind.Discard:
                    confirmTitle.text = Copy(UiText.Settings.DiscardTitle);
                    confirmSubtitle.text = Copy(UiText.Settings.DiscardSubtitle);
                    declineLabel.text = Copy(UiText.Settings.LeaveWithoutSaving);
                    acceptLabel.text = Copy(UiText.Settings.SaveAndLeave);
                    break;
                case SettingsConfirmKind.LeaveGame:
                    confirmTitle.text = Copy(UiText.Settings.LeaveGameTitle);
                    confirmSubtitle.text = SettingsStyle.Modal.LeaveGameSubtitle;
                    declineLabel.text = Copy(UiText.Settings.Cancel);
                    acceptLabel.text = Copy(UiText.Settings.Leave);
                    break;
                default:
                    var language = chromeLocale != null ? chromeLocale.LanguageCode : "ko";
                    confirmTitle.text = SettingsStyle.Modal.ResetTabTitle(paintedConfirmTab, language);
                    confirmSubtitle.text = Copy(UiText.Settings.ResetTabSubtitle);
                    declineLabel.text = Copy(UiText.Settings.Cancel);
                    acceptLabel.text = Copy(UiText.Settings.Reset);
                    break;
            }

            LayoutConfirm();
        }

        private void LayoutConfirm()
        {
            if (confirmTitle == null || confirmSubtitle == null)
            {
                return;
            }

            var plate = confirmTitle.rectTransform.parent as RectTransform;
            if (plate == null)
            {
                return;
            }

            var modal = CharacterClosetStyle.Modal;
            var minTitleHeight = modal.TitleFontSize * 1.4f;
            var subtitleHeight = modal.SubtitleFontSize * 1.4f;
            confirmTitle.textWrappingMode = TextWrappingModes.Normal;
            confirmTitle.overflowMode = TextOverflowModes.Overflow;
            var titleHeight = Mathf.Max(
                minTitleHeight,
                confirmTitle.GetPreferredValues(confirmTitle.text, modal.PanelSize.x, 0f).y);

            var title = confirmTitle.rectTransform;
            title.sizeDelta = new Vector2(0f, titleHeight);

            var subtitleTop = modal.TitleTop + titleHeight + modal.SubtitleGap;
            var subtitle = confirmSubtitle.rectTransform;
            subtitle.anchoredPosition = new Vector2(0f, -subtitleTop);
            subtitle.sizeDelta = new Vector2(0f, subtitleHeight);

            var buttonTop = subtitleTop + subtitleHeight + modal.ButtonGapAbove;
            var half = (modal.ButtonSize.x + modal.ButtonGap) * 0.5f;
            var decline = declineLabel != null
                ? declineLabel.rectTransform.parent as RectTransform
                : null;
            var accept = acceptLabel != null
                ? acceptLabel.rectTransform.parent as RectTransform
                : null;
            if (decline != null)
            {
                decline.anchoredPosition = new Vector2(-half, -buttonTop);
            }

            if (accept != null)
            {
                accept.anchoredPosition = new Vector2(half, -buttonTop);
            }

            plate.sizeDelta = new Vector2(
                modal.PanelSize.x,
                buttonTop + modal.ButtonSize.y + modal.BottomPadding);
        }

        private void OpenConfirm()
        {
            isConfirmOpen = true;
            confirmRoot.SetActive(true);
            BeginBackdrop(confirmBackdrop);
        }

        public void HideConfirm()
        {
            paintedConfirm = null;
            leaveConfirmTitle = null;
            isConfirmOpen = false;
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }

            EndBackdrop();
        }

        /// <summary>
        /// Escape closes whichever panel is up without answering it, as its X
        /// does. Read here rather than through the UI input module's cancel
        /// action because this is the only key the screen listens for, and only
        /// while a panel is up.
        /// </summary>
        /// <remarks>
        /// The writing panel is asked second and only when no confirmation is
        /// up, so one press never closes two things.
        /// </remarks>
        private void Update()
        {
            ConsumedEscapeThisFrame = false;
            if (!isConfirmOpen && !isFeedbackOpen)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            ConsumedEscapeThisFrame = true;
            if (isConfirmOpen)
            {
                ConfirmDismissed?.Invoke();
                return;
            }

            RaiseFeedbackDismissed();
        }

        /// <summary>
        /// Starts softening the screen behind a panel that has just opened.
        /// </summary>
        /// <remarks>
        /// The still is taken of the screen without the panel on it, so the
        /// capture waits a frame while the panel is up but not yet drawn. Until
        /// it arrives the dim alone stands in.
        /// </remarks>
        private void BeginBackdrop(RawImage target)
        {
            if (target == null)
            {
                return;
            }

            EndBackdrop();
            activeBackdrop = target;
            activeBackdrop.enabled = false;
            backdropRoutine = StartCoroutine(CaptureBackdrop());
        }

        private void EndBackdrop()
        {
            if (backdropRoutine != null)
            {
                StopCoroutine(backdropRoutine);
                backdropRoutine = null;
            }

            ReleaseBackdrop();
            activeBackdrop = null;
        }

        private IEnumerator CaptureBackdrop()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            ReleaseBackdrop();
            backdrop = ScreenBlur.Capture(
                CharacterClosetStyle.Modal.BackdropHalvings,
                CharacterClosetStyle.Modal.BackdropBlur);
            if (activeBackdrop != null)
            {
                activeBackdrop.texture = backdrop;
                activeBackdrop.uvRect = ScreenBlur.UvRect;
                activeBackdrop.enabled = true;
            }

            backdropRoutine = null;
        }

        private void ReleaseBackdrop()
        {
            if (backdrop == null)
            {
                return;
            }

            if (activeBackdrop != null)
            {
                activeBackdrop.texture = null;
            }

            backdrop.Release();
            Destroy(backdrop);
            backdrop = null;
        }

        /// <summary>
        /// Built with the screen rather than when first asked for, so opening a
        /// confirmation is a flag rather than a construction.
        /// </summary>
        private void CreateConfirm(RectTransform canvas)
        {
            var root = CreateRect("Confirm", canvas);
            Stretch(root);

            var blurRect = CreateRect("Backdrop", root);
            Stretch(blurRect);
            confirmBackdrop = blurRect.gameObject.AddComponent<RawImage>();
            confirmBackdrop.raycastTarget = false;
            confirmBackdrop.enabled = false;

            // Takes every click that misses the panel and does nothing with
            // it: the design gives the outside no meaning.
            var dimRect = CreateRect("Dim", root);
            Stretch(dimRect);
            AddImage(dimRect, CharacterClosetStyle.Palette.Dim, raycastTarget: true);

            CreateConfirmPanel(root);

            root.gameObject.SetActive(false);
            confirmRoot = root.gameObject;
        }

        private void CreateConfirmPanel(RectTransform root)
        {
            var modal = CharacterClosetStyle.Modal.PanelSize;
            var plate = CreateRect("Panel", root);
            SetAnchor(plate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            plate.anchoredPosition = Vector2.zero;
            plate.sizeDelta = modal;
            AddImage(
                plate,
                CharacterClosetStyle.Palette.ModalFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.Modal.PanelRadius),
                raycastTarget: true);

            var titleHeight = CharacterClosetStyle.Modal.TitleFontSize * 1.4f;
            var subtitleHeight = CharacterClosetStyle.Modal.SubtitleFontSize * 1.4f;

            confirmTitle = CreateText(
                "Title",
                plate,
                SettingsStyle.Modal.ResetAllTitle,
                CharacterClosetStyle.Modal.TitleFontSize,
                CharacterClosetStyle.Palette.ModalTitle,
                TextAlignmentOptions.Top);
            confirmTitle.textWrappingMode = TextWrappingModes.Normal;
            confirmTitle.overflowMode = TextOverflowModes.Overflow;
            var title = confirmTitle.rectTransform;
            SetAnchor(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            title.anchoredPosition = new Vector2(0f, -CharacterClosetStyle.Modal.TitleTop);
            title.sizeDelta = new Vector2(0f, titleHeight);

            var subtitleTop = CharacterClosetStyle.Modal.TitleTop
                              + titleHeight
                              + CharacterClosetStyle.Modal.SubtitleGap;
            confirmSubtitle = CreateText(
                "Subtitle",
                plate,
                SettingsStyle.Modal.ResetAllSubtitle,
                CharacterClosetStyle.Modal.SubtitleFontSize,
                CharacterClosetStyle.Palette.ModalSubtitle,
                TextAlignmentOptions.Top,
                regularFont);
            var subtitle = confirmSubtitle.rectTransform;
            SetAnchor(subtitle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            subtitle.anchoredPosition = new Vector2(0f, -subtitleTop);
            subtitle.sizeDelta = new Vector2(0f, subtitleHeight);

            var buttonTop = subtitleTop
                            + subtitleHeight
                            + CharacterClosetStyle.Modal.ButtonGapAbove;
            var half = (CharacterClosetStyle.Modal.ButtonSize.x
                        + CharacterClosetStyle.Modal.ButtonGap) * 0.5f;

            declineLabel = CreateConfirmButton(
                plate,
                "DeclineButton",
                new Vector2(-half, -buttonTop),
                SettingsStyle.Modal.CancelLabel,
                CharacterClosetStyle.Palette.DeclineFill,
                CharacterClosetStyle.Palette.DeclineHoverFill,
                CharacterClosetStyle.Palette.DeclineLabel,
                () => ConfirmDeclined?.Invoke());

            acceptLabel = CreateConfirmButton(
                plate,
                "AcceptButton",
                new Vector2(half, -buttonTop),
                SettingsStyle.Modal.ResetLabel,
                CharacterClosetStyle.Palette.AcceptFill,
                CharacterClosetStyle.Palette.AcceptHoverFill,
                CharacterClosetStyle.Palette.AcceptLabel,
                () => ConfirmAccepted?.Invoke());

            CreateCloseButton(plate);
        }

        private TMP_Text CreateConfirmButton(
            RectTransform plate,
            string name,
            Vector2 position,
            string label,
            Color fillColor,
            Color hoverColor,
            Color labelColor,
            Action clicked)
        {
            // The plate is the writing panel's own; only its label is wanted
            // back here, because a confirmation's buttons never change state.
            CreateModalButton(
                plate,
                name,
                position,
                label,
                fillColor,
                hoverColor,
                labelColor,
                clicked,
                out _,
                out var text,
                out _);
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
            SettingsStyle.ApplyCloseIcon(image, closeIcon);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ConfirmDismissed?.Invoke());
            buttons.Add(button);
        }
    }
}
