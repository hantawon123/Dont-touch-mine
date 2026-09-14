using System;
using System.Collections;
using Game.Client.Character;
using Game.Client.Home;
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

            switch (kind)
            {
                case SettingsConfirmKind.ResetAll:
                    confirmTitle.text = SettingsStyle.Modal.ResetAllTitle;
                    confirmSubtitle.text = SettingsStyle.Modal.ResetAllSubtitle;
                    declineLabel.text = SettingsStyle.Modal.CancelLabel;
                    acceptLabel.text = SettingsStyle.Modal.ResetLabel;
                    break;
                case SettingsConfirmKind.Discard:
                    confirmTitle.text = SettingsStyle.Modal.DiscardTitle;
                    confirmSubtitle.text = SettingsStyle.Modal.DiscardSubtitle;
                    declineLabel.text = SettingsStyle.Modal.LeaveLabel;
                    acceptLabel.text = SettingsStyle.Modal.SaveAndLeaveLabel;
                    break;
                case SettingsConfirmKind.LeaveGame:
                    confirmTitle.text = SettingsStyle.Modal.LeaveGameTitle;
                    confirmSubtitle.text = SettingsStyle.Modal.LeaveGameSubtitle;
                    declineLabel.text = SettingsStyle.Modal.CancelLabel;
                    acceptLabel.text = SettingsStyle.Modal.LeaveGameAcceptLabel;
                    break;
                default:
                    confirmTitle.text = SettingsStyle.TabLabel(tab) + SettingsStyle.Modal.ResetTabTitleSuffix;
                    confirmSubtitle.text = SettingsStyle.Modal.ResetTabSubtitle;
                    declineLabel.text = SettingsStyle.Modal.CancelLabel;
                    acceptLabel.text = SettingsStyle.Modal.ResetLabel;
                    break;
            }

            isConfirmOpen = true;
            confirmRoot.SetActive(true);
            BeginBackdrop(confirmBackdrop);
        }

        public void HideConfirm()
        {
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
            if (closeIcon != null)
            {
                image.sprite = closeIcon;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ConfirmDismissed?.Invoke());
            buttons.Add(button);
        }
    }
}
