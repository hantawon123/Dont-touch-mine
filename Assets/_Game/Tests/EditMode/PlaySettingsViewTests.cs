using System.Reflection;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class PlaySettingsViewTests
    {
        [Test]
        public void TitleInput_LeavesRoomForTheCharacterCounter()
        {
            var root = CreateView(out var panel, out _);
            try
            {
                var input = Find(panel.transform, "TitleInput") as RectTransform;
                var underline = Find(panel.transform, "Underline") as RectTransform;
                var counter = Find(panel.transform, "TitleCounter") as RectTransform;
                Assert.That(input, Is.Not.Null);
                Assert.That(underline, Is.Not.Null);
                Assert.That(counter, Is.Not.Null);
                Assert.That(
                    input.offsetMax.x,
                    Is.EqualTo(-PlaySettingsStyle.Layout.TitleInputRightPadding));
                Assert.That(
                    underline.sizeDelta.x,
                    Is.EqualTo(-PlaySettingsStyle.Layout.TitleInputRightPadding));
                Assert.That(input.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(counter.anchorMin, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(counter.anchorMax, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(
                    counter.sizeDelta.x,
                    Is.EqualTo(PlaySettingsStyle.Layout.TitleCounterWidth));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CloseButton_SitsOnTheModalTopRightCorner()
        {
            var root = CreateView(out var panel, out var view);
            try
            {
                var close = panel.transform.Find("CloseButton") as RectTransform;
                Assert.That(close, Is.Not.Null);
                Assert.That(close.parent, Is.EqualTo(panel.transform));
                Assert.That(close.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(close.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(close.pivot, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(
                    close.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        -PlaySettingsStyle.Overlay.CloseOffset.x,
                        -PlaySettingsStyle.Overlay.CloseOffset.y)));
                Assert.That(
                    close.sizeDelta,
                    Is.EqualTo(new Vector2(
                        PlaySettingsStyle.Overlay.CloseSize,
                        PlaySettingsStyle.Overlay.CloseSize)));
                Assert.That(close.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(Find(root.transform, "BackButton"), Is.Null);

                var overlay = Find(root.transform, "PlaySettingsOverlay");
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.Find("CloseButton"), Is.Null);

                var raised = 0;
                view.CloseRequested += () => raised++;
                close.GetComponent<Button>().onClick.Invoke();
                Assert.That(raised, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResetButton_SharesTheFooterRowWithApply()
        {
            var root = CreateView(out var panel, out var view);
            try
            {
                var reset = Find(panel.transform, "ResetButton") as RectTransform;
                var apply = Find(panel.transform, "ApplyButton") as RectTransform;
                Assert.That(reset, Is.Not.Null);
                Assert.That(apply, Is.Not.Null);
                Assert.That(reset.parent.name, Is.EqualTo("ActionRow"));
                Assert.That(apply.parent, Is.EqualTo(reset.parent));
                Assert.That(reset.GetSiblingIndex(), Is.LessThan(apply.GetSiblingIndex()));
                Assert.That(
                    reset.Find("Text").GetComponent<Text>().text,
                    Is.EqualTo(PlaySettingsStyle.Layout.ResetLabel));
                var resetText = reset.Find("Text").GetComponent<Text>();
                var applyText = apply.Find("Text").GetComponent<Text>();
                Assert.That(resetText.font, Is.EqualTo(applyText.font));
                Assert.That(resetText.fontSize, Is.EqualTo(PlaySettingsStyle.FontSize.Apply));
                Assert.That(applyText.fontSize, Is.EqualTo(PlaySettingsStyle.FontSize.Apply));
                Assert.That(resetText.fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(applyText.fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(
                    reset.GetComponent<Image>().color,
                    Is.EqualTo(PlaySettingsStyle.Palette.ResetFill));
                Assert.That(
                    reset.Find("Stroke").GetComponent<Image>().color,
                    Is.EqualTo(PlaySettingsStyle.Palette.ResetOffStroke));
                Assert.That(Find(panel.transform, "Header").Find("ResetButton"), Is.Null);
                Assert.That(Find(panel.transform, "Header").Find("RevertButton"), Is.Null);

                view.SetDraft(new PlaySettingsDraft("방", "CODE", false, null, 4, 3, "playground"));
                view.SetEditable(true);
                Assert.That(reset.GetComponent<Button>().interactable, Is.False);
                Assert.That(
                    reset.Find("Text").GetComponent<Text>().color,
                    Is.EqualTo(PlaySettingsStyle.Palette.ResetOffLabel));
                Assert.That(
                    reset.Find("Stroke").GetComponent<Image>().color,
                    Is.EqualTo(PlaySettingsStyle.Palette.ResetOffStroke));

                var plus = (Button)typeof(PlaySettingsView).GetField(
                    "maxPlayersPlusButton",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                plus.onClick.Invoke();
                Assert.That(reset.GetComponent<Button>().interactable, Is.True);
                Assert.That(
                    reset.Find("Text").GetComponent<Text>().color,
                    Is.EqualTo(PlaySettingsStyle.Palette.ResetLabel));
                Assert.That(
                    reset.Find("Stroke").GetComponent<Image>().color,
                    Is.EqualTo(PlaySettingsStyle.Palette.ResetStroke));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BodyScroll_ShowsAVerticalScrollbarOnTheRight()
        {
            var root = CreateView(out var panel, out _);
            try
            {
                var scroll = Find(panel.transform, "SettingsScroll") as RectTransform;
                Assert.That(scroll, Is.Not.Null);
                var scrollRect = scroll.GetComponent<ScrollRect>();
                Assert.That(scrollRect, Is.Not.Null);
                Assert.That(scrollRect.verticalScrollbar, Is.Not.Null);
                Assert.That(
                    scrollRect.verticalScrollbarVisibility,
                    Is.EqualTo(ScrollRect.ScrollbarVisibility.Permanent));

                var bar = scrollRect.verticalScrollbar.GetComponent<RectTransform>();
                Assert.That(bar.parent, Is.EqualTo(scroll));
                Assert.That(bar.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(bar.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(bar.pivot, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(
                    bar.anchoredPosition,
                    Is.EqualTo(new Vector2(-PlaySettingsStyle.Layout.ScrollbarRightInset, 0f)));
                Assert.That(
                    bar.sizeDelta,
                    Is.EqualTo(new Vector2(
                        PlaySettingsStyle.Layout.ScrollbarWidth,
                        -PlaySettingsStyle.Layout.ScrollbarVerticalInset * 2f)));
                Assert.That(bar.Find("SlidingArea/Handle"), Is.Not.Null);
                Assert.That(Find(panel.transform, "Header").Find("Scrollbar"), Is.Null);
                Assert.That(Find(panel.transform, "Footer").Find("Scrollbar"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Guest_HidesFooterAndLetsTheBodyFillIt()
        {
            var root = CreateView(out var panel, out var view);
            try
            {
                view.SetEditable(false);
                var footer = panel.transform.Find("Footer");
                var body = panel.transform.Find("Body") as RectTransform;
                Assert.That(footer, Is.Not.Null);
                Assert.That(body, Is.Not.Null);
                Assert.That(footer.gameObject.activeSelf, Is.False);
                Assert.That(body.offsetMin.y, Is.EqualTo(0f));
                Assert.That(
                    Find(footer, "ResetButton").gameObject.activeInHierarchy,
                    Is.False);
                Assert.That(
                    Find(footer, "ApplyButton").gameObject.activeInHierarchy,
                    Is.False);

                view.SetEditable(true);
                Assert.That(footer.gameObject.activeSelf, Is.True);
                Assert.That(
                    body.offsetMin.y,
                    Is.EqualTo(PlaySettingsStyle.FooterHeight));
                Assert.That(
                    Find(footer, "ResetButton").gameObject.activeInHierarchy,
                    Is.True);
                Assert.That(
                    Find(footer, "ApplyButton").gameObject.activeInHierarchy,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateView(out GameObject panel, out PlaySettingsView view)
        {
            var root = new GameObject("Settings layout", typeof(RectTransform), typeof(Canvas));
            panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            view = root.AddComponent<PlaySettingsView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            typeof(PlaySettingsView).GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            return root;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
