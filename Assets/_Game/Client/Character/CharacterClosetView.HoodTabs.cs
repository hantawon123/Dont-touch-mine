using Game.Client.Home;
using Game.Core.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Character
{
    public sealed partial class CharacterClosetView
    {
        private RectTransform hoodTabs;
        private CategoryTab hoodShapeTab;
        private CategoryTab hoodColorTab;
        private bool hasHoodColor;

        private void CreateHoodTabs(RectTransform locker)
        {
            hoodTabs = CreateRect("HoodTabs", locker);
            SetAnchor(hoodTabs, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1));
            hoodTabs.offsetMin = new Vector2(CharacterClosetStyle.Locker.PaddingHorizontal,
                -CharacterClosetStyle.HoodTabs.Top - CharacterClosetStyle.HoodTabs.Height);
            hoodTabs.offsetMax = new Vector2(-CharacterClosetStyle.Locker.PaddingHorizontal,
                -CharacterClosetStyle.HoodTabs.Top);
            AddImage(hoodTabs, CharacterClosetStyle.Palette.CellFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.HoodTabs.Radius));

            hoodShapeTab = CreateHoodTab(AvatarPartCategory.Hood, "후드 모양", 0);
            hoodColorTab = CreateHoodTab(AvatarPartCategory.HoodColor, "후드 색상", 1);
            hoodTabs.gameObject.SetActive(false);
        }

        private CategoryTab CreateHoodTab(AvatarPartCategory category, string title, int index)
        {
            var rect = CreateRect($"HoodTab_{category}", hoodTabs);
            SetAnchor(rect, new Vector2(index * .5f, 0), new Vector2((index + 1) * .5f, 1),
                new Vector2(.5f, .5f));
            float inset = CharacterClosetStyle.HoodTabs.Inset;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            var fill = AddImage(rect, CharacterClosetStyle.Palette.TabFill,
                HomeUiFonts.Rounded(CharacterClosetStyle.HoodTabs.Radius - 4), raycastTarget: true);

            var outline = CreateRect("Stroke", rect);
            SetAnchor(outline, Vector2.zero, Vector2.one, new Vector2(.5f, .5f));
            outline.offsetMin = outline.offsetMax = Vector2.zero;
            var stroke = AddImage(outline, CharacterClosetStyle.Palette.TabSelectedStroke,
                HomeUiFonts.Outline(CharacterClosetStyle.HoodTabs.Radius - 4, CharacterClosetStyle.HoodTabs.Stroke));
            var labelRect = CreateText("Label", rect, title, CharacterClosetStyle.HoodTabs.FontSize,
                CharacterClosetStyle.Palette.TextMuted, TextAlignmentOptions.Center);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(.5f, .5f));
            labelRect.offsetMin = new Vector2(6, 0);
            labelRect.offsetMax = new Vector2(-6, 0);

            var hover = rect.gameObject.AddComponent<HomeHoverHighlight>();
            hover.Bind(fill, null, CharacterClosetStyle.Palette.TabFill, CharacterClosetStyle.Palette.TabHoverFill);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => CategorySelected?.Invoke(category));
            buttons.Add(button);
            return new CategoryTab(category, stroke, labelRect.GetComponent<TMP_Text>(), button);
        }

        private void UpdateHoodTabs(AvatarPartCategory? category)
        {
            bool visible = hasHoodColor &&
                (category == AvatarPartCategory.Hood || category == AvatarPartCategory.HoodColor);
            hoodTabs.gameObject.SetActive(visible);
            MarkHoodTab(hoodShapeTab, category);
            MarkHoodTab(hoodColorTab, category);

            float top = visible
                ? CharacterClosetStyle.HoodTabs.Top + CharacterClosetStyle.HoodTabs.Height + CharacterClosetStyle.HoodTabs.GridGap
                : CharacterClosetStyle.Locker.PaddingVertical;
            float bottom = visible ? CharacterClosetStyle.HoodTabs.Bottom : CharacterClosetStyle.Locker.PaddingVertical;
            var viewport = lockerScroll.viewport;
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, bottom);
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -top);
            var track = lockerScroll.verticalScrollbar.GetComponent<RectTransform>();
            track.offsetMin = new Vector2(track.offsetMin.x, bottom);
            track.offsetMax = new Vector2(track.offsetMax.x, -top);
        }

        private static void MarkHoodTab(CategoryTab tab, AvatarPartCategory? selected)
        {
            bool active = tab.Category == selected;
            tab.Stroke.enabled = active;
            tab.Label.color = active
                ? CharacterClosetStyle.Palette.TabSelectedLabel
                : CharacterClosetStyle.Palette.TextMuted;
        }
    }
}
