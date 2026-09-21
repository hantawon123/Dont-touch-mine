using System;
using System.Collections;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Character
{
    /// <summary>
    /// Which confirmation is being asked for. The two differ only in their
    /// heading, but not in what answering them does, so the presenter has to
    /// know which one it put up.
    /// </summary>
    public enum ClosetConfirmKind
    {
        Reset,
        Discard
    }

    /// <summary>
    /// The confirmation over the closet: the blurred still, the dim, and the
    /// panel asking the question.
    /// </summary>
    /// <remarks>
    /// One panel serves both questions. Two panels would be two copies of a
    /// layout that differs by a single line of text, and the answer is carried
    /// by <see cref="ClosetConfirmKind"/> rather than by which panel spoke.
    /// </remarks>
    public sealed partial class CharacterClosetView
    {
        private GameObject confirmRoot;
        private RawImage backdropImage;
        private TMP_Text confirmTitle;
        private TMP_Text confirmSubtitle;
        private TMP_Text confirmDecline;
        private TMP_Text confirmAccept;
        private RenderTexture backdrop;
        private Coroutine backdropRoutine;
        private bool isConfirmOpen;

        /// <summary>The panel's 예.</summary>
        public event Action ConfirmAccepted;

        /// <summary>Its 아니오, its X, and Escape.</summary>
        public event Action ConfirmDismissed;

        public void ShowConfirm(ClosetConfirmKind kind)
        {
            if (confirmRoot == null)
            {
                return;
            }

            lastConfirmKind = kind;
            confirmTitle.text = kind == ClosetConfirmKind.Reset
                ? Copy(UiText.Closet.ResetTitle)
                : Copy(UiText.Closet.DiscardTitle);
            if (confirmSubtitle != null)
            {
                confirmSubtitle.text = Copy(UiText.Closet.Subtitle);
            }

            if (confirmDecline != null)
            {
                confirmDecline.text = Copy(UiText.Closet.Decline);
            }

            if (confirmAccept != null)
            {
                confirmAccept.text = Copy(UiText.Closet.Accept);
            }

            isConfirmOpen = true;
            confirmRoot.SetActive(true);

            // The still is taken of the screen without the modal on it, so the
            // capture waits a frame while the panel is up but not yet drawn.
            // Until it arrives the dim alone stands in.
            backdropImage.enabled = false;
            if (backdropRoutine != null)
            {
                StopCoroutine(backdropRoutine);
            }

            backdropRoutine = StartCoroutine(CaptureBackdrop());
        }

        public void HideConfirm()
        {
            isConfirmOpen = false;
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }

            if (backdropRoutine != null)
            {
                StopCoroutine(backdropRoutine);
                backdropRoutine = null;
            }

            ReleaseBackdrop();
        }

        /// <summary>
        /// Escape answers 아니오, as the design asks. Read here rather than
        /// through the UI input module's cancel action because this is the only
        /// key the screen listens for, and only while the panel is up.
        /// </summary>
        private void Update()
        {
            if (!isConfirmOpen)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ConfirmDismissed?.Invoke();
            }
        }

        private IEnumerator CaptureBackdrop()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            ReleaseBackdrop();
            backdrop = ScreenBlur.Capture(
                CharacterClosetStyle.Modal.BackdropHalvings,
                CharacterClosetStyle.Modal.BackdropBlur);
            backdropImage.texture = backdrop;
            backdropImage.uvRect = ScreenBlur.UvRect;
            backdropImage.enabled = true;
            backdropRoutine = null;
        }

        private void ReleaseBackdrop()
        {
            if (backdrop == null)
            {
                return;
            }

            backdropImage.texture = null;
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
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var blurRect = CreateRect("Backdrop", root);
            SetAnchor(blurRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            blurRect.offsetMin = Vector2.zero;
            blurRect.offsetMax = Vector2.zero;
            backdropImage = blurRect.gameObject.AddComponent<RawImage>();
            backdropImage.raycastTarget = false;
            backdropImage.enabled = false;

            // Takes every click that misses the panel and does nothing with
            // it: the design gives the outside no meaning.
            var dimRect = CreateRect("Dim", root);
            SetAnchor(dimRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            AddImage(dimRect, CharacterClosetStyle.Palette.Dim, raycastTarget: true);

            CreateConfirmPanel(root);

            root.gameObject.SetActive(false);
            confirmRoot = root.gameObject;
        }

        private void CreateConfirmPanel(RectTransform root)
        {
            var panel = CreateRect("Panel", root);
            SetAnchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = CharacterClosetStyle.Modal.PanelSize;
            AddImage(
                panel,
                CharacterClosetStyle.Palette.ModalFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.Modal.PanelRadius),
                raycastTarget: true);

            var titleHeight = CharacterClosetStyle.Modal.TitleFontSize * 1.4f;
            var subtitleHeight = CharacterClosetStyle.Modal.SubtitleFontSize * 1.4f;

            var title = CreateText(
                "Title",
                panel,
                Copy(UiText.Closet.ResetTitle),
                CharacterClosetStyle.Modal.TitleFontSize,
                CharacterClosetStyle.Palette.ModalTitle,
                TextAlignmentOptions.Top);
            SetAnchor(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            title.anchoredPosition = new Vector2(0f, -CharacterClosetStyle.Modal.TitleTop);
            title.sizeDelta = new Vector2(0f, titleHeight);
            confirmTitle = title.GetComponent<TMP_Text>();

            var subtitleTop = CharacterClosetStyle.Modal.TitleTop
                              + titleHeight
                              + CharacterClosetStyle.Modal.SubtitleGap;
            var subtitle = CreateText(
                "Subtitle",
                panel,
                Copy(UiText.Closet.Subtitle),
                CharacterClosetStyle.Modal.SubtitleFontSize,
                CharacterClosetStyle.Palette.ModalSubtitle,
                TextAlignmentOptions.Top,
                HomeUiFonts.ApplyRegular());
            SetAnchor(subtitle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            subtitle.anchoredPosition = new Vector2(0f, -subtitleTop);
            subtitle.sizeDelta = new Vector2(0f, subtitleHeight);
            confirmSubtitle = subtitle.GetComponent<TMP_Text>();

            var buttonTop = subtitleTop
                            + subtitleHeight
                            + CharacterClosetStyle.Modal.ButtonGapAbove;
            var half = (CharacterClosetStyle.Modal.ButtonSize.x
                        + CharacterClosetStyle.Modal.ButtonGap) * 0.5f;

            confirmDecline = CreateConfirmButton(
                panel,
                "DeclineButton",
                new Vector2(-half, -buttonTop),
                Copy(UiText.Closet.Decline),
                CharacterClosetStyle.Palette.DeclineFill,
                CharacterClosetStyle.Palette.DeclineHoverFill,
                CharacterClosetStyle.Palette.DeclineLabel,
                () => ConfirmDismissed?.Invoke());

            confirmAccept = CreateConfirmButton(
                panel,
                "AcceptButton",
                new Vector2(half, -buttonTop),
                Copy(UiText.Closet.Accept),
                CharacterClosetStyle.Palette.AcceptFill,
                CharacterClosetStyle.Palette.AcceptHoverFill,
                CharacterClosetStyle.Palette.AcceptLabel,
                () => ConfirmAccepted?.Invoke());

            CreateCloseButton(panel);
        }

        private TMP_Text CreateConfirmButton(
            RectTransform panel,
            string name,
            Vector2 position,
            string label,
            Color fillColor,
            Color hoverColor,
            Color labelColor,
            Action clicked)
        {
            var rect = CreateRect(name, panel);
            SetAnchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rect.anchoredPosition = position;
            rect.sizeDelta = CharacterClosetStyle.Modal.ButtonSize;

            var fill = AddImage(
                rect,
                fillColor,
                HomeUiFonts.Rounded(CharacterClosetStyle.Modal.ButtonRadius),
                raycastTarget: true);

            var labelRect = CreateText(
                "Label",
                rect,
                label,
                CharacterClosetStyle.Modal.ButtonFontSize,
                labelColor,
                TextAlignmentOptions.Center);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var hover = rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(fill, null, fillColor, hoverColor);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            buttons.Add(button);
            return labelRect.GetComponent<TMP_Text>();
        }

        private void CreateCloseButton(RectTransform panel)
        {
            var rect = CreateRect("CloseButton", panel);
            SetAnchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(
                -CharacterClosetStyle.Modal.CloseOffset.x,
                -CharacterClosetStyle.Modal.CloseOffset.y);
            rect.sizeDelta = new Vector2(
                CharacterClosetStyle.Modal.CloseSize, CharacterClosetStyle.Modal.CloseSize);

            var image = AddImage(
                rect, CharacterClosetStyle.Palette.CloseIcon, raycastTarget: true);
            SettingsStyle.ApplyCloseIcon(image, closeIcon);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ConfirmDismissed?.Invoke());
            buttons.Add(button);
        }
    }
}
