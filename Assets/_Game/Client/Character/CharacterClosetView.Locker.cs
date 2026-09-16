using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Character
{
    /// <summary>
    /// The two halves the player works with: the rail of categories on the left
    /// and the locker of parts on the right.
    /// </summary>
    /// <remarks>
    /// Kept apart from the rest of the screen because these are the only pieces
    /// the presenter refills. The background, the arrow and the buttons are
    /// built once and never told anything.
    /// </remarks>
    public sealed partial class CharacterClosetView
    {
        private RectTransform tabRail;
        private RectTransform lockerContent;
        private ScrollRect lockerScroll;
        private readonly List<CategoryTab> tabs = new List<CategoryTab>();
        private readonly List<PartCell> cells = new List<PartCell>();
        private AvatarPartCategory shownCategory;
        private bool hasShownCategory;

        public void ShowCategories(IReadOnlyList<AvatarPartGroup> groups)
        {
            if (tabRail == null)
            {
                return;
            }

            ClearTabs();
            if (groups == null)
            {
                return;
            }

            hasHoodColor = false;
            var visibleCount = 0;
            foreach (var group in groups)
            {
                if (group == null) continue;
                if (group.Category == AvatarPartCategory.HoodColor) hasHoodColor = true;
                else visibleCount++;
            }
            var origin = TabsOrigin;
            var height = Mathf.Min(CharacterClosetStyle.Tabs.Size.y,
                (CharacterClosetStyle.Locker.Size.y - CharacterClosetStyle.Tabs.Gap * (visibleCount - 1)) / Mathf.Max(1, visibleCount));
            var step = height + CharacterClosetStyle.Tabs.Gap;
            var visibleIndex = 0;
            for (var index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                if (group == null || group.Category == AvatarPartCategory.HoodColor)
                {
                    continue;
                }

                tabs.Add(CreateTab(
                    group, new Vector2(origin.x, origin.y - (step * visibleIndex++)), height));
            }

            if (hasShownCategory)
            {
                MarkActiveTab(shownCategory);
            }
        }

        public void ShowParts(AvatarPartGroup group, string selectedPartId)
        {
            if (lockerContent == null)
            {
                return;
            }

            ClearCells();
            UpdateHoodTabs(group?.Category);
            if (group == null)
            {
                return;
            }

            shownCategory = group.Category;
            hasShownCategory = true;
            MarkActiveTab(group.Category);

            foreach (var part in group.Parts)
            {
                if (part != null && !string.IsNullOrEmpty(part.Id))
                {
                    cells.Add(CreatePartCell(group, part));
                }
            }

            // A category is looked at from the top however it was left.
            if (lockerScroll != null)
            {
                lockerScroll.verticalNormalizedPosition = 1f;
            }

            ShowSelectedPart(group.Category, selectedPartId);
        }

        public void ShowSelectedPart(AvatarPartCategory category, string partId)
        {
            if (!hasShownCategory || category != shownCategory)
            {
                return;
            }

            var wanted = partId ?? AvatarAppearance.NoPart;
            foreach (var cell in cells)
            {
                if (cell.Stroke != null)
                {
                    cell.Stroke.enabled =
                        string.Equals(cell.PartId, wanted, StringComparison.Ordinal);
                }
            }
        }

        private void CreateTabRail(RectTransform canvas)
        {
            tabRail = CreateRect("CategoryRail", canvas);
            SetAnchor(tabRail, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            tabRail.anchoredPosition = Vector2.zero;
            tabRail.sizeDelta = CharacterClosetStyle.Tabs.Size;
        }

        private CategoryTab CreateTab(AvatarPartGroup group, Vector2 position, float height)
        {
            var rect = CreateRect($"Tab_{group.Category}", tabRail);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(CharacterClosetStyle.Tabs.Size.x, height);

            var fill = AddImage(
                rect,
                CharacterClosetStyle.Palette.TabFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.Radius.Tab),
                raycastTarget: true);

            var strokeRect = CreateRect("Stroke", rect);
            SetAnchor(strokeRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            strokeRect.offsetMin = Vector2.zero;
            strokeRect.offsetMax = Vector2.zero;
            var stroke = AddImage(
                strokeRect,
                CharacterClosetStyle.Palette.TabSelectedStroke,
                HomeUiFonts.Outline(
                    CharacterClosetStyle.Radius.Tab,
                    CharacterClosetStyle.Tabs.SelectedStroke));
            stroke.enabled = false;

            var iconLeft = CharacterClosetStyle.Tabs.IconLeft(group.Category);
            var iconSize = CharacterClosetStyle.Tabs.IconSize(group.Category);
            if (group.Icon != null)
            {
                var iconRect = CreateRect("Icon", rect);
                SetAnchor(
                    iconRect,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f));
                iconRect.anchoredPosition = new Vector2(iconLeft, 0f);
                iconRect.sizeDelta = iconSize;
                var icon = AddImage(iconRect, Color.white);
                icon.sprite = group.Icon;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
            }

            var label = CreateText(
                "Label",
                rect,
                group.Category == AvatarPartCategory.Hood ? "후드" : group.Label,
                CharacterClosetStyle.Tabs.FontSize,
                CharacterClosetStyle.Palette.TextPrimary,
                TextAlignmentOptions.Left);
            SetAnchor(label, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
            label.offsetMin = new Vector2(
                group.Icon != null
                    ? iconLeft + iconSize.x + CharacterClosetStyle.Tabs.LabelGap
                    : iconLeft,
                0f);
            label.offsetMax = new Vector2(-16f, 0f);

            var hover = rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(
                fill,
                null,
                CharacterClosetStyle.Palette.TabFill,
                CharacterClosetStyle.Palette.TabHoverFill);

            var category = group.Category;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => CategorySelected?.Invoke(category));
            buttons.Add(button);

            return new CategoryTab(
                category, stroke, label.GetComponent<TMP_Text>(), button);
        }

        /// <summary>
        /// The panel on the right and the scrolling grid inside it.
        /// </summary>
        /// <remarks>
        /// A grid layout rather than placed cells: the number of parts is the
        /// one thing about this screen that is expected to keep changing, and
        /// three across with the panel's padding is the whole rule.
        /// </remarks>
        private void CreateLocker(RectTransform canvas)
        {
            var panel = CreateRect("Locker", canvas);
            SetAnchor(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            panel.anchoredPosition = new Vector2(-LockerMargin.x, -LockerMargin.y);
            panel.sizeDelta = CharacterClosetStyle.Locker.Size;
            AddImage(
                panel,
                CharacterClosetStyle.Palette.LockerFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.Radius.Locker),
                raycastTarget: true);

            var viewport = CreateRect("Viewport", panel);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = new Vector2(
                CharacterClosetStyle.Locker.PaddingHorizontal,
                CharacterClosetStyle.Locker.PaddingVertical);
            viewport.offsetMax = new Vector2(
                -CharacterClosetStyle.Locker.PaddingHorizontal,
                -CharacterClosetStyle.Locker.PaddingVertical);
            viewport.gameObject.AddComponent<RectMask2D>();

            lockerContent = CreateRect("Content", viewport);
            SetAnchor(lockerContent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            lockerContent.anchoredPosition = Vector2.zero;
            lockerContent.sizeDelta = Vector2.zero;

            var grid = lockerContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(
                CharacterClosetStyle.Locker.CellSize, CharacterClosetStyle.Locker.CellSize);
            grid.spacing = new Vector2(
                CharacterClosetStyle.Locker.CellGap, CharacterClosetStyle.Locker.CellGap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CharacterClosetStyle.Locker.Columns;
            grid.childAlignment = TextAnchor.UpperLeft;

            var fitter = lockerContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            lockerScroll = panel.gameObject.AddComponent<ScrollRect>();
            lockerScroll.content = lockerContent;
            lockerScroll.viewport = viewport;
            lockerScroll.horizontal = false;
            lockerScroll.vertical = true;
            lockerScroll.movementType = ScrollRect.MovementType.Clamped;
            lockerScroll.scrollSensitivity = 24f;
            lockerScroll.verticalScrollbar = CreateLockerScrollbar(panel);
            lockerScroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
            CreateHoodTabs(panel);
        }

        /// <summary>
        /// The handle down the inside of the locker's right edge.
        /// </summary>
        /// <remarks>
        /// ScrollRect hides the track when every part fits in the viewport.
        /// Keep the viewport width fixed so toggling visibility does not move the grid.
        /// </remarks>
        private Scrollbar CreateLockerScrollbar(RectTransform panel)
        {
            var track = CreateRect("Scrollbar", panel);
            SetAnchor(track, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            track.anchoredPosition = new Vector2(
                -CharacterClosetStyle.Locker.ScrollbarInset, 0f);
            track.sizeDelta = new Vector2(
                CharacterClosetStyle.Locker.ScrollbarWidth,
                -CharacterClosetStyle.Locker.PaddingVertical * 2f);

            var area = CreateRect("SlidingArea", track);
            SetAnchor(area, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            var handle = CreateRect("Handle", area);

            // Stretched to the track before the scrollbar takes it over: the
            // scrollbar drives the axis it scrolls and leaves the other one at
            // whatever the rect was built with, which by default is 100 pixels
            // of nothing.
            SetAnchor(handle, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            handle.sizeDelta = Vector2.zero;

            var handleImage = AddImage(
                handle,
                CharacterClosetStyle.Palette.ScrollbarHandle,
                HomeUiFonts.Rounded(
                    Mathf.RoundToInt(CharacterClosetStyle.Locker.ScrollbarWidth)),
                raycastTarget: true);

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.transition = Selectable.Transition.None;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        /// <summary>
        /// One cell of the locker.
        /// </summary>
        /// <remarks>
        /// Three ways of showing a part, in order of how much the art knows:
        /// its own picture, the category's silhouette painted in the part's
        /// colour, or the colour alone. The middle one is what the closet
        /// looks like today — a body colour reads as a coloured body rather
        /// than as a coloured square, and it costs no new art.
        /// </remarks>
        private PartCell CreatePartCell(AvatarPartGroup group, AvatarPart part)
        {
            var cell = CreateCellPlate($"Cell_{part.Id}");
            var tinted = part.Thumbnail == null && group.Icon != null;
            var inset = part.Thumbnail != null
                ? CharacterClosetStyle.Locker.ThumbnailInset
                : CharacterClosetStyle.Locker.IconInset;

            var content = CreateRect("Thumbnail", cell.Rect);
            SetAnchor(content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            content.offsetMin = new Vector2(inset, inset);
            content.offsetMax = new Vector2(-inset, -inset);

            if (part.Thumbnail != null && group.Category == AvatarPartCategory.Face)
            {
                // An inset rounded rectangle shares the selection ring's corner centres.
                var radius = Mathf.Max(1, Mathf.RoundToInt(CharacterClosetStyle.Radius.Cell - inset));
                AddImage(content, Color.white, HomeUiFonts.Rounded(radius));
                content.gameObject.AddComponent<Mask>().showMaskGraphic = false;

                var picture = CreateRect("Picture", content);
                SetAnchor(picture, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                picture.offsetMin = Vector2.zero;
                picture.offsetMax = Vector2.zero;
                content = picture;
            }

            var image = AddImage(content, part.Swatch);
            if (part.Thumbnail != null)
            {
                image.sprite = part.Thumbnail;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else if (tinted)
            {
                // The icons are white silhouettes, so the tint multiplies
                // straight through to the part's colour.
                image.sprite = group.Icon;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
            else
            {
                // Nothing to draw the part as at all: the colour fills the
                // cell, which is enough to tell two of them apart.
                image.sprite = HomeUiFonts.Rounded(CharacterClosetStyle.Radius.Cell);
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            Bind(cell, group.Category, part.Id);
            return new PartCell(part.Id, cell.Stroke);
        }

        private CellPlate CreateCellPlate(string name)
        {
            var rect = CreateRect(name, lockerContent);
            var fill = AddImage(
                rect,
                CharacterClosetStyle.Palette.CellFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.Radius.Cell),
                raycastTarget: true);

            var strokeRect = CreateRect("Stroke", rect);
            SetAnchor(strokeRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            strokeRect.offsetMin = Vector2.zero;
            strokeRect.offsetMax = Vector2.zero;
            var stroke = AddImage(
                strokeRect,
                CharacterClosetStyle.Palette.CellSelectedStroke,
                HomeUiFonts.Outline(
                    CharacterClosetStyle.Radius.Cell,
                    CharacterClosetStyle.Locker.SelectedStroke));
            stroke.enabled = false;

            return new CellPlate(rect, fill, stroke);
        }

        private void Bind(CellPlate cell, AvatarPartCategory category, string partId)
        {
            var hover = cell.Rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(
                cell.Fill,
                null,
                CharacterClosetStyle.Palette.CellFill,
                CharacterClosetStyle.Palette.CellHoverFill);

            var button = cell.Rect.gameObject.AddComponent<Button>();
            button.targetGraphic = cell.Fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => PartSelected?.Invoke(category, partId));
            buttons.Add(button);
        }

        private void MarkActiveTab(AvatarPartCategory category)
        {
            if (category == AvatarPartCategory.HoodColor) category = AvatarPartCategory.Hood;
            foreach (var tab in tabs)
            {
                var selected = tab.Category == category;
                if (tab.Stroke != null)
                {
                    tab.Stroke.enabled = selected;
                }

                if (tab.Label != null)
                {
                    tab.Label.color = selected
                        ? CharacterClosetStyle.Palette.TabSelectedLabel
                        : CharacterClosetStyle.Palette.TextPrimary;
                }
            }
        }

        private void ClearTabs()
        {
            foreach (var tab in tabs)
            {
                if (tab.Button != null)
                {
                    tab.Button.onClick.RemoveAllListeners();
                    buttons.Remove(tab.Button);
                    Destroy(tab.Button.gameObject);
                }
            }

            tabs.Clear();
        }

        private void ClearCells()
        {
            if (lockerContent == null)
            {
                return;
            }

            for (var index = lockerContent.childCount - 1; index >= 0; index--)
            {
                var child = lockerContent.GetChild(index);
                var button = child.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    buttons.Remove(button);
                }

                Destroy(child.gameObject);
            }

            cells.Clear();
        }

        /// <summary>
        /// A built tab, kept so the selected one can be marked.
        /// </summary>
        private readonly struct CategoryTab
        {
            public CategoryTab(
                AvatarPartCategory category, Image stroke, TMP_Text label, Button button)
            {
                Category = category;
                Stroke = stroke;
                Label = label;
                Button = button;
            }

            public AvatarPartCategory Category { get; }

            public Image Stroke { get; }

            public TMP_Text Label { get; }

            public Button Button { get; }
        }

        /// <summary>A built cell, kept only so its ring can be turned on.</summary>
        private readonly struct PartCell
        {
            public PartCell(string partId, Image stroke)
            {
                PartId = partId;
                Stroke = stroke;
            }

            public string PartId { get; }

            public Image Stroke { get; }
        }

        /// <summary>The three pieces every cell is built from.</summary>
        private readonly struct CellPlate
        {
            public CellPlate(RectTransform rect, Image fill, Image stroke)
            {
                Rect = rect;
                Fill = fill;
                Stroke = stroke;
            }

            public RectTransform Rect { get; }

            public Image Fill { get; }

            public Image Stroke { get; }
        }
    }
}
