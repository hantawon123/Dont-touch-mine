using Game.Core.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// The notice shown when a newer build is on the download page
    /// (S15P21D205-1109).
    /// </summary>
    /// <remarks>
    /// Built like the suspended notice and for the same reason: behind it every
    /// room is out of reach. An old build sits in a Photon partition of its own,
    /// so the room list comes back empty and a code never resolves - and before
    /// this, nothing said why. It cannot be dismissed; the two things it offers
    /// are the only two that still help.
    /// </remarks>
    public sealed partial class HomeMenuView
    {
        public static string UpdateTitle =>
            UiTextCatalog.Shipped.Get(UiText.Home.UpdateTitle, "ko");

        public static string UpdateBody =>
            UiTextCatalog.Shipped.Get(UiText.Home.UpdateBody, "ko");

        /// <summary>
        /// Taller than the suspended panel by the second line of the body.
        /// </summary>
        private const float UpdatePanelHeight = 330f;

        private const float UpdateButtonGap = 20f;

        private GameObject updateRoot;
        private string updateDownloadUrl;

        /// <summary>
        /// Puts the notice up. Idempotent, and nothing takes it down: the build
        /// does not change until the player quits and runs the new one.
        /// </summary>
        public void ShowUpdateNotice(string downloadUrl)
        {
            updateDownloadUrl = downloadUrl;
            if (updateRoot != null)
            {
                updateRoot.SetActive(true);
            }
        }

        /// <summary>True while the notice is up. For tests.</summary>
        public bool IsUpdateNoticeVisible =>
            updateRoot != null && updateRoot.activeSelf;

        /// <summary>Where the download button goes. For tests.</summary>
        public string UpdateDownloadUrl => updateDownloadUrl;

        private void BuildUpdateNotice(RectTransform parent)
        {
            var root = CreateRect("UpdateNotice", parent);
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // Swallows the clicks meant for the menu behind, as the suspended
            // notice's scrim does.
            AddImage(root, SuspendedScrim, raycastTarget: true);

            var panel = CreateRect("Panel", root);
            SetAnchor(
                panel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(SuspendedPanelWidth, UpdatePanelHeight);

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
                UiText.Home.UpdateTitle,
                SuspendedTitleSize,
                FontStyles.Bold,
                HomeStyle.Palette.TextPrimary,
                topOffset: -52f);

            // Two lines, so twice the height the shared helper gives one.
            CreateSuspendedLine(
                panel,
                "Body",
                UiText.Home.UpdateBody,
                SuspendedBodySize,
                FontStyles.Normal,
                HomeStyle.Palette.TextPrimary,
                topOffset: -112f);
            var body = (RectTransform)panel.Find("Body");
            body.sizeDelta = new Vector2(body.sizeDelta.x, SuspendedBodySize * 3.2f);

            var offset = (SuspendedQuitButtonSize.x + UpdateButtonGap) / 2f;
            CreateUpdateButton(
                panel,
                "UpdateDownloadButton",
                UiText.Home.UpdateDownload,
                -offset,
                HomeStyle.Palette.ApplyOnFill,
                HomeStyle.Palette.ToggleOnFill,
                OpenDownloadPage);

            // Quits the way the corner label does, so the presenter closes the
            // game the one way it already knows how.
            CreateUpdateButton(
                panel,
                "UpdateQuitButton",
                UiText.Home.Quit,
                offset,
                HomeStyle.Palette.ButtonFill,
                HomeStyle.Palette.HoverFill,
                () => ActionClicked?.Invoke(HomeMenuAction.Quit));

            updateRoot = root.gameObject;

            // Built hidden, for the same reason the suspended notice is: a
            // player on the current build must never see it flash past.
            updateRoot.SetActive(false);
        }

        /// <remarks>
        /// Opened here rather than through the presenter. It leaves the game for
        /// the browser and changes nothing on this screen, so there is no state
        /// for a presenter to keep - and routing it there would add a member to
        /// every application host for one button.
        /// </remarks>
        private void OpenDownloadPage()
        {
            if (!string.IsNullOrEmpty(updateDownloadUrl))
            {
                Application.OpenURL(updateDownloadUrl);
            }
        }

        private void CreateUpdateButton(
            RectTransform panel,
            string name,
            string key,
            float x,
            Color idle,
            Color hover,
            UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(name, panel);
            SetAnchor(
                rect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f));
            rect.anchoredPosition = new Vector2(x, SuspendedQuitBottom);
            rect.sizeDelta = SuspendedQuitButtonSize;

            var fill = AddImage(
                rect,
                idle,
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
                Copy(key),
                SuspendedQuitSize,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            ApplyMenuFont(label);
            label.color = HomeStyle.Palette.ApplyOnLabel;
            Remember(label, key);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            menuButtons.Add(button);

            rect.gameObject.AddComponent<HomeHoverHighlight>()
                .Bind(fill, null, idle, hover);
        }
    }
}
