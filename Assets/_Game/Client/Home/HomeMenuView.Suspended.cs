using Game.Core.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// The notice shown when the account is suspended (S15P21D205-924).
    /// </summary>
    /// <remarks>
    /// Before this, a suspended player reached the home screen and watched every
    /// button fail in silence — the friend list empty, room creation refused,
    /// invites going nowhere — with nothing anywhere saying why.
    /// <para>
    /// It covers the screen and cannot be dismissed. The panels beside it close
    /// on a click outside; this one has nothing to go back to, because every
    /// action behind it is refused by the server. The one control it carries is
    /// the way out of the game: without it a suspended player could not even
    /// quit, because the notice swallows the 게임 종료 label behind it.
    /// </para>
    /// </remarks>
    public sealed partial class HomeMenuView
    {
        public const string SuspendedTitle = "이용이 제한된 계정입니다";
        public const string SuspendedBody = "운영자가 이 계정의 이용을 중지했습니다.";

        /// <summary>
        /// The same words as the label in the corner, because it does the same
        /// thing — and that label is behind the scrim while this is up.
        /// </summary>
        public const string SuspendedQuitLabel = "게임 종료";

        /// <summary>
        /// Darker than <see cref="HomeStyle.Palette.PanelFill"/> and covering the
        /// whole screen, so what is behind reads as out of reach rather than as
        /// something to try.
        /// </summary>
        private static readonly Color SuspendedScrim = new Color(0f, 0f, 0f, 0.82f);

        private const float SuspendedPanelWidth = 560f;

        /// <summary>
        /// Taller than the two lines need, by the height of the quit button and
        /// the air around it.
        /// </summary>
        private const float SuspendedPanelHeight = 300f;

        private const float SuspendedSidePadding = 40f;
        private const float SuspendedTitleSize = 28f;
        private const float SuspendedBodySize = 20f;
        private const float SuspendedQuitSize = 22f;
        private static readonly Vector2 SuspendedQuitButtonSize = new Vector2(220f, 52f);
        private const float SuspendedQuitBottom = 36f;

        private GameObject suspendedRoot;

        /// <summary>
        /// Shows or hides the notice. Idempotent.
        /// </summary>
        /// <remarks>
        /// Only the presenter decides when, and only for
        /// <c>BackendFailure.Suspended</c>. Showing it for a timeout would tell
        /// someone on a bad connection that they had been suspended.
        /// </remarks>
        public void SetSuspendedNoticeVisible(bool visible)
        {
            if (suspendedRoot != null)
            {
                suspendedRoot.SetActive(visible);
            }
        }

        /// <summary>True while the notice is up. For tests and for callers that
        /// want to leave it alone once it is showing.</summary>
        public bool IsSuspendedNoticeVisible =>
            suspendedRoot != null && suspendedRoot.activeSelf;

        private void BuildSuspendedNotice(RectTransform parent)
        {
            var root = CreateRect("SuspendedNotice", parent);
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // raycastTarget on the scrim is the whole point: it swallows the
            // clicks that would otherwise reach the menu behind it. Without it
            // the notice would be a picture over live buttons.
            AddImage(root, SuspendedScrim, raycastTarget: true);

            // No dismiss area and no close button. Either would be a control
            // that does nothing: the account is refused by the server, and
            // asking again answers the same. Quitting is the exception — it is
            // the one thing left that still works.

            var panel = CreateRect("Panel", root);
            SetAnchor(
                panel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(SuspendedPanelWidth, SuspendedPanelHeight);

            var fill = AddImage(
                panel,
                HomeStyle.Palette.PanelFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Modal),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            CreateSuspendedLine(
                panel,
                "Title",
                SuspendedTitle,
                SuspendedTitleSize,
                FontStyles.Bold,
                HomeStyle.Palette.TextPrimary,
                topOffset: -52f);

            CreateSuspendedLine(
                panel,
                "Body",
                SuspendedBody,
                SuspendedBodySize,
                FontStyles.Normal,
                HomeStyle.Palette.TextPrimary,
                topOffset: -116f);

            CreateSuspendedQuitButton(panel);

            suspendedRoot = root.gameObject;

            // Built hidden. A player who is not suspended must never see this
            // flash on the way in, and the presenter turns it on after sign-in
            // has answered.
            suspendedRoot.SetActive(false);
        }

        /// <summary>
        /// The way out of the game, inside the notice.
        /// </summary>
        /// <remarks>
        /// A plate rather than the bare label the corner uses: it is the only
        /// thing on this screen that can be pressed, and it has to read that way
        /// against a panel with nothing else on it.
        /// <para>
        /// It raises <see cref="HomeMenuAction.Quit"/> like the corner label, so
        /// the presenter closes the game the one way it already knows how.
        /// </para>
        /// </remarks>
        private void CreateSuspendedQuitButton(RectTransform panel)
        {
            var rect = CreateRect("SuspendedQuitButton", panel);
            SetAnchor(
                rect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f));
            rect.anchoredPosition = new Vector2(0f, SuspendedQuitBottom);
            rect.sizeDelta = SuspendedQuitButtonSize;

            var fill = AddImage(
                rect,
                HomeStyle.Palette.ApplyOnFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Control),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            var labelRect = CreateRect("Label", rect);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = AddText(
                labelRect,
                SuspendedQuitLabel,
                SuspendedQuitSize,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            ApplyMenuFont(label);
            label.color = HomeStyle.Palette.ApplyOnLabel;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ActionClicked?.Invoke(HomeMenuAction.Quit));
            menuButtons.Add(button);

            rect.gameObject.AddComponent<HomeHoverHighlight>()
                .Bind(fill, null, HomeStyle.Palette.ApplyOnFill, HomeStyle.Palette.ToggleOnFill);
        }

        private void CreateSuspendedLine(
            RectTransform panel,
            string name,
            string content,
            float fontSize,
            FontStyles style,
            Color color,
            float topOffset)
        {
            var rect = CreateRect(name, panel);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rect.offsetMin = new Vector2(SuspendedSidePadding, 0f);
            rect.offsetMax = new Vector2(-SuspendedSidePadding, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, fontSize * 1.6f);
            rect.anchoredPosition = new Vector2(0f, topOffset);

            var text = AddText(rect, content, fontSize, style, TextAlignmentOptions.Center);
            text.color = color;
            text.gameObject.SetActive(true);
        }
    }
}
