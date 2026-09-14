using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Lobby;
using Game.Core.Rooms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public sealed partial class PlaySettingsView
    {
        private bool layoutBuilt;
        private Text titleCounterText;
        private Text mapNameText;
        private Text categoryText;
        private Button mapPrevButton;
        private Button mapNextButton;
        private Button categoryPrevButton;
        private Button categoryNextButton;
        private Button applyButton;
        private Button revertButton;
        private Text revertLabel;
        private Image revertFill;
        private Image revertStroke;
        private bool resetHovered;
        private Image mapPreviewImage;
        private RectTransform settingsContent;
        private ScrollRect bodyScroll;

        private int selectedCategoryIndex = PlaySettingsCategoryCatalog.DefaultIndex;

        private void EnsureLayout()
        {
            if (panel == null)
            {
                return;
            }

            if (layoutBuilt && IsCurrentLayout(panel.transform))
            {
                var content = panel.transform.Find("Body/SettingsScroll/Viewport/Content") as RectTransform;
                if (content != null)
                {
                    CacheScrollRefs(content);
                    CacheRoomCodeRefs(content);
                    CacheMapAreaRefs(content);
                    CacheMapScrollRefs();
                    CacheDurationSliderRefs(content);
                    CacheApplyRefs();
                    CacheRevertRefs();
                }

                return;
            }

            layoutBuilt = true;
            BuildLayout((RectTransform)panel.transform);
            BindRuleControls();
            BindDurationSliders();
        }

        private static bool IsCurrentLayout(Transform root)
        {
            var stamp = root.Find("LayoutStamp");
            return stamp != null &&
                   stamp.TryGetComponent<PlaySettingsLayoutVersion>(out var marker) &&
                   marker.Version == PlaySettingsStyle.LayoutVersion;
        }

        private void CacheScrollRefs(RectTransform content)
        {
            settingsContent = content;
            var scrollTransform = panel.transform.Find("Body/SettingsScroll");
            bodyScroll = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
        }

        private void CacheRoomCodeRefs(RectTransform content)
        {
            var valueTransform = FindDeepChild(content, "RoomCodeValue");
            if (valueTransform != null)
            {
                var text = valueTransform.GetComponentInChildren<Text>();
                if (text != null)
                {
                    roomCodeText = text;
                }

                var hitButton = valueTransform.GetComponent<Button>();
                if (hitButton != null)
                {
                    roomCodeHitButton = hitButton;
                }
            }

            var copyTransform = FindDeepChild(content, "CopyRoomCodeButton");
            if (copyTransform != null)
            {
                var button = copyTransform.GetComponent<Button>();
                if (button != null)
                {
                    copyRoomCodeButton = button;
                }

                var iconTransform = copyTransform.Find("Icon");
                if (iconTransform != null)
                {
                    var icon = iconTransform.GetComponent<Image>();
                    if (icon != null)
                    {
                        copyIconImage = icon;
                    }
                }
            }

            var feedbackTransform = FindDeepChild(content, "CopiedFeedback");
            if (feedbackTransform != null)
            {
                copyFeedbackRoot = feedbackTransform.gameObject;
                var text = feedbackTransform.GetComponent<Text>()
                    ?? feedbackTransform.GetComponentInChildren<Text>();
                if (text != null)
                {
                    copyFeedbackText = text;
                }
            }

            var shiftTransform = FindDeepChild(content, "CopiedFeedbackShift");
            if (shiftTransform != null)
            {
                copyFeedbackShift = shiftTransform.gameObject;
            }
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void CacheMapAreaRefs(RectTransform content)
        {
            var mapArea = FindDeepChild(content, "MapArea");
            if (mapArea == null)
            {
                return;
            }

            var mapName = FindDeepChild(mapArea, "MapName");
            if (mapName != null)
            {
                var text = mapName.GetComponent<Text>() ?? mapName.GetComponentInChildren<Text>();
                if (text != null)
                {
                    mapNameText = text;
                }
            }

            var categoryValue = FindDeepChild(mapArea, "CategoryValue");
            if (categoryValue != null)
            {
                var text = categoryValue.GetComponent<Text>() ?? categoryValue.GetComponentInChildren<Text>();
                if (text != null)
                {
                    categoryText = text;
                }
            }

            var categoryPrev = FindDeepChild(mapArea, "CategoryPrev");
            if (categoryPrev != null)
            {
                var button = categoryPrev.GetComponent<Button>();
                if (button != null)
                {
                    categoryPrevButton = button;
                }
            }

            var categoryNext = FindDeepChild(mapArea, "CategoryNext");
            if (categoryNext != null)
            {
                var button = categoryNext.GetComponent<Button>();
                if (button != null)
                {
                    categoryNextButton = button;
                }
            }
        }

        private void RebuildSettingsScrollLayout()
        {
            if (settingsContent == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(settingsContent);
            if (bodyScroll != null)
            {
                bodyScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void BuildLayout(RectTransform root)
        {
            ruleValues.Clear();
            ruleMinus.Clear();
            rulePlus.Clear();
            mapSlotImages.Clear();
            mapSlotButtons.Clear();
            mapScroll = null;
            mapContent = null;

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            var panelImage = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            panelImage.sprite = HomeUiFonts.Rounded(PlaySettingsStyle.PanelRadius);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PlaySettingsStyle.Palette.PanelFill;
            panelImage.raycastTarget = true;

            var stroke = CreateRect("Stroke", root);
            Stretch(stroke);
            var strokeImage = stroke.gameObject.AddComponent<Image>();
            strokeImage.sprite = HomeUiFonts.Outline(PlaySettingsStyle.PanelRadius, PlaySettingsStyle.BorderWidth);
            strokeImage.type = Image.Type.Sliced;
            strokeImage.color = PlaySettingsStyle.Palette.Border;
            strokeImage.raycastTarget = false;

            root.sizeDelta = PlaySettingsStyle.ModalSize;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;

            var header = CreateRect("Header", root);
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            header.offsetMin = new Vector2(0f, -PlaySettingsStyle.HeaderHeight);
            header.offsetMax = Vector2.zero;
            CreateModalTitle(header, "게임 설정");

            var footer = CreateRect("Footer", root);
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            footer.offsetMin = Vector2.zero;
            footer.offsetMax = new Vector2(0f, PlaySettingsStyle.FooterHeight);
            applyWarning = CreateApplyWarning(footer);
            CreateFooterActions(footer);

            var body = CreateRect("Body", root);
            Anchor(body, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            body.offsetMin = new Vector2(0f, PlaySettingsStyle.FooterHeight);
            body.offsetMax = new Vector2(0f, -PlaySettingsStyle.HeaderHeight);
            BuildBody(body);

            header.SetAsLastSibling();
            footer.SetAsLastSibling();
            stroke.SetSiblingIndex(0);

            var stamp = CreateRect("LayoutStamp", root);
            stamp.gameObject.SetActive(false);
            stamp.gameObject.AddComponent<PlaySettingsLayoutVersion>().Version =
                PlaySettingsStyle.LayoutVersion;

            RefreshFooterSpace();
        }

        private void RefreshFooterSpace()
        {
            if (panel == null)
            {
                return;
            }

            var footer = panel.transform.Find("Footer") as RectTransform;
            var body = panel.transform.Find("Body") as RectTransform;
            var bottom = editable ? PlaySettingsStyle.FooterHeight : 0f;
            if (footer != null)
            {
                footer.gameObject.SetActive(editable);
            }

            if (body == null || Mathf.Approximately(body.offsetMin.y, bottom))
            {
                return;
            }

            body.offsetMin = new Vector2(0f, bottom);
            if (panel.activeInHierarchy)
            {
                RebuildSettingsScrollLayout();
            }
        }

        private void BuildBody(RectTransform body)
        {
            var scrollArea = CreateRect("SettingsScroll", body);
            Stretch(scrollArea);
            BuildSettingsScroll(scrollArea);
        }

        private void BuildSettingsScroll(RectTransform scrollArea)
        {
            bodyScroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            bodyScroll.horizontal = false;
            bodyScroll.vertical = true;
            bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            bodyScroll.scrollSensitivity = 30f;

            var viewport = CreateRect("Viewport", scrollArea);
            Stretch(viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            bodyScroll.viewport = viewport;
            bodyScroll.verticalScrollbar = CreateBodyScrollbar(scrollArea);
            bodyScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            settingsContent = CreateRect("Content", viewport);
            settingsContent.anchorMin = new Vector2(0f, 1f);
            settingsContent.anchorMax = new Vector2(1f, 1f);
            settingsContent.pivot = new Vector2(0.5f, 1f);
            settingsContent.anchoredPosition = Vector2.zero;
            settingsContent.sizeDelta = new Vector2(0f, 0f);
            bodyScroll.content = settingsContent;

            var layout = settingsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = PlaySettingsStyle.RowSpacing;
            layout.padding = new RectOffset(
                (int)PlaySettingsStyle.SidePadding,
                (int)PlaySettingsStyle.SidePadding,
                (int)PlaySettingsStyle.SidePadding,
                (int)PlaySettingsStyle.SidePadding);

            var fitter = settingsContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var mapArea = CreateLayoutRow(settingsContent, PlaySettingsStyle.MapSectionHeight);
            mapArea.name = "MapArea";
            BuildMapSection(mapArea);
            AddLayoutDivider(settingsContent);
            AddSectionTitle(settingsContent, "방 설정");
            BuildTitleRow(settingsContent);
            BuildRoomCodeRow(settingsContent);
            BuildCounterRow(settingsContent, "인원 설정", out maxPlayersMinusButton, out maxPlayersText,
                out maxPlayersPlusButton);
            BuildCounterRow(settingsContent, "파괴 기능 횟수", out destructionMinusButton, out destructionLimitText,
                out destructionPlusButton);
            BuildDurationSliderRow(
                settingsContent,
                "숨기는 시간",
                "HidingDuration",
                MatchRuleSettings.MinHidingDurationSeconds,
                MatchRuleSettings.MaxHidingDurationSeconds,
                MatchRuleSettings.DefaultHidingDurationSeconds,
                out hidingSlider,
                out hidingValue);
            BuildDurationSliderRow(
                settingsContent,
                "찾는 시간",
                "SearchingDuration",
                MatchRuleSettings.MinSearchingDurationSeconds,
                MatchRuleSettings.MaxSearchingDurationSeconds,
                MatchRuleSettings.DefaultSearchingDurationSeconds,
                out searchingSlider,
                out searchingValue);
            BuildRuleRow(settingsContent, "달리는 속도", 2);
            BuildRuleRow(settingsContent, "기절 펀치 횟수", 3);
        }

        /// <summary>
        /// The handle down the body's right padding, so the wheel already
        /// had somewhere to go and now the thumb does too.
        /// </summary>
        private Scrollbar CreateBodyScrollbar(RectTransform scrollArea)
        {
            var track = CreateRect("Scrollbar", scrollArea);
            Anchor(track, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            track.anchoredPosition = new Vector2(
                -PlaySettingsStyle.Layout.ScrollbarRightInset, 0f);
            track.sizeDelta = new Vector2(
                PlaySettingsStyle.Layout.ScrollbarWidth,
                -PlaySettingsStyle.Layout.ScrollbarVerticalInset * 2f);

            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = Color.clear;
            trackImage.raycastTarget = true;

            var area = CreateRect("SlidingArea", track);
            Stretch(area);

            var handle = CreateRect("Handle", area);
            Stretch(handle);
            handle.sizeDelta = Vector2.zero;

            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HomeUiFonts.Rounded(PlaySettingsStyle.Layout.ScrollbarRadius);
            handleImage.type = Image.Type.Sliced;
            handleImage.color = PlaySettingsStyle.Palette.ScrollbarHandle;
            handleImage.raycastTarget = true;

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.transition = Selectable.Transition.None;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            track.SetAsLastSibling();
            return bar;
        }

        private void AddLayoutDivider(RectTransform parent)
        {
            var divider = CreateLayoutRow(parent, 1f);
            divider.name = "SectionDivider";
            divider.gameObject.AddComponent<Image>().color = PlaySettingsStyle.Palette.Divider;
        }

        private RectTransform CreateLayoutRow(RectTransform parent, float height)
        {
            var row = CreateRect("SettingRow", parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, height);
            return row;
        }

        private void AddSectionTitle(RectTransform parent, string title)
        {
            var row = CreateLayoutRow(parent, PlaySettingsStyle.Layout.SectionTitleHeight);
            row.name = "SectionTitleRow";

            var label = row.gameObject.AddComponent<Text>();
            label.text = title;
            label.font = ExtraBoldFont();
            label.fontSize = PlaySettingsStyle.FontSize.SectionTitle;
            label.color = PlaySettingsStyle.Palette.Text;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            ApplySingleLine(label);
            Stretch(label.rectTransform);
        }

        private void BuildTitleRow(RectTransform parent)
        {
            var row = CreateLayoutRow(parent, PlaySettingsStyle.RowHeight);
            row.name = "TitleRow";
            CreateBodyText(row, "방 제목", new Vector2(0f, 0f),
                new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 1f), Vector2.zero, Vector2.zero);

            var field = CreateRect("TitleField", row);
            field.anchorMin = new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 0f);
            field.anchorMax = new Vector2(1f, 1f);
            field.offsetMin = Vector2.zero;
            field.offsetMax = Vector2.zero;

            var underline = CreateRect("Underline", field);
            Anchor(underline, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            underline.sizeDelta = new Vector2(-PlaySettingsStyle.Layout.TitleInputRightPadding, 1f);
            underline.anchoredPosition = new Vector2(0f, 6f);
            underline.gameObject.AddComponent<Image>().color = PlaySettingsStyle.Palette.Underline;

            var inputRect = CreateRect("TitleInput", field);
            Anchor(inputRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            inputRect.offsetMin = new Vector2(0f, 12f);
            inputRect.offsetMax = new Vector2(-PlaySettingsStyle.Layout.TitleInputRightPadding, 0f);
            inputRect.gameObject.AddComponent<RectMask2D>();
            var inputBg = inputRect.gameObject.AddComponent<Image>();
            inputBg.color = Color.clear;
            titleText = CreateBodyText(inputRect, string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            titleText.alignment = TextAnchor.MiddleLeft;
            titleInput = inputRect.gameObject.AddComponent<InputField>();
            titleInput.textComponent = titleText;
            titleInput.targetGraphic = inputBg;
            titleInput.characterLimit = RoomSettings.MaxTitleLength;

            titleCounterText = CreateBodyText(field, "0/20", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-PlaySettingsStyle.Layout.TitleCounterRightInset, -14f),
                new Vector2(
                    PlaySettingsStyle.Layout.TitleCounterWidth,
                    PlaySettingsStyle.Layout.TitleCounterHeight));
            titleCounterText.gameObject.name = "TitleCounter";
            titleCounterText.alignment = TextAnchor.MiddleRight;
            titleCounterText.fontSize = PlaySettingsStyle.FontSize.Counter;
        }

        private void BuildRoomCodeRow(RectTransform parent)
        {
            var row = CreateLayoutRow(parent, PlaySettingsStyle.RowHeight);
            row.name = "RoomCodeRow";

            var roomCodeLabel = CreateBodyText(row, "방 코드", new Vector2(0f, 0f),
                new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 1f), Vector2.zero, Vector2.zero);
            ApplySingleLine(roomCodeLabel);

            var group = CreateRect("RoomCodeControls", row);
            group.anchorMin = new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 0f);
            group.anchorMax = Vector2.one;
            group.offsetMin = new Vector2(12f, 0f);
            group.offsetMax = new Vector2(-4f, 0f);

            var layout = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = PlaySettingsStyle.Layout.RoomCodeControlSpacing;

            var spacer = CreateRect("Spacer", group);
            var spacerElement = spacer.gameObject.AddComponent<LayoutElement>();
            spacerElement.flexibleWidth = 1f;
            spacerElement.minWidth = 0f;

            var feedbackRect = CreateRect("CopiedFeedback", group);
            var feedbackElement = feedbackRect.gameObject.AddComponent<LayoutElement>();
            feedbackElement.preferredWidth = PlaySettingsStyle.Layout.CopiedFeedbackWidth;
            feedbackElement.preferredHeight = PlaySettingsStyle.RowHeight;
            feedbackElement.minWidth = PlaySettingsStyle.Layout.CopiedFeedbackWidth;
            copyFeedbackText = CreateBodyText(
                feedbackRect,
                "복사되었습니다!",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            copyFeedbackText.alignment = TextAnchor.MiddleRight;
            copyFeedbackText.color = PlaySettingsStyle.Palette.ApplyFill;
            ApplySingleLine(copyFeedbackText);
            copyFeedbackRoot = feedbackRect.gameObject;
            feedbackRect.gameObject.SetActive(false);

            var feedbackShift = CreateRect("CopiedFeedbackShift", group);
            var shiftElement = feedbackShift.gameObject.AddComponent<LayoutElement>();
            shiftElement.preferredWidth = PlaySettingsStyle.Layout.CopiedFeedbackShift;
            shiftElement.minWidth = PlaySettingsStyle.Layout.CopiedFeedbackShift;
            shiftElement.preferredHeight = 1f;
            feedbackShift.gameObject.SetActive(false);
            copyFeedbackShift = feedbackShift.gameObject;

            var valueRect = CreateRect("RoomCodeValue", group);
            var valueElement = valueRect.gameObject.AddComponent<LayoutElement>();
            valueElement.preferredWidth = PlaySettingsStyle.Layout.RoomCodeValueWidth;
            valueElement.preferredHeight = PlaySettingsStyle.RowHeight;

            roomCodeText = CreateBodyText(valueRect, string.Empty, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            roomCodeText.alignment = TextAnchor.MiddleRight;
            roomCodeText.raycastTarget = false;
            ApplySingleLine(roomCodeText);

            var valueHit = valueRect.gameObject.AddComponent<Image>();
            valueHit.color = Color.clear;
            roomCodeHitButton = valueRect.gameObject.AddComponent<Button>();
            roomCodeHitButton.targetGraphic = valueHit;

            var copySize = PlaySettingsStyle.Layout.CopyIconSize;
            var copyRect = CreateRect("CopyRoomCodeButton", group);
            var copyElement = copyRect.gameObject.AddComponent<LayoutElement>();
            copyElement.preferredWidth = copySize;
            copyElement.preferredHeight = copySize;
            copyElement.minWidth = copySize;
            copyElement.minHeight = copySize;

            var copyHit = copyRect.gameObject.AddComponent<Image>();
            copyHit.color = Color.clear;
            copyRoomCodeButton = copyRect.gameObject.AddComponent<Button>();
            copyRoomCodeButton.targetGraphic = copyHit;

            var iconRect = CreateRect("Icon", copyRect);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(copySize, copySize);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = LoadCopyIcon();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            copyIconImage = icon;
        }

        private void BuildCounterRow(
            RectTransform parent,
            string label,
            out Button minus,
            out Text value,
            out Button plus)
        {
            var row = CreateLayoutRow(parent, PlaySettingsStyle.RowHeight);
            CreateBodyText(row, label, new Vector2(0f, 0f),
                new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 1f), Vector2.zero, Vector2.zero);
            BuildControlGroup(row, out minus, out value, out plus);
        }

        private void BuildRuleRow(RectTransform parent, string label, int ruleIndex)
        {
            var row = CreateLayoutRow(parent, PlaySettingsStyle.RowHeight);
            CreateBodyText(row, label, new Vector2(0f, 0f),
                new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 1f), Vector2.zero, Vector2.zero);
            BuildControlGroup(row, out var minus, out var value, out var plus);
            ruleMinus.Add(minus);
            rulePlus.Add(plus);
            ruleValues.Add(value);
        }

        private void CacheDurationSliderRefs(RectTransform content)
        {
            CacheDurationSlider(content, "HidingDuration", out hidingSlider, out hidingValue);
            CacheDurationSlider(content, "SearchingDuration", out searchingSlider, out searchingValue);
        }

        private static void CacheDurationSlider(
            RectTransform content,
            string name,
            out Slider slider,
            out Text value)
        {
            slider = null;
            value = null;
            var row = FindDeepChild(content, name + "Row");
            if (row == null)
            {
                return;
            }

            var sliderTransform = row.Find("Slider");
            if (sliderTransform != null)
            {
                slider = sliderTransform.GetComponent<Slider>();
            }

            var valueTransform = row.Find("Value");
            if (valueTransform != null)
            {
                value = valueTransform.GetComponent<Text>();
            }
        }

        private void BuildDurationSliderRow(
            RectTransform parent,
            string label,
            string name,
            int min,
            int max,
            int defaultValue,
            out Slider slider,
            out Text value)
        {
            var row = CreateLayoutRow(parent, PlaySettingsStyle.RowHeight);
            row.name = name + "Row";
            CreateBodyText(row, label, new Vector2(0f, 0f),
                new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 1f), Vector2.zero, Vector2.zero);

            var valueRect = CreateRect("Value", row);
            Anchor(valueRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            valueRect.anchoredPosition = Vector2.zero;
            valueRect.sizeDelta = new Vector2(
                PlaySettingsStyle.Layout.DurationValueWidth, PlaySettingsStyle.RowHeight);
            value = valueRect.gameObject.AddComponent<Text>();
            value.font = BodyFont();
            value.fontSize = PlaySettingsStyle.FontSize.Body;
            value.color = PlaySettingsStyle.Palette.Text;
            value.alignment = TextAnchor.MiddleRight;
            value.raycastTarget = false;
            ApplySingleLine(value);

            var root = CreateRect("Slider", row);
            Anchor(root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            root.anchoredPosition = new Vector2(
                -(PlaySettingsStyle.Layout.DurationValueWidth + SettingsStyle.Slider.PercentGap),
                0f);
            root.sizeDelta = new Vector2(SettingsStyle.Slider.TrackSize.x, SettingsStyle.Slider.HitHeight);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            var track = CreateRect("Track", root);
            Anchor(track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(0f, SettingsStyle.Slider.TrackSize.y);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = HomeUiFonts.Rounded(PlaySettingsStyle.Layout.SliderTrackRadius);
            trackImage.type = Image.Type.Sliced;
            trackImage.pixelsPerUnitMultiplier = 1f;
            trackImage.color = SettingsStyle.Palette.SliderTrack;
            trackImage.raycastTarget = false;

            var fillArea = CreateRect("FillArea", root);
            Anchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta = new Vector2(0f, SettingsStyle.Slider.TrackSize.y);

            var fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HomeUiFonts.Rounded(PlaySettingsStyle.Layout.SliderTrackRadius);
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = 1f;
            fillImage.color = SettingsStyle.Palette.SliderFill;
            fillImage.raycastTarget = false;

            var handleArea = CreateRect("HandleArea", root);
            Anchor(handleArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            handleArea.anchoredPosition = Vector2.zero;
            handleArea.sizeDelta = new Vector2(
                -SettingsStyle.Slider.HandleDiameter, SettingsStyle.Slider.HandleDiameter);
            AddDefaultMark(handleArea, min, max, defaultValue);

            var handle = CreateRect("Handle", handleArea);
            Anchor(handle, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(SettingsStyle.Slider.HandleDiameter, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HomeUiFonts.CircleSprite;
            handleImage.type = Image.Type.Simple;
            handleImage.color = SettingsStyle.Palette.SliderHandle;
            handleImage.raycastTarget = true;

            slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private static void AddDefaultMark(RectTransform handleArea, int min, int max, int defaultValue)
        {
            if (max <= min)
            {
                return;
            }

            var t = Mathf.InverseLerp(min, max, defaultValue);
            var mark = CreateRect("DefaultMark", handleArea);
            Anchor(mark, new Vector2(t, 0.5f), new Vector2(t, 0.5f), new Vector2(0.5f, 0.5f));
            mark.anchoredPosition = Vector2.zero;
            mark.sizeDelta = new Vector2(
                PlaySettingsStyle.Layout.DefaultMarkWidth,
                PlaySettingsStyle.Layout.DefaultMarkHeight);
            var image = mark.gameObject.AddComponent<Image>();
            image.color = PlaySettingsStyle.Palette.DefaultMark;
            image.raycastTarget = false;
            mark.SetAsFirstSibling();
        }

        private void BuildControlGroup(
            RectTransform row,
            out Button minus,
            out Text value,
            out Button plus)
        {
            var group = CreateRect("Controls", row);
            group.anchorMin = new Vector2(PlaySettingsStyle.Layout.LabelAreaRatio, 0f);
            group.anchorMax = Vector2.one;
            group.offsetMin = new Vector2(12f, 0f);
            group.offsetMax = new Vector2(-4f, 0f);

            var layout = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = PlaySettingsStyle.Layout.ControlSpacing;

            minus = CreateLayoutArrowButton(group, isLeft: true);
            value = CreateLayoutValueText(group);
            plus = CreateLayoutArrowButton(group, isLeft: false);
        }

        private Button CreateLayoutArrowButton(RectTransform parent, bool isLeft)
        {
            var size = PlaySettingsStyle.Layout.ArrowSize;
            var rect = CreateRect(isLeft ? "Prev" : "Next", parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size;
            element.preferredHeight = size;
            element.minWidth = size;
            element.minHeight = size;

            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;

            var iconRect = CreateRect("Icon", rect);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(size, size);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = isLeft ? LoadArrowLeftIcon() : LoadArrowRightIcon();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return button;
        }

        private Text CreateLayoutValueText(RectTransform parent)
        {
            var rect = CreateRect("Value", parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = PlaySettingsStyle.Layout.ControlValueWidth;
            element.minWidth = PlaySettingsStyle.Layout.ControlValueWidth;

            var label = rect.gameObject.AddComponent<Text>();
            label.text = "0";
            label.font = BodyFont();
            label.fontSize = PlaySettingsStyle.FontSize.Body;
            label.color = PlaySettingsStyle.Palette.Text;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            ApplySingleLine(label);
            return label;
        }

        private void BuildMapSection(RectTransform parent)
        {
            ConfigureVerticalGroup(parent, TextAnchor.UpperCenter, PlaySettingsStyle.Layout.MapColumnSpacing);
            var mapLayout = parent.GetComponent<VerticalLayoutGroup>();
            mapLayout.padding = new RectOffset(
                0,
                0,
                0,
                (int)PlaySettingsStyle.Layout.MapSectionBottomSpacing);

            var titleRow = CreateSplitRow(parent, "SectionTitles", PlaySettingsStyle.Layout.SectionTitleHeight);
            CreateCenteredLabel(
                CreateSplitCell(titleRow, "MapTitle"),
                "맵 선택",
                PlaySettingsStyle.FontSize.Body);
            CreateCenteredLabel(
                CreateSplitCell(titleRow, "CategoryTitle"),
                "카테고리 선택",
                PlaySettingsStyle.FontSize.Body);

            var selectionHeight = PlaySettingsStyle.Layout.SelectionRowHeight;
            var pickerRow = CreateSplitRow(parent, "Pickers", selectionHeight);

            var mapPicker = CreateHorizontalPickerRow(CreateSplitCell(pickerRow, "MapSelect"), selectionHeight);
            Stretch(mapPicker);
            AddFlexibleSpacer(mapPicker);
            mapPrevButton = CreateLayoutArrowButton(mapPicker, isLeft: true);
            AddFlexibleSpacer(mapPicker);
            CreateMapStack(mapPicker);
            AddFlexibleSpacer(mapPicker);
            mapNextButton = CreateLayoutArrowButton(mapPicker, isLeft: false);
            AddFlexibleSpacer(mapPicker);

            var categoryPicker = CreateHorizontalPickerRow(
                CreateSplitCell(pickerRow, "CategorySelect"),
                selectionHeight);
            Stretch(categoryPicker);
            AddFlexibleSpacer(categoryPicker);
            categoryPrevButton = CreateLayoutArrowButton(categoryPicker, isLeft: true);
            categoryPrevButton.gameObject.name = "CategoryPrev";
            AddFlexibleSpacer(categoryPicker);
            categoryText = CreatePickerValueText(
                categoryPicker,
                PlaySettingsCategoryCatalog.Default.Label,
                selectionHeight);
            AddFlexibleSpacer(categoryPicker);
            categoryNextButton = CreateLayoutArrowButton(categoryPicker, isLeft: false);
            categoryNextButton.gameObject.name = "CategoryNext";
            AddFlexibleSpacer(categoryPicker);
        }

        private static void AddFlexibleSpacer(RectTransform parent)
        {
            var spacer = CreateRect("Spacer", parent);
            var element = spacer.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minWidth = 0f;
        }

        private RectTransform CreateSplitRow(RectTransform parent, string name, float height)
        {
            var row = CreateLayoutRow(parent, height);
            row.name = name;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 0f;
            return row;
        }

        private static RectTransform CreateSplitCell(RectTransform parent, string name)
        {
            var rect = CreateRect(name, parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            return rect;
        }

        private void CreateMapStack(RectTransform parent)
        {
            var previewSize = PlaySettingsStyle.Layout.MapPreviewSize;
            var nameHeight = PlaySettingsStyle.Layout.MapNameHeight;
            var spacing = PlaySettingsStyle.Layout.MapNameSpacing;
            var stackHeight = previewSize.y + spacing + nameHeight;

            var stack = CreateRect("MapStack", parent);
            stack.sizeDelta = new Vector2(previewSize.x, stackHeight);
            var element = stack.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = previewSize.x;
            element.minWidth = previewSize.x;
            element.preferredHeight = stackHeight;
            element.minHeight = stackHeight;

            var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = spacing;

            mapPreviewImage = CreateMapPreviewImage(stack);
            mapNameText = CreateMapNameText(stack, previewSize.x, nameHeight);
        }

        private static Text CreateMapNameText(RectTransform parent, float width, float height)
        {
            var row = CreateRect("MapName", parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;

            var label = row.gameObject.AddComponent<Text>();
            label.text = string.Empty;
            label.font = BodyFont();
            label.fontSize = PlaySettingsStyle.FontSize.MapName;
            label.color = PlaySettingsStyle.Palette.Text;
            label.alignment = TextAnchor.UpperCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static Text CreateCenteredLabel(RectTransform parent, string text, int fontSize)
        {
            var label = parent.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = BodyFont();
            label.fontSize = fontSize;
            label.color = PlaySettingsStyle.Palette.Text;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            ApplySingleLine(label);
            return label;
        }

        private static void ConfigureVerticalGroup(RectTransform rect, TextAnchor alignment, float spacing)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = spacing;
        }

        private static RectTransform CreateHorizontalPickerRow(RectTransform parent, float height)
        {
            var row = CreateRect("PickerRow", parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = PlaySettingsStyle.Layout.PickerSpacing;
            return row;
        }

        private static Text CreatePickerValueText(RectTransform parent, string text, float rowHeight)
        {
            var row = CreateRect("CategoryValue", parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = rowHeight;
            element.minHeight = rowHeight;
            element.minWidth = PlaySettingsStyle.Layout.CategoryValueMinWidth;
            element.preferredWidth = PlaySettingsStyle.Layout.CategoryValueMinWidth;

            var labelRect = CreateRect("Text", row);
            Stretch(labelRect);
            var label = labelRect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = BodyFont();
            label.fontSize = PlaySettingsStyle.FontSize.Body;
            label.color = PlaySettingsStyle.Palette.Text;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            ApplySingleLine(label);
            return label;
        }

        private Image CreateMapPreviewImage(RectTransform parent)
        {
            var preview = CreateRect("MapPreview", parent);
            var element = preview.gameObject.AddComponent<LayoutElement>();
            var size = PlaySettingsStyle.Layout.MapPreviewSize;
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            element.minWidth = size.x;
            element.minHeight = size.y;
            preview.sizeDelta = size;
            var image = preview.gameObject.AddComponent<Image>();
            image.sprite = HomeUiFonts.Rounded(16);
            image.type = Image.Type.Sliced;
            image.color = PlaySettingsStyle.Palette.MapPreview;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private void CacheApplyRefs()
        {
            if (panel == null)
            {
                return;
            }

            var footer = panel.transform.Find("Footer");
            if (footer == null)
            {
                return;
            }

            var buttonTransform = footer.Find("ActionRow/ApplyButton") ?? footer.Find("ApplyButton");
            if (buttonTransform != null)
            {
                applyButton = buttonTransform.GetComponent<Button>();
                applyFill = buttonTransform.GetComponent<Image>();
                var labelTransform = buttonTransform.Find("Text");
                if (labelTransform != null)
                {
                    applyLabel = labelTransform.GetComponent<Text>();
                    if (applyLabel != null)
                    {
                        applyLabel.font = MediumFont();
                    }
                }
            }

            var warningTransform = footer.Find("ApplyWarning");
            if (warningTransform != null)
            {
                applyWarning = warningTransform.GetComponent<Text>();
            }

            RefreshApplyChrome();
        }

        private void CacheRevertRefs()
        {
            if (panel == null)
            {
                return;
            }

            var footer = panel.transform.Find("Footer");
            if (footer == null)
            {
                return;
            }

            var buttonTransform = footer.Find("ActionRow/ResetButton") ?? footer.Find("ResetButton");
            if (buttonTransform == null)
            {
                return;
            }

            revertButton = buttonTransform.GetComponent<Button>();
            revertFill = buttonTransform.GetComponent<Image>();
            var strokeTransform = buttonTransform.Find("Stroke");
            if (strokeTransform != null)
            {
                revertStroke = strokeTransform.GetComponent<Image>();
            }

            var labelTransform = buttonTransform.Find("Text");
            if (labelTransform != null)
            {
                revertLabel = labelTransform.GetComponent<Text>();
            }

            RefreshRevertChrome();
        }

        private void CreateFooterActions(RectTransform footer)
        {
            var row = CreateRect("ActionRow", footer);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = Vector2.zero;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = PlaySettingsStyle.Layout.ActionSpacing;

            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            revertButton = CreateResetButton(row);
            applyButton = CreateApplyButton(row);
        }

        private Button CreateResetButton(RectTransform parent)
        {
            var paddingX = PlaySettingsStyle.ApplyPaddingHorizontal;
            var paddingY = PlaySettingsStyle.ApplyPaddingVertical;
            var fontSize = PlaySettingsStyle.FontSize.Apply;

            var rect = CreateRect("ResetButton", parent);
            revertFill = rect.gameObject.AddComponent<Image>();
            revertFill.type = Image.Type.Sliced;
            revertFill.color = PlaySettingsStyle.Palette.ResetFill;

            var stroke = CreateRect("Stroke", rect);
            Stretch(stroke);
            var strokeImage = stroke.gameObject.AddComponent<Image>();
            strokeImage.type = Image.Type.Sliced;
            strokeImage.color = PlaySettingsStyle.Palette.ResetOffStroke;
            strokeImage.raycastTarget = false;
            revertStroke = strokeImage;

            var labelRect = CreateRect("Text", rect);
            Stretch(labelRect);
            revertLabel = labelRect.gameObject.AddComponent<Text>();
            revertLabel.text = PlaySettingsStyle.Layout.ResetLabel;
            revertLabel.font = MediumFont();
            revertLabel.fontSize = fontSize;
            revertLabel.color = PlaySettingsStyle.Palette.ResetOffLabel;
            revertLabel.alignment = TextAnchor.MiddleCenter;
            revertLabel.raycastTarget = false;
            revertLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            revertLabel.verticalOverflow = VerticalWrapMode.Overflow;

            var textWidth = Mathf.Max(revertLabel.preferredWidth, 128f);
            var textHeight = Mathf.Max(revertLabel.preferredHeight, fontSize);
            var height = textHeight + (paddingY * 2f);
            rect.sizeDelta = new Vector2(textWidth + (paddingX * 2f), height);

            var radius = Mathf.Min(
                PlaySettingsStyle.ApplyButtonRadius,
                Mathf.Max(8, Mathf.FloorToInt((height * 0.5f) - 1f)));
            revertFill.sprite = HomeUiFonts.Rounded(radius);
            strokeImage.sprite = HomeUiFonts.Outline(radius, PlaySettingsStyle.BorderWidth);

            var slot = rect.gameObject.AddComponent<LayoutElement>();
            slot.preferredWidth = rect.sizeDelta.x;
            slot.preferredHeight = rect.sizeDelta.y;
            slot.minWidth = rect.sizeDelta.x;
            slot.minHeight = rect.sizeDelta.y;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = revertFill;
            button.transition = Selectable.Transition.None;
            button.interactable = false;
            BindResetHover(button);
            return button;
        }

        private static Text CreateApplyWarning(RectTransform footer)
        {
            var rect = CreateRect("ApplyWarning", footer);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 52f);
            rect.sizeDelta = new Vector2(PlaySettingsStyle.ModalSize.x - 80f, 28f);

            var label = rect.gameObject.AddComponent<Text>();
            label.text = "적용되지 않은 변경사항이 있습니다!";
            label.font = BodyFont();
            label.fontSize = PlaySettingsStyle.FontSize.ApplyWarning;
            label.color = PlaySettingsStyle.Palette.ApplyWarning;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.gameObject.SetActive(false);
            return label;
        }

        private Button CreateApplyButton(RectTransform footer)
        {
            var paddingX = PlaySettingsStyle.ApplyPaddingHorizontal;
            var paddingY = PlaySettingsStyle.ApplyPaddingVertical;
            var fontSize = PlaySettingsStyle.FontSize.Apply;

            var rect = CreateRect("ApplyButton", footer);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            applyFill = rect.gameObject.AddComponent<Image>();
            applyFill.type = Image.Type.Sliced;
            applyFill.color = PlaySettingsStyle.Palette.ApplyOffFill;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = applyFill;
            button.interactable = false;
            button.transition = Selectable.Transition.None;

            var labelRect = CreateRect("Text", rect);
            Stretch(labelRect);

            applyLabel = labelRect.gameObject.AddComponent<Text>();
            applyLabel.text = "적용하기";
            applyLabel.font = MediumFont();
            applyLabel.fontSize = fontSize;
            applyLabel.color = PlaySettingsStyle.Palette.ApplyOffLabel;
            applyLabel.alignment = TextAnchor.MiddleCenter;
            applyLabel.raycastTarget = false;
            applyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            applyLabel.verticalOverflow = VerticalWrapMode.Overflow;

            var textWidth = Mathf.Max(applyLabel.preferredWidth, 128f);
            var textHeight = Mathf.Max(applyLabel.preferredHeight, fontSize);
            var height = textHeight + (paddingY * 2f);
            rect.sizeDelta = new Vector2(textWidth + (paddingX * 2f), height);
            rect.anchoredPosition = Vector2.zero;

            var radius = Mathf.Min(
                PlaySettingsStyle.ApplyButtonRadius,
                Mathf.Max(8, Mathf.FloorToInt((height * 0.5f) - 1f)));
            applyFill.sprite = HomeUiFonts.Rounded(radius);

            var slot = rect.gameObject.AddComponent<LayoutElement>();
            slot.preferredWidth = rect.sizeDelta.x;
            slot.preferredHeight = rect.sizeDelta.y;
            slot.minWidth = rect.sizeDelta.x;
            slot.minHeight = rect.sizeDelta.y;
            return button;
        }

        private void CreateModalTitle(RectTransform parent, string text)
        {
            var rect = CreateRect("Title", parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -PlaySettingsStyle.TitleTopPadding);
            rect.sizeDelta = new Vector2(0f, PlaySettingsStyle.FontSize.Header + 8f);

            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = HeaderFont();
            label.fontSize = PlaySettingsStyle.FontSize.Header;
            label.color = PlaySettingsStyle.Palette.Text;
            label.alignment = TextAnchor.UpperCenter;
            label.raycastTarget = false;
        }

        private Text CreateBodyText(
            RectTransform parent,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 sizeOrMax)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (sizeOrMax == Vector2.zero && offsetMin == Vector2.zero)
            {
                Stretch(rect);
            }
            else if (sizeOrMax.y > 0f && anchorMin == anchorMax)
            {
                rect.pivot = new Vector2(anchorMin.x, 0.5f);
                rect.anchoredPosition = offsetMin;
                rect.sizeDelta = sizeOrMax;
            }
            else
            {
                rect.offsetMin = offsetMin;
                rect.offsetMax = sizeOrMax;
            }

            var label = go.AddComponent<Text>();
            label.text = text;
            label.font = BodyFont();
            label.fontSize = PlaySettingsStyle.FontSize.Body;
            label.color = PlaySettingsStyle.Palette.Text;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            ApplySingleLine(label);
            return label;
        }

        private static void ApplySingleLine(Text label)
        {
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static Font bodyFont;
        private static Font headerFont;
        private static Font extraBoldFont;
        private static Font mediumFont;
        private static Sprite copyIcon;
        private static Sprite copyCheckIcon;
        private static Sprite arrowLeftIcon;
        private static Sprite arrowRightIcon;

        private static Font BodyFont() =>
            bodyFont ??= Resources.Load<Font>(PlaySettingsStyle.RegularFontResource);

        private static Font HeaderFont() =>
            headerFont ??= Resources.Load<Font>(PlaySettingsStyle.HeaderFontResource)
            ?? BodyFont();

        private static Font ExtraBoldFont() =>
            extraBoldFont ??= Resources.Load<Font>(PlaySettingsStyle.GameStartFontResource)
            ?? HeaderFont();

        private static Font MediumFont()
        {
            if (mediumFont != null)
            {
                return mediumFont;
            }

            mediumFont = Resources.Load<Font>(PlaySettingsStyle.MediumFontResource);
#if UNITY_EDITOR
            if (mediumFont == null)
            {
                mediumFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                    "Assets/_Game/Content/Fonts/Paperlogy-5Medium.ttf");
            }
#endif
            return mediumFont ?? BodyFont();
        }

        private static Sprite LoadCopyIcon()
        {
            if (copyIcon != null)
            {
                return copyIcon;
            }

            copyIcon = Resources.Load<Sprite>(PlaySettingsStyle.CopyIconResource);
            if (copyIcon != null)
            {
                return copyIcon;
            }

            var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < 24; y++)
            {
                for (var x = 0; x < 24; x++)
                {
                    var onBack = x >= 4 && x <= 13 && y >= 8 && y <= 17;
                    var onFront = x >= 10 && x <= 19 && y >= 2 && y <= 11;
                    var border = onBack || onFront;
                    if (onFront && onBack)
                    {
                        border = x >= 10 && x <= 13 && y >= 8 && y <= 11;
                    }

                    texture.SetPixel(x, y, border ? Color.white : Color.clear);
                }
            }

            texture.Apply(false, false);
            copyIcon = Sprite.Create(texture, new Rect(0f, 0f, 24f, 24f), new Vector2(0.5f, 0.5f), 100f);
            copyIcon.hideFlags = HideFlags.HideAndDontSave;
            return copyIcon;
        }

        private static Sprite LoadCopyCheckIcon()
        {
            if (copyCheckIcon != null)
            {
                return copyCheckIcon;
            }

            copyCheckIcon = Resources.Load<Sprite>(PlaySettingsStyle.CopyCheckIconResource);
            return copyCheckIcon;
        }

        private static Sprite LoadArrowLeftIcon() =>
            arrowLeftIcon ??= Resources.Load<Sprite>(PlaySettingsStyle.ArrowLeftIconResource);

        private static Sprite LoadArrowRightIcon() =>
            arrowRightIcon ??= Resources.Load<Sprite>(PlaySettingsStyle.ArrowRightIconResource);

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }

        private void BindResetHover(Button button)
        {
            var trigger = button.gameObject.GetComponent<EventTrigger>()
                ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            AddResetHoverTrigger(trigger, EventTriggerType.PointerEnter, true);
            AddResetHoverTrigger(trigger, EventTriggerType.PointerExit, false);
            PaintResetHover();
        }

        private void AddResetHoverTrigger(EventTrigger trigger, EventTriggerType type, bool hovered)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ =>
            {
                resetHovered = hovered;
                PaintResetHover();
            });
            trigger.triggers.Add(entry);
        }

        private void PaintResetHover()
        {
            var enabled = revertButton != null && revertButton.interactable;
            var lit = enabled && resetHovered;
            if (revertFill != null)
            {
                revertFill.color = lit
                    ? PlaySettingsStyle.Palette.ResetHoverFill
                    : PlaySettingsStyle.Palette.ResetFill;
            }

            if (revertLabel != null)
            {
                revertLabel.color = !enabled
                    ? PlaySettingsStyle.Palette.ResetOffLabel
                    : lit
                        ? PlaySettingsStyle.Palette.ResetHoverLabel
                        : PlaySettingsStyle.Palette.ResetLabel;
            }

            if (revertStroke != null)
            {
                revertStroke.color = !enabled
                    ? PlaySettingsStyle.Palette.ResetOffStroke
                    : lit
                        ? PlaySettingsStyle.Palette.ResetHoverStroke
                        : PlaySettingsStyle.Palette.ResetStroke;
            }
        }
    }
}
