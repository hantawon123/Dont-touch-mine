using System;
using System.Collections.Generic;
using Game.Core.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// The region picker that hangs under the globe button.
    /// </summary>
    /// <remarks>
    /// Two ways out, like the profile panel: the globe that opened it, or
    /// anywhere else on the screen. The catcher behind it does not eat the
    /// press on the globe because the buttons are built after the panels and
    /// Unity hit-tests later siblings first.
    /// </remarks>
    public sealed partial class HomeMenuView
    {
        /// <summary>
        /// Raised with the code of the region the player picked.
        /// </summary>
        public event Action<string> RegionSelected;

        /// <summary>
        /// Raised when the player presses somewhere that is not the picker.
        /// </summary>
        public event Action ServerSettingsDismissed;

        private readonly List<RegionRow> regionRows = new List<RegionRow>();
        private RectTransform serverPanel;
        private TMP_Text serverTitleText;

        private readonly struct RegionRow
        {
            public RegionRow(ServerRegion region, TMP_Text label, Image check)
            {
                Region = region;
                Label = label;
                Check = check;
            }

            public ServerRegion Region { get; }
            public TMP_Text Label { get; }
            public Image Check { get; }
        }

        private void CreateServerSettingsRoot(RectTransform canvas)
        {
            var root = CreateRect("ServerSettingsRoot", canvas);
            root.gameObject.SetActive(false);
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

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
            dismiss.onClick.AddListener(() => ServerSettingsDismissed?.Invoke());
            menuButtons.Add(dismiss);

            var panel = CreateRect("ServerSettingsPanel", root);
            SetAnchor(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            panel.anchoredPosition = new Vector2(
                -HomeStyle.Server.PanelRightMargin, -HomeStyle.Server.PanelTopMargin);
            panel.sizeDelta = HomeStyle.Server.PanelSize;

            var fill = AddImage(
                panel,
                HomeStyle.Palette.PanelFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Panel, SquareCorner.TopRight),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            serverPanel = panel;
            CreateServerTitle(panel);
            CreateRegionRows(panel);
            FitServerPanel();

            serverSettingsRoot = root.gameObject;
            SetSelectedRegion(ServerRegionCatalog.Default.Code);
        }

        private void CreateServerTitle(RectTransform panel)
        {
            var title = CreateRect("Title", panel);
            SetAnchor(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            title.offsetMin = new Vector2(HomeStyle.Server.SidePadding, 0f);
            title.offsetMax = new Vector2(-HomeStyle.Server.SidePadding, 0f);
            title.anchoredPosition = new Vector2(0f, -HomeStyle.Server.VerticalPadding);
            title.sizeDelta = new Vector2(title.sizeDelta.x, HomeStyle.Server.TitleHeight);

            var text = AddText(
                title,
                Copy(UiText.Home.ServerTitle),
                HomeStyle.FontSize.ServerTitle,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            ApplyMenuFont(text);
            text.color = HomeStyle.Palette.TextPrimary;
            serverTitleText = text;
            Remember(text, UiText.Home.ServerTitle);
        }

        /// <summary>
        /// The regions, in a strip that scrolls when there are more of them
        /// than the panel is tall.
        /// </summary>
        /// <remarks>
        /// Photon offers far more regions than the four the design drew, so the
        /// list is built to outgrow the panel rather than to fit it. There is no
        /// visible scrollbar: the design has none, and the rows are cut off
        /// mid-height at the bottom edge, which is its own hint that there is
        /// more below.
        /// </remarks>
        private void CreateRegionRows(RectTransform panel)
        {
            var viewport = CreateRect("Viewport", panel);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = new Vector2(
                HomeStyle.Server.SidePadding, HomeStyle.Server.VerticalPadding);
            viewport.offsetMax = new Vector2(
                -HomeStyle.Server.SidePadding, -HomeStyle.Server.RowsTop);
            viewport.gameObject.AddComponent<RectMask2D>();

            var regions = ServerRegionCatalog.All;
            var content = CreateRect("Content", viewport);
            SetAnchor(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(
                0f,
                (regions.Count * HomeStyle.Server.RowHeight)
                    + ((regions.Count - 1) * HomeStyle.Server.RowGap));

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;

            // Clamped rather than elastic: a four-line list that bounces reads
            // as a bug rather than as polish.
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = HomeStyle.Server.RowHeight;

            for (var index = 0; index < regions.Count; index++)
            {
                CreateRegionRow(content, regions[index], index);
            }
        }

        private void CreateRegionRow(RectTransform content, ServerRegion region, int index)
        {
            var row = CreateRect(RegionRowName(region), content);
            SetAnchor(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            row.offsetMin = new Vector2(0f, 0f);
            row.offsetMax = new Vector2(0f, 0f);
            row.anchoredPosition = new Vector2(
                0f, -index * (HomeStyle.Server.RowHeight + HomeStyle.Server.RowGap));
            row.sizeDelta = new Vector2(0f, HomeStyle.Server.RowHeight);

            var hover = AddImage(
                row,
                Color.clear,
                HomeUiFonts.Rounded(HomeStyle.Server.RowRadius),
                raycastTarget: true);
            hover.type = Image.Type.Sliced;
            hover.pixelsPerUnitMultiplier = 1f;

            var labelRect = CreateRect("Label", row);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = new Vector2(HomeStyle.Server.SidePadding * 0.5f, 0f);
            labelRect.offsetMax = new Vector2(-HomeStyle.Server.CheckSize, 0f);
            var label = AddText(
                labelRect,
                RegionLabel(region),
                HomeStyle.FontSize.Region,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            label.color = HomeStyle.Palette.TextPrimary;

            var checkRect = CreateRect("Check", row);
            SetAnchor(
                checkRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            checkRect.anchoredPosition = new Vector2(-HomeStyle.Server.SidePadding * 0.5f, 0f);
            checkRect.sizeDelta = new Vector2(
                HomeStyle.Server.CheckSize, HomeStyle.Server.CheckSize);

            // The mark is drawn in the accent colour already, so it is left
            // white here rather than tinted back to it.
            var check = AddImage(checkRect, Color.white, checkIcon);
            check.preserveAspect = true;
            check.enabled = false;

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hover;
            button.transition = Selectable.Transition.None;

            // The mark moves on the press rather than waiting to be told to.
            // Which region is in use is a local setting, so there is nothing to
            // fail and nothing to wait for.
            var code = region.Code;
            button.onClick.AddListener(() =>
            {
                SetSelectedRegion(code);
                RegionSelected?.Invoke(code);
            });
            menuButtons.Add(button);

            row.gameObject.AddComponent<HomeHoverHighlight>()
                .Bind(hover, null, Color.clear, HomeStyle.Palette.RowHover);

            regionRows.Add(new RegionRow(region, label, check));
        }

        public void SetServerSettingsVisible(bool visible)
        {
            SetActionSelected(HomeMenuAction.ServerSettings, visible);
            if (serverSettingsRoot != null)
            {
                serverSettingsRoot.SetActive(visible);
            }
        }

        /// <summary>
        /// Lights the row for the given region and puts the mark beside it.
        /// </summary>
        /// <remarks>
        /// A code that matches nothing leaves every row unlit rather than
        /// guessing at one, so a region the catalogue has lost is visible as a
        /// picker with nothing chosen instead of silently reading as another
        /// region.
        /// </remarks>
        public void SetSelectedRegion(string code)
        {
            for (var index = 0; index < regionRows.Count; index++)
            {
                var row = regionRows[index];
                var isSelected = string.Equals(row.Region.Code, code, StringComparison.Ordinal);
                row.Label.color = isSelected
                    ? HomeStyle.Palette.Accent
                    : HomeStyle.Palette.TextPrimary;
                if (row.Check != null)
                {
                    row.Check.enabled = isSelected && checkIcon != null;
                }
            }
        }

        /// <summary>
        /// The region's name in the applied language, or the one the
        /// catalogue of regions was written with when there is no line for it.
        /// </summary>
        private string RegionLabel(ServerRegion region)
        {
            var key = UiText.Home.Region(region.Code);
            return UiTextCatalog.Shipped.Knows(key) ? Copy(key) : region.DisplayName;
        }

        /// <summary>
        /// Writes the regions again and gives the panel the width the longest
        /// of them needs.
        /// </summary>
        private void RepaintRegions()
        {
            for (var index = 0; index < regionRows.Count; index++)
            {
                var row = regionRows[index];
                if (row.Label != null)
                {
                    row.Label.text = RegionLabel(row.Region);
                }
            }

            FitServerPanel();
        }

        /// <summary>
        /// The panel is as wide as its widest line asks for, never narrower
        /// than the design. 북미 is four characters and North America is
        /// thirteen, and the check mark sits where the overflow would go.
        /// </summary>
        private void FitServerPanel()
        {
            if (serverPanel == null)
            {
                return;
            }

            var needed = HomeStyle.Server.PanelSize.x;
            if (serverTitleText != null)
            {
                needed = Mathf.Max(
                    needed,
                    TextWidth(serverTitleText) + (HomeStyle.Server.SidePadding * 2f));
            }

            for (var index = 0; index < regionRows.Count; index++)
            {
                var label = regionRows[index].Label;
                if (label != null)
                {
                    needed = Mathf.Max(
                        needed, TextWidth(label) + HomeStyle.Server.RowChrome);
                }
            }

            serverPanel.sizeDelta = new Vector2(
                Mathf.Ceil(needed), HomeStyle.Server.PanelSize.y);
        }

        private static float TextWidth(TMP_Text text) =>
            text.GetPreferredValues(text.text).x;

        private static string RegionRowName(ServerRegion region)
        {
            return $"Region_{region.Code}";
        }
    }
}
