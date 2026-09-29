using System;
using System.Collections.Generic;
using Game.Client.Common;
using Game.Core.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// The friend panel: the list on one tab, requests on the other.
    /// </summary>
    /// <remarks>
    /// The search box and the refresh line sit above both tabs rather than
    /// inside either, because the design draws them once and both tabs use
    /// them. Only the list under them is swapped.
    /// </remarks>
    public sealed partial class HomeMenuView
    {
        /// <summary>
        /// Raised with the id of the friend whose incoming request was answered.
        /// </summary>
        public event Action<string> FriendRequestAccepted;

        public event Action<string> FriendRequestDeclined;

        /// <summary>
        /// A sent request cancelled from its search-result icon.
        /// </summary>
        public event Action<string> FriendRequestCancelled;

        /// <summary>
        /// A friendship ended through the friend's context menu.
        /// </summary>
        public event Action<string> FriendRemoved;

        public event Action FriendListRefreshRequested;

        private readonly List<FriendRow> friendRows = new List<FriendRow>();
        private readonly List<FriendRow> requestRows = new List<FriendRow>();
        private RectTransform friendContextRoot;
        private RectTransform friendContextButton;
        private string friendContextPlayerId;

        private void CloseFriendContextMenu()
        {
            friendContextPlayerId = null;
            if (friendContextRoot != null) friendContextRoot.gameObject.SetActive(false);
        }

        private void OpenFriendContextMenu(string playerId, PointerEventData pointer)
        {
            if (friendContextRoot == null)
            {
                friendContextRoot = CreateRect("FriendContextMenu", friendListRoot.transform);
                SetAnchor(friendContextRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                friendContextRoot.offsetMin = Vector2.zero;
                friendContextRoot.offsetMax = Vector2.zero;
                var dismiss = friendContextRoot.gameObject.AddComponent<Button>();
                dismiss.targetGraphic = AddImage(friendContextRoot, Color.clear, raycastTarget: true);
                dismiss.transition = Selectable.Transition.None;
                dismiss.onClick.AddListener(CloseFriendContextMenu);
                menuButtons.Add(dismiss);

                friendContextButton = CreateRect("RemoveFriend", friendContextRoot);
                SetAnchor(friendContextButton, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
                friendContextButton.sizeDelta = new Vector2(130f, 42f);
                var fill = AddImage(friendContextButton, Color.white, HomeUiFonts.Rounded(8), raycastTarget: true);
                fill.type = Image.Type.Sliced;
                var label = CreateRect("Label", friendContextButton);
                SetAnchor(label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                label.offsetMin = Vector2.zero;
                label.offsetMax = Vector2.zero;
                var text = AddText(label, Copy(UiText.Home.Unfriend), 18f, FontStyles.Normal, TextAlignmentOptions.Center);
                Remember(text, UiText.Home.Unfriend);
                text.color = new Color(1f, 0.3f, 0.3f);
                var remove = friendContextButton.gameObject.AddComponent<Button>();
                remove.targetGraphic = fill;
                remove.onClick.AddListener(() =>
                {
                    var target = friendContextPlayerId;
                    CloseFriendContextMenu();
                    if (target != null) FriendRemoved?.Invoke(target);
                });
                menuButtons.Add(remove);
            }

            friendContextPlayerId = playerId;
            friendContextRoot.gameObject.SetActive(true);
            friendContextRoot.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                friendContextRoot, pointer.position, pointer.pressEventCamera, out var point);
            var bounds = friendContextRoot.rect;
            point.x = Mathf.Clamp(point.x, bounds.xMin, bounds.xMax - friendContextButton.sizeDelta.x);
            point.y = Mathf.Clamp(point.y, bounds.yMin + friendContextButton.sizeDelta.y, bounds.yMax);
            friendContextButton.anchoredPosition = point;
        }

        /// <summary>
        /// One line of the list: an avatar, a name, and whatever belongs on the
        /// right of it.
        /// </summary>
        private sealed class FriendRow
        {
            public RectTransform Rect;
            public TMP_Text Name;
            public Image Trailing;
            public Image Accept;
            public Image Reject;
            public Button AcceptButton;
            public Button RejectButton;
            public Button Row;
        }

        private string FriendsEmptyMessage => Copy(UiText.Home.Empty);

        private void CreateFriendListRoot(RectTransform canvas)
        {
            var root = CreateRect("FriendListRoot", canvas);
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
            dismissButton = dismissRect.gameObject.AddComponent<Button>();
            dismissButton.targetGraphic = dismissImage;
            dismissButton.transition = Selectable.Transition.None;
            dismissButton.navigation = new Navigation { mode = Navigation.Mode.None };
            dismissButton.onClick.AddListener(() => FriendListDismissed?.Invoke());
            menuButtons.Add(dismissButton);

            var panel = CreateRect("FriendListPanel", root);
            SetAnchor(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            panel.anchoredPosition = new Vector2(
                -HomeStyle.Friends.PanelRightMargin, HomeStyle.Friends.PanelBottomMargin);
            panel.sizeDelta = HomeStyle.Friends.PanelSize;

            var fill = AddImage(
                panel,
                HomeStyle.Palette.PanelFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Panel, SquareCorner.BottomRight),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            CreateFriendTabs(panel);
            CreateFriendSearchRow(panel);
            CreateRefreshRow(panel);
            CreateFriendBodies(panel);

            friendListRoot = root.gameObject;
            SetFriendSearchVisible(false);
            SetIncomingRequestCount(0);
        }

        private void CreateFriendTabs(RectTransform panel)
        {
            var tabs = CreateRect("Tabs", panel);
            SetAnchor(tabs, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            tabs.offsetMin = new Vector2(HomeStyle.Friends.SidePadding, 0f);
            tabs.offsetMax = new Vector2(-HomeStyle.Friends.SidePadding, 0f);
            tabs.anchoredPosition = new Vector2(0f, -HomeStyle.Friends.VerticalPadding);
            tabs.sizeDelta = new Vector2(0f, HomeStyle.Friends.TabHeight);

            friendListTab = CreateTab(tabs, "친구 목록", Copy(UiText.Home.ListTab), 0f, 0.5f, () => FriendSearchClosed?.Invoke());
            Remember(friendListTab, UiText.Home.ListTab);
            friendRequestTab = CreateTab(tabs, "친구 요청", Copy(UiText.Home.RequestTab), 0.5f, 1f, () => FriendSearchOpened?.Invoke());
            Remember(friendRequestTab, UiText.Home.RequestTab);

            var divider = CreateRect("Divider", tabs);
            SetAnchor(divider, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            divider.anchoredPosition = Vector2.zero;
            divider.sizeDelta = new Vector2(
                HomeStyle.Friends.TabDividerThickness, HomeStyle.Friends.TabDividerHeight);
            AddImage(divider, HomeStyle.Palette.TabDivider);

            CreateBadge(friendRequestTab.transform.parent as RectTransform);

            // One rule across the whole panel, lit under whichever tab is
            // showing. Two separate underlines would leave a seam in the middle.
            listRule = CreateTabRule(tabs, 0f, 0.5f);
            requestRule = CreateTabRule(tabs, 0.5f, 1f);
        }

        /// <summary>
        /// One tab: a plate that takes the click, and a centred row holding the
        /// label and, on the request tab, its count.
        /// </summary>
        /// <remarks>
        /// The row is a layout group rather than a measured placement. Asking a
        /// label for its width before Unity has laid it out gives an answer
        /// that puts the badge on top of the text, and the group also recentres
        /// the label on its own when the badge is hidden.
        /// </remarks>
        private TMP_Text CreateTab(
            RectTransform parent, string name, string label, float min, float max, Action onClicked)
        {
            var tab = CreateRect(name, parent);
            SetAnchor(tab, new Vector2(min, 0f), new Vector2(max, 1f), new Vector2(0.5f, 0.5f));
            tab.offsetMin = Vector2.zero;
            tab.offsetMax = Vector2.zero;

            // The label goes on a child rather than on the tab itself: a
            // GameObject carries one Graphic, and the tab's is the invisible
            // plate that takes the click across its whole width.
            var hit = AddImage(tab, Color.clear, raycastTarget: true);

            var row = CreateRect("Content", tab);
            SetAnchor(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(0f, HomeStyle.Friends.TabHeight);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = HomeStyle.Friends.RefreshGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var labelRect = CreateRect("Label", row);
            var text = AddText(
                labelRect,
                label,
                HomeStyle.FontSize.Tab,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClicked());
            menuButtons.Add(button);
            return text;
        }

        private Image CreateTabRule(RectTransform tabs, float min, float max)
        {
            var rule = CreateRect("Rule", tabs);
            SetAnchor(rule, new Vector2(min, 0f), new Vector2(max, 0f), new Vector2(0.5f, 1f));
            rule.offsetMin = new Vector2(0f, 0f);
            rule.offsetMax = new Vector2(0f, 0f);
            rule.anchoredPosition = new Vector2(0f, 0f);
            rule.sizeDelta = new Vector2(0f, HomeStyle.Friends.TabRuleThickness);
            return AddImage(rule, HomeStyle.Palette.TabIdle);
        }

        /// <summary>
        /// The count that sits beside the tab's label, laid out by the same row.
        /// </summary>
        private void CreateBadge(RectTransform row)
        {
            var badge = CreateRect("Badge", row);
            badge.sizeDelta = new Vector2(
                HomeStyle.Friends.BadgeDiameter, HomeStyle.Friends.BadgeDiameter);
            var element = badge.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = HomeStyle.Friends.BadgeDiameter;
            element.preferredHeight = HomeStyle.Friends.BadgeDiameter;
            element.minWidth = HomeStyle.Friends.BadgeDiameter;
            AddImage(badge, HomeStyle.Palette.BadgeFill, HomeUiFonts.CircleSprite);

            var labelRect = CreateRect("Count", badge);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            requestBadgeText = AddText(
                labelRect,
                "N",
                HomeStyle.FontSize.Badge,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            ApplyMenuFont(requestBadgeText);
            requestBadgeText.color = HomeStyle.Palette.BadgeLabel;

            requestBadge = badge.gameObject;
        }

        private void CreateFriendSearchRow(RectTransform panel)
        {
            var row = CreateRect("SearchRow", panel);
            SetAnchor(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            row.anchoredPosition = new Vector2(0f, -HomeStyle.Friends.SearchTop);
            row.sizeDelta = new Vector2(
                HomeStyle.Friends.SearchRowWidth, HomeStyle.Friends.SearchRowHeight);

            var glyph = CreateRect("Glyph", row);
            SetAnchor(glyph, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            glyph.anchoredPosition = Vector2.zero;
            glyph.sizeDelta = new Vector2(
                HomeStyle.Friends.SearchIconSize, HomeStyle.Friends.SearchIconSize);
            // Tinted white so the file's own colour comes through, the way
            // every other icon on this screen is handled.
            var glyphImage = AddImage(glyph, Color.white, searchIcon);
            glyphImage.preserveAspect = true;
            glyphImage.enabled = searchIcon != null;

            var clear = CreateRect("Clear", row);
            SetAnchor(clear, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            clear.anchoredPosition = Vector2.zero;
            clear.sizeDelta = new Vector2(
                HomeStyle.Friends.SearchIconSize, HomeStyle.Friends.SearchIconSize);
            var clearImage = AddImage(clear, Color.white, clearIcon, raycastTarget: true);
            clearImage.preserveAspect = true;
            clearImage.enabled = clearIcon != null;
            var clearButton = clear.gameObject.AddComponent<Button>();
            clearButton.targetGraphic = clearImage;
            clearButton.transition = Selectable.Transition.None;
            clearButton.onClick.AddListener(ClearFriendSearch);
            menuButtons.Add(clearButton);

            var field = CreateRect("Field", row);
            SetAnchor(field, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            field.offsetMin = new Vector2(
                HomeStyle.Friends.SearchIconSize + HomeStyle.Friends.SearchIconGap, 0f);
            field.offsetMax = new Vector2(
                -(HomeStyle.Friends.SearchIconSize + HomeStyle.Friends.SearchIconGap), 0f);

            var viewport = CreateRect("TextArea", field);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();

            var textRect = CreateRect("Text", viewport);
            SetAnchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = AddText(
                textRect,
                string.Empty,
                HomeStyle.FontSize.FriendSearch,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                raycastTarget: true);
            text.color = HomeStyle.Palette.SearchText;

            var placeholderRect = CreateRect("Placeholder", viewport);
            SetAnchor(placeholderRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholder = AddText(
                placeholderRect,
                Copy(UiText.Home.SearchPlaceholder),
                HomeStyle.FontSize.FriendSearch,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            placeholder.color = HomeStyle.Palette.Placeholder;
            Remember(placeholder, UiText.Home.SearchPlaceholder);

            field.gameObject.SetActive(false);
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = koreanFont;
            input.pointSize = HomeStyle.FontSize.FriendSearch;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = HomeStyle.Palette.SearchText;
            input.characterLimit = NicknamePolicy.MaxLength;
            input.onValueChanged.AddListener(OnFriendSearchTyped);
            input.onSubmit.AddListener(OnFriendSearchTyped);
            field.gameObject.SetActive(true);
            friendSearchInput = input;
        }

        /// <summary>
        /// The refresh line: a glyph and the word, together at the right.
        /// </summary>
        /// <remarks>
        /// Sized by a layout group rather than by measuring the label here.
        /// A label that has not been through a layout pass reports its width
        /// unreliably, and placing the glyph from that number put it on top of
        /// the text. The group does the measuring at the point Unity has the
        /// answer, and the fitter shrinks the row to whatever it comes to.
        /// </remarks>
        private void CreateRefreshRow(RectTransform panel)
        {
            var row = CreateRect("RefreshRow", panel);
            SetAnchor(row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            row.anchoredPosition = new Vector2(
                -HomeStyle.Friends.SidePadding, -HomeStyle.Friends.RefreshTop);
            row.sizeDelta = new Vector2(0f, HomeStyle.Friends.RefreshIconSize);

            var hit = AddImage(row, Color.clear, raycastTarget: true);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = HomeStyle.Friends.RefreshGap;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var glyph = CreateRect("Glyph", row);
            var glyphElement = glyph.gameObject.AddComponent<LayoutElement>();
            glyphElement.preferredWidth = HomeStyle.Friends.RefreshIconSize;
            glyphElement.preferredHeight = HomeStyle.Friends.RefreshIconSize;
            glyphElement.minWidth = HomeStyle.Friends.RefreshIconSize;
            var glyphImage = AddImage(glyph, Color.white, refreshIcon);
            glyphImage.preserveAspect = true;
            glyphImage.enabled = refreshIcon != null;

            var labelRect = CreateRect("Label", row);
            var label = AddText(
                labelRect,
                Copy(UiText.Home.Refresh),
                HomeStyle.FontSize.RefreshLabel,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            label.color = HomeStyle.Palette.Refresh;
            Remember(label, UiText.Home.Refresh);

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => FriendListRefreshRequested?.Invoke());
            menuButtons.Add(button);
        }

        /// <summary>
        /// The two lists, one scroll view each, only one of them ever shown.
        /// </summary>
        private void CreateFriendBodies(RectTransform panel)
        {
            friendListBody = CreateScrollBody(panel, "ListBody", out var listContent);
            onlineSectionText = CreateSectionTitle(listContent, Copy(UiText.Home.Online), true, leading: false);
            Remember(onlineSectionText, UiText.Home.Online);
            onlineItemsRoot = CreateItemGroup(listContent, "OnlineItems");
            onlineEmptyText = CreateEmptyMessage(
                listContent, "OnlineEmptyMessage", FriendsEmptyMessage);
            offlineSectionText = CreateSectionTitle(listContent, Copy(UiText.Home.Offline), false);
            Remember(offlineSectionText, UiText.Home.Offline);
            offlineItemsRoot = CreateItemGroup(listContent, "OfflineItems");
            offlineEmptyText = CreateEmptyMessage(
                listContent, "OfflineEmptyMessage", FriendsEmptyMessage);
            listContentRoot = listContent;

            friendSearchBody = CreateScrollBody(panel, "RequestBody", out var requestContent);
            searchSectionText = CreateSectionTitle(requestContent, Copy(UiText.Home.SearchResults), true, leading: false);
            Remember(searchSectionText, UiText.Home.SearchResults);
            searchItemsRoot = CreateItemGroup(requestContent, "SearchItems");
            searchEmptyText = CreateEmptyMessage(
                requestContent, "SearchEmptyMessage", Copy(UiText.Home.SearchEmpty));
            requestSectionText = CreateSectionTitle(
                requestContent, string.Format(Copy(UiText.Home.IncomingRequests), 0), false);
            requestItemsRoot = CreateItemGroup(requestContent, "RequestItems");
            requestContentRoot = requestContent;
        }

        private GameObject CreateScrollBody(
            RectTransform panel, string name, out RectTransform content)
        {
            var viewport = CreateRect(name, panel);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = new Vector2(
                HomeStyle.Friends.SidePadding, HomeStyle.Friends.VerticalPadding);
            viewport.offsetMax = new Vector2(
                -HomeStyle.Friends.SidePadding, -HomeStyle.Friends.BodyTop);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport);
            SetAnchor(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = HomeStyle.Friends.RowGap;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = HomeStyle.Friends.RowHeight;
            scroll.verticalScrollbar = CreateScrollbar(viewport);
            // AutoHide rather than AutoHideAndExpandViewport: the expanding
            // mode resizes the viewport, and this bar lives inside it.
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return viewport.gameObject;
        }

        /// <summary>
        /// A handle with no track behind it, as the design draws it.
        /// </summary>
        /// <summary>
        /// A handle with no track behind it, as the design draws it.
        /// </summary>
        /// <remarks>
        /// Three strips, not one: the bar takes the pointer across a
        /// comfortable width, the handle takes the drag across that same width,
        /// and only the thin child inside the handle is painted. Making the
        /// painted strip the grabbable one would ask the player to hit three
        /// pixels.
        /// </remarks>
        private Scrollbar CreateScrollbar(RectTransform viewport)
        {
            var bar = CreateRect("Scrollbar", viewport);
            SetAnchor(bar, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            bar.offsetMin = new Vector2(-HomeStyle.Friends.ScrollbarHitWidth, 0f);
            bar.offsetMax = Vector2.zero;
            bar.sizeDelta = new Vector2(HomeStyle.Friends.ScrollbarHitWidth, 0f);

            // Invisible, but present: a scrollbar with no graphic of its own
            // takes no clicks on the track, so there is no way to jump a page.
            AddImage(bar, Color.clear, raycastTarget: true);

            var slidingArea = CreateRect("Sliding Area", bar);
            SetAnchor(slidingArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            slidingArea.offsetMin = Vector2.zero;
            slidingArea.offsetMax = Vector2.zero;

            // Anchors and size are left for the Scrollbar to drive; a handle
            // with the default rect keeps its 100x100 and paints a slab over
            // the list instead of a bar down its edge.
            var handle = CreateRect("Handle", slidingArea);
            SetAnchor(handle, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            handle.sizeDelta = Vector2.zero;
            var grab = AddImage(handle, Color.clear, raycastTarget: true);

            var visual = CreateRect("Visual", handle);
            SetAnchor(visual, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            visual.offsetMin = new Vector2(-HomeStyle.Friends.ScrollbarWidth * 0.5f, 0f);
            visual.offsetMax = new Vector2(HomeStyle.Friends.ScrollbarWidth * 0.5f, 0f);
            var visualImage = AddImage(
                visual,
                HomeStyle.Palette.ScrollHandle,
                HomeUiFonts.Rounded(HomeStyle.Friends.ScrollbarRadius));
            visualImage.type = Image.Type.Sliced;
            visualImage.pixelsPerUnitMultiplier = 1f;

            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = grab;
            scrollbar.transition = Selectable.Transition.None;
            return scrollbar;
        }

        private TMP_Text CreateSectionTitle(
            RectTransform parent, string label, bool primary, bool leading = true)
        {
            var line = HomeStyle.FontSize.Section * 1.4f;
            var height = leading ? line + HomeStyle.Friends.SectionGap : line;

            var rect = CreateRect("Section", parent);
            rect.sizeDelta = new Vector2(0f, height);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            var text = AddText(
                rect,
                label,
                HomeStyle.FontSize.Section,
                FontStyles.Normal,
                TextAlignmentOptions.BottomLeft);
            text.color = primary
                ? HomeStyle.Palette.SectionPrimary
                : HomeStyle.Palette.SectionSecondary;
            return text;
        }

        /// <summary>
        /// The line that stands in for a result. Centred, where a heading is
        /// ranged left, so it does not read as another section of the list.
        /// </summary>
        private TMP_Text CreateEmptyMessage(RectTransform parent, string name, string label)
        {
            var line = HomeStyle.FontSize.Section * 1.4f;
            var height = line + HomeStyle.Friends.SectionHeaderGap;

            var rect = CreateRect(name, parent);
            rect.sizeDelta = new Vector2(0f, height);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            var text = AddText(
                rect,
                label,
                HomeStyle.FontSize.Section,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            text.color = HomeStyle.Palette.SectionSecondary;
            return text;
        }

        private static RectTransform CreateItemGroup(RectTransform parent, string name)
        {
            var group = CreateRect(name, parent);
            var layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = HomeStyle.Friends.RowGap;
            layout.padding = new RectOffset(0, 0, (int)HomeStyle.Friends.SectionHeaderGap, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = group.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return group;
        }

        private FriendRow CreateFriendRow(RectTransform parent, bool withRequestActions)
        {
            var rect = CreateRect("Row", parent);
            rect.sizeDelta = new Vector2(0f, HomeStyle.Friends.RowHeight);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = HomeStyle.Friends.RowHeight;
            element.minHeight = HomeStyle.Friends.RowHeight;

            var hover = AddImage(
                rect,
                Color.clear,
                HomeUiFonts.Rounded(HomeStyle.Friends.RowRadius),
                raycastTarget: true);
            hover.type = Image.Type.Sliced;
            hover.pixelsPerUnitMultiplier = 1f;
            rect.gameObject.AddComponent<HomeHoverHighlight>()
                .Bind(hover, null, Color.clear, HomeStyle.Palette.RowHover);

            var avatar = CreateRect("Avatar", rect);
            SetAnchor(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            avatar.anchoredPosition = new Vector2(HomeStyle.Friends.AvatarLeft, 0f);
            avatar.sizeDelta = new Vector2(
                HomeStyle.Friends.AvatarDiameter, HomeStyle.Friends.AvatarDiameter);
            AddImage(avatar, AvatarColor, HomeUiFonts.CircleSprite);
            Game.Client.Character.AvatarFaceSlot.Attach(avatar);

            var trailingWidth = withRequestActions
                ? (HomeStyle.Friends.RowIconSize * 2f) + HomeStyle.Friends.RowIconGap
                : HomeStyle.Friends.RowIconSize;

            var nameRect = CreateRect("Name", rect);
            SetAnchor(nameRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            nameRect.offsetMin = new Vector2(
                HomeStyle.Friends.AvatarLeft
                    + HomeStyle.Friends.AvatarDiameter
                    + HomeStyle.Friends.AvatarToName,
                0f);
            nameRect.offsetMax = new Vector2(
                -(trailingWidth + (HomeStyle.Friends.RowIconGap * 2f)), 0f);
            var name = AddText(
                nameRect,
                string.Empty,
                HomeStyle.FontSize.FriendName,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            name.color = HomeStyle.Palette.FriendOnline;

            // A long name folds onto a second line rather than running under the
            // icons. Two lines of this size still fit the row's fixed height.
            name.textWrappingMode = TextWrappingModes.Normal;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.maxVisibleLines = 2;

            var row = new FriendRow { Rect = rect, Name = name };

            // Every row can be pressed; only the search results do anything
            // with it. Wiring it here rather than per list keeps the two
            // bindings from having to build different rows.
            row.Row = rect.gameObject.AddComponent<Button>();
            row.Row.targetGraphic = hover;
            row.Row.transition = Selectable.Transition.None;

            if (withRequestActions)
            {
                row.Accept = CreateRowIcon(rect, "Accept", acceptIcon, 1);
                row.Reject = CreateRowIcon(rect, "Reject", rejectIcon, 0);
                row.AcceptButton = row.Accept.gameObject.AddComponent<Button>();
                row.AcceptButton.targetGraphic = row.Accept;
                row.AcceptButton.transition = Selectable.Transition.None;
                row.RejectButton = row.Reject.gameObject.AddComponent<Button>();
                row.RejectButton.targetGraphic = row.Reject;
                row.RejectButton.transition = Selectable.Transition.None;
            }
            else
            {
                row.Trailing = CreateRowIcon(rect, "Trailing", steamIcon, 0);
            }

            return row;
        }

        private Image CreateRowIcon(RectTransform parent, string name, Sprite sprite, int slotFromRight)
        {
            var rect = CreateRect(name, parent);
            SetAnchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(
                -(HomeStyle.Friends.RowIconGap
                    + (slotFromRight
                        * (HomeStyle.Friends.RowIconSize + HomeStyle.Friends.RowIconGap))),
                0f);
            rect.sizeDelta = new Vector2(
                HomeStyle.Friends.RowIconSize, HomeStyle.Friends.RowIconSize);

            var image = AddImage(rect, Color.white, sprite, raycastTarget: true);
            image.preserveAspect = true;
            image.enabled = false;
            return image;
        }

        /// <summary>
        /// What is in the box, including the syllable still being composed.
        /// </summary>
        /// <remarks>
        /// A Korean keyboard hands over a syllable only once the next keystroke
        /// settles it. Until then TextMeshPro draws the part-built glyph but
        /// keeps it out of <c>text</c>, so filtering on <c>text</c> alone runs a
        /// keystroke behind what the player can see. The composition is read
        /// off <c>Input.compositionString</c>, the value the field itself
        /// draws, once a frame while the box has focus, and off the browser's
        /// own field on WebGL, and put back on the front.
        /// <para>
        /// Not the Input System's <c>onIMECompositionChange</c>: that fires
        /// only once IME has been switched on through the Input System, and
        /// TextMeshPro switches it on another way, so it stayed silent while
        /// the glyph was plainly on screen.
        /// </para>
        /// </remarks>
        private string TypedFriendSearch =>
            (friendSearchInput != null ? friendSearchInput.text : string.Empty) + composingText;

        private void OnFriendSearchTyped(string value)
        {
            // Whatever was being composed is now part of the text.
            composingText = string.Empty;
            FriendSearchRequested?.Invoke(TypedFriendSearch);
        }

        private void PollFriendSearchComposition()
        {
            if (friendSearchInput == null)
            {
                return;
            }

            // Once the box is left, what was being built is either in the
            // text or gone, so nothing is pending.
            SetFriendComposing(
                friendSearchInput.isFocused
                    ? Input.compositionString ?? string.Empty
                    : string.Empty);
        }

        private void OnBrowserFriendComposing(TMP_InputField field, string composing)
        {
            if (field == friendSearchInput)
            {
                SetFriendComposing(composing);
            }
        }

        private void SetFriendComposing(string next)
        {
            if (string.Equals(next, composingText, StringComparison.Ordinal))
            {
                return;
            }

            composingText = next;
            FriendSearchRequested?.Invoke(TypedFriendSearch);
        }

        /// <summary>
        /// Listens to the browser's field only while the panel is open. The
        /// keyboard side needs no listener: it is polled while focused.
        /// </summary>
        private void WatchComposition(bool watching)
        {
            WebTextInput.ComposingChanged -= OnBrowserFriendComposing;
            if (watching)
            {
                WebTextInput.ComposingChanged += OnBrowserFriendComposing;
            }
        }

        /// <summary>
        /// Empties the box, the half-typed syllable with it.
        /// </summary>
        /// <remarks>
        /// Clearing <c>text</c> alone leaves the composition on screen, because
        /// it was never in <c>text</c> to begin with. Dropping focus is what
        /// ends it.
        /// </remarks>
        private void ClearFriendSearch()
        {
            composingText = string.Empty;
            if (friendSearchInput == null)
            {
                return;
            }

            friendSearchInput.DeactivateInputField();
            friendSearchInput.text = string.Empty;
        }

        /// <summary>
        /// Grows or shrinks a group to hold exactly these friends.
        /// </summary>
        /// <remarks>
        /// Rows are reused rather than rebuilt. A refresh that destroyed and
        /// respawned every row would drop the hover under the pointer and take
        /// the scroll position with it.
        /// </remarks>
        private void BindFriendRows(
            RectTransform group, IReadOnlyList<FriendSummary> friends, bool online)
        {
            if (group == null)
            {
                return;
            }

            var rows = EnsureRows(friendRows, group, friends.Count, false);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var used = index < friends.Count;
                row.Rect.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                var friend = friends[index];
                row.Name.text = friend.Nickname;
                row.Name.color = online
                    ? HomeStyle.Palette.FriendOnline
                    : HomeStyle.Palette.FriendOffline;
                if (friend.AppearanceSet)
                {
                    Game.Core.Players.AvatarAppearanceBoard.Remember(
                        friend.PlayerId, friend.Appearance);
                }

                var avatar = row.Rect.Find("Avatar") as RectTransform;
                Game.Client.Character.AvatarFaceSlot.Attach(avatar)
                    ?.Follow(friend.PlayerId);
                row.Row.onClick.RemoveAllListeners();
                var contextClick = row.Rect.GetComponent<HomeFriendContextClick>()
                    ?? row.Rect.gameObject.AddComponent<HomeFriendContextClick>();
                var playerId = friend.PlayerId;
                contextClick.RightClicked = pointer => OpenFriendContextMenu(playerId, pointer);

                // The mark says this friend is signed in to Steam but not in
                // the game, which is the middle of the three states.
                if (row.Trailing != null)
                {
                    row.Trailing.sprite = steamIcon;
                    row.Trailing.enabled =
                        friend.Presence == FriendPresence.SteamOnline
                        && steamIcon != null;
                }
            }

            if (online)
            {
                shownOnlineCount = friends.Count;
            }
            else
            {
                shownOfflineCount = friends.Count;
            }

            UpdateFriendSections();
        }

        /// <summary>
        /// Puts a line under whichever section came back empty.
        /// </summary>
        /// <remarks>
        /// Both headings stay whatever happens, so the panel always reads as
        /// two lists rather than rearranging itself around what is missing. The
        /// reason for an empty one matters: with something typed the friends
        /// exist and are merely filtered out, and "친구가 없어요" would be wrong.
        /// </remarks>
        private void UpdateFriendSections()
        {
            var message = TypedFriendSearch.Length > 0
                ? Copy(UiText.Home.SearchEmpty)
                : FriendsEmptyMessage;

            ShowSectionEmpty(onlineEmptyText, shownOnlineCount == 0, message);
            ShowSectionEmpty(offlineEmptyText, shownOfflineCount == 0, message);
        }

        private static void ShowSectionEmpty(TMP_Text text, bool empty, string message)
        {
            if (text == null)
            {
                return;
            }

            text.gameObject.SetActive(empty);
            if (empty)
            {
                text.text = message;
            }
        }

        private void BindSearchRows(IReadOnlyList<FriendSearchHit> results)
        {
            if (searchItemsRoot == null)
            {
                return;
            }

            var rows = EnsureRows(friendRows, searchItemsRoot, results.Count, false);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var used = index < results.Count;
                row.Rect.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                var hit = results[index];
                row.Name.text = hit.Nickname
                    + (hit.IsIncoming ? "  (받은 요청 · 수락)" : string.Empty);
                Game.Client.Character.AvatarFaceSlot.Attach(row.Rect.Find("Avatar") as RectTransform)
                    ?.Follow(hit.PlayerId);

                // Pending requests cannot be sent again; their icon cancels them.
                row.Name.color = hit.IsPending
                    ? HomeStyle.Palette.FriendOffline
                    : HomeStyle.Palette.FriendOnline;
                row.Row.onClick.RemoveAllListeners();
                row.Row.interactable = !hit.IsPending;
                if (!hit.IsPending)
                {
                    var playerId = hit.PlayerId;
                    if (hit.IsIncoming)
                        row.Row.onClick.AddListener(() => FriendRequestAccepted?.Invoke(playerId));
                    else
                        row.Row.onClick.AddListener(() => FriendRequestClicked?.Invoke(playerId));
                }

                if (row.Trailing != null)
                {
                    row.Trailing.sprite = hit.IsPending ? rejectIcon : friendPlusIcon;
                    row.Trailing.rectTransform.sizeDelta = Vector2.one * HomeStyle.Friends.RowIconSize;
                    row.Trailing.enabled = row.Trailing.sprite != null;
                    var action = row.Trailing.GetComponent<Button>()
                        ?? row.Trailing.gameObject.AddComponent<Button>();
                    action.targetGraphic = row.Trailing;
                    action.transition = Selectable.Transition.None;
                    action.onClick.RemoveAllListeners();
                    var playerId = hit.PlayerId;
                    if (hit.IsPending)
                        action.onClick.AddListener(() => FriendRequestCancelled?.Invoke(playerId));
                    else if (hit.IsIncoming)
                        action.onClick.AddListener(() => FriendRequestAccepted?.Invoke(playerId));
                    else
                        action.onClick.AddListener(() => FriendRequestClicked?.Invoke(playerId));
                }
            }

            // No count beside the heading: a search matches the whole nickname
            // exactly, so the answer is only ever one friend or none.
            if (searchSectionText != null)
            {
                searchSectionText.text = Copy(UiText.Home.SearchResults);
            }
        }

        /// <summary>
        /// The requests waiting to be answered, each with its two buttons.
        /// </summary>
        public void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (requestItemsRoot == null)
            {
                return;
            }

            var rows = EnsureRows(requestRows, requestItemsRoot, requests.Count, true);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var used = index < requests.Count;
                row.Rect.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                var playerId = requests[index].PlayerId;
                row.Name.text = requests[index].Nickname;
                row.Name.color = HomeStyle.Palette.FriendOnline;
                Game.Client.Character.AvatarFaceSlot.Attach(row.Rect.Find("Avatar") as RectTransform)
                    ?.Follow(playerId);
                row.Accept.enabled = acceptIcon != null;
                row.Reject.enabled = rejectIcon != null;

                // Listeners are cleared first because the row is reused: without
                // this a third refresh would answer for three different players.
                row.AcceptButton.onClick.RemoveAllListeners();
                row.AcceptButton.onClick.AddListener(
                    () => FriendRequestAccepted?.Invoke(playerId));
                row.RejectButton.onClick.RemoveAllListeners();
                row.RejectButton.onClick.AddListener(
                    () => FriendRequestDeclined?.Invoke(playerId));
            }

            incomingRequestCount = requests.Count;
            if (requestSectionText != null)
            {
                requestSectionText.text = string.Format(
                    Copy(UiText.Home.IncomingRequests), incomingRequestCount);
            }

            SetIncomingRequestCount(requests.Count);
        }

        /// <summary>
        /// Hands back the rows of one group, making more if it is short.
        /// </summary>
        private List<FriendRow> EnsureRows(
            List<FriendRow> pool, RectTransform group, int wanted, bool withRequestActions)
        {
            var owned = new List<FriendRow>();
            for (var index = 0; index < pool.Count; index++)
            {
                if (pool[index].Rect != null && pool[index].Rect.parent == group)
                {
                    owned.Add(pool[index]);
                }
            }

            while (owned.Count < wanted)
            {
                var row = CreateFriendRow(group, withRequestActions);
                pool.Add(row);
                owned.Add(row);
            }

            return owned;
        }

        /// <summary>
        /// Shows or hides the whole search section, heading included.
        /// </summary>
        /// <remarks>
        /// Nothing typed means nothing was asked, so the section is not there
        /// at all: a standing "검색된 친구 (0)" over "해당 유저를 찾을 수
        /// 없습니다." reads as a failed search rather than as an empty box, and
        /// it pushes the requests below it down for no reason.
        /// </remarks>
        private void UpdateSearchEmptyHint(IReadOnlyList<FriendSearchHit> results)
        {
            var searched = friendSearchInput != null
                && !string.IsNullOrWhiteSpace(friendSearchInput.text);

            if (searchSectionText != null)
            {
                searchSectionText.gameObject.SetActive(searched);
            }

            if (searchItemsRoot != null)
            {
                searchItemsRoot.gameObject.SetActive(searched);
            }

            if (searchEmptyText == null)
            {
                return;
            }

            searchEmptyText.gameObject.SetActive(searched && results.Count == 0);
            searchEmptyText.text = Copy(UiText.Home.SearchEmpty);
        }

        private void ApplyTabColours()
        {
            if (friendListTab != null)
            {
                friendListTab.color = isRequestTabOpen
                    ? HomeStyle.Palette.TabIdle
                    : HomeStyle.Palette.TabSelected;
            }

            if (friendRequestTab != null)
            {
                friendRequestTab.color = isRequestTabOpen
                    ? HomeStyle.Palette.TabSelected
                    : HomeStyle.Palette.TabIdle;
            }

            if (listRule != null)
            {
                listRule.color = isRequestTabOpen
                    ? HomeStyle.Palette.TabIdle
                    : HomeStyle.Palette.TabSelected;
            }

            if (requestRule != null)
            {
                requestRule.color = isRequestTabOpen
                    ? HomeStyle.Palette.TabSelected
                    : HomeStyle.Palette.TabIdle;
            }
        }

        private static void ClearRowButtons(List<FriendRow> rows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index].Row != null)
                {
                    rows[index].Row.onClick.RemoveAllListeners();
                }

                if (rows[index].AcceptButton != null)
                {
                    rows[index].AcceptButton.onClick.RemoveAllListeners();
                }

                if (rows[index].RejectButton != null)
                {
                    rows[index].RejectButton.onClick.RemoveAllListeners();
                }
            }
        }

        /// <summary>
        /// The requests this player is waiting on an answer to.
        /// </summary>
        /// <remarks>
        /// The mock-up has no section for these, so they are accepted and not
        /// drawn. When a design arrives it goes here beside the incoming list.
        /// </remarks>
        public void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests)
        {
        }

        /// <summary>
        /// Says why the last friend action did not happen. An empty message
        /// clears it.
        /// </summary>
        public void SetFriendActionError(string message)
        {
            if (searchEmptyText == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                // The rows that are already up decide this, not an assumed
                // empty result: clearing a failure must not overwrite a hit.
                UpdateSearchEmptyHint(lastSearchResults);
                return;
            }

            searchEmptyText.gameObject.SetActive(true);
            searchEmptyText.text = message;
        }

        /// <summary>
        /// How many requests are waiting, which is all the badge shows.
        /// </summary>
        public void SetIncomingRequestCount(int count)
        {
            var label = count > 9 ? "9+" : count.ToString();
            requestBadge?.SetActive(count > 0);
            friendButtonBadge?.SetActive(count > 0);
            if (requestBadgeText != null) requestBadgeText.text = label;
            if (friendButtonBadgeText != null) friendButtonBadgeText.text = label;
        }
    }
}
