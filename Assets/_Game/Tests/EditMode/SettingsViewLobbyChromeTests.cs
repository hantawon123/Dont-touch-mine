using Game.Client.Settings;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class SettingsViewLobbyChromeTests
    {
        [Test]
        public void CloseIcon_IsAvailableThroughPlayerResources()
        {
            var sprite = Resources.Load<Sprite>(SettingsStyle.CloseIconResource);
            Assert.That(sprite, Is.Not.Null, "Player builds must load the close icon through Resources.");
            Assert.That(SettingsStyle.LoadCloseIcon(), Is.EqualTo(sprite));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        public void SliderValue_ClipsRailWithoutShrinkingRoundedEnds(int percent)
        {
            var root = new GameObject("Slider Test", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<SettingsView>();
                typeof(SettingsView).GetMethod("CreateSlider",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { root.GetComponent<RectTransform>(), 0, 100,
                        new System.Action<int>(_ => { }) });
                var slider = root.GetComponentInChildren<Slider>(true);
                slider.SetValueWithoutNotify(percent);
                var graphic = slider.fillRect.Find("Graphic").GetComponent<Image>();
                var track = slider.transform.Find("Track").GetComponent<RectTransform>();

                Assert.That(slider.fillRect.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(slider.fillRect.anchorMax.x, Is.EqualTo(percent / 100f).Within(0.001f));
                Assert.That(graphic.rectTransform.rect.size, Is.EqualTo(
                    new Vector2(SettingsStyle.Slider.TrackSize.x + SettingsStyle.Slider.FillLeftOverhang,
                        SettingsStyle.Slider.FillHeight)));
                var background = track.transform.Find("Background").GetComponent<RectTransform>();
                Assert.That(background.rect.height, Is.EqualTo(SettingsStyle.Slider.TrackSize.y));
                Assert.That(graphic.rectTransform.rect.height, Is.GreaterThan(background.rect.height));
                Assert.That(track.rect.height, Is.EqualTo(graphic.rectTransform.rect.height));
                Assert.That(background.anchoredPosition.y, Is.Zero);
                Assert.That(graphic.rectTransform.anchoredPosition.y, Is.Zero);
                Assert.That(track.GetComponent<Mask>(), Is.Null);
                Assert.That(graphic.transform.IsChildOf(track.transform), Is.True);
                Assert.That(graphic.sprite, Is.Not.Null);
                Assert.That(graphic.type, Is.EqualTo(Image.Type.Sliced));
                var fillCorners = new Vector3[4];
                slider.fillRect.GetWorldCorners(fillCorners);
                var handleCentre = slider.handleRect.TransformPoint(slider.handleRect.rect.center);
                Assert.That(fillCorners[2].x, Is.EqualTo(handleCentre.x).Within(0.001f),
                    "Orange must reach the handle centre even at low values.");
                var trackCorners = new Vector3[4];
                track.GetWorldCorners(trackCorners);
                Assert.That(fillCorners[0].x,
                    Is.EqualTo(trackCorners[0].x - SettingsStyle.Slider.FillLeftOverhang).Within(0.001f));
                Assert.That(graphic.color.a, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConfigureAsLobbyOverlay_HidesFeedbackAndShowsLeaveText()
        {
            var root = new GameObject("Lobby Settings");
            try
            {
                root.SetActive(false);
                var view = root.AddComponent<SettingsView>();
                view.ConfigureAsLobbyOverlay();
                root.SetActive(true);
                typeof(SettingsView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);

                Assert.That(Find(root, "FeedbackRow"), Is.Null);
                Assert.That(Find(root, "BackButton"), Is.Null);
                Assert.That(Find(root, "LeaveGameButton"), Is.Null);
                var panel = Find(root, "Panel") as RectTransform;
                Assert.That(panel, Is.Not.Null);
                Assert.That(
                    panel.localScale,
                    Is.EqualTo(Vector3.one * SettingsStyle.Frame.LobbyScale));
                var leave = Find(root, "LeaveGameLabel");
                Assert.That(leave, Is.Not.Null);
                var rect = leave.GetComponent<RectTransform>();
                Assert.That(rect.anchoredPosition, Is.EqualTo(SettingsStyle.Back.Position));
                var label = leave.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                Assert.That(label.text, Is.EqualTo(SettingsStyle.Buttons.LeaveLabel));
                var close = Find(root, "CloseButton");
                Assert.That(close, Is.Not.Null);
                Assert.That(
                    close.GetComponent<Image>().sprite,
                    Is.EqualTo(SettingsStyle.LoadCloseIcon()));
                var background = Find(root, "Background").GetComponent<Image>();
                Assert.That(background.color, Is.EqualTo(SettingsStyle.Palette.OverlayDim));
                Assert.That(background.color.a, Is.EqualTo(0.8f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShowChrome_RedrawsTabsAndButtonsInTheAppliedLanguage()
        {
            var root = new GameObject("Settings chrome");
            try
            {
                var view = root.AddComponent<SettingsView>();
                typeof(SettingsView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);

                var store = new InMemoryGeneralSettingsStore();
                store.Save(new GeneralSettings("en"));
                var general = new GeneralSettingsSystem(store);
                using var locale = new UiLocale(general);

                view.ShowChrome(locale);

                Assert.That(Find(root, "GeneralTab").GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text,
                    Is.EqualTo("General"));
                Assert.That(Find(root, "LanguageRow").Find("Label").GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Language"));
                Assert.That(Find(root, "ApplyButton").GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text,
                    Is.EqualTo("Apply"));
                Assert.That(Find(root, "ResetButton").GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text,
                    Is.EqualTo("Discard Changes"));
                Assert.That(Find(root, "DisplayModeRow").Find("Label").GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Display Mode"));
                Assert.That(Find(root, "DeviceRow").Find("Label").GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Microphone Device"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HomeLayout_KeepsFeedbackAndOmitsLeaveButton()
        {
            var root = new GameObject("Home Settings");
            try
            {
                var view = root.AddComponent<SettingsView>();
                typeof(SettingsView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);

                Assert.That(Find(root, "FeedbackRow"), Is.Not.Null);
                Assert.That(Find(root, "LeaveGameButton"), Is.Null);
                Assert.That(Find(root, "LeaveGameLabel"), Is.Null);
                Assert.That(Find(root, "BackButton"), Is.Not.Null);
                var panel = Find(root, "Panel") as RectTransform;
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ModalOnlyLayout_ReusesGameConfirmationWithoutSettingsScreen()
        {
            var root = new GameObject("Tutorial Exit Confirmation");
            try
            {
                root.SetActive(false);
                var view = root.AddComponent<SettingsView>();
                view.ConfigureAsModalOnly();
                root.SetActive(true);
                typeof(SettingsView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);

                Assert.That(Find(root, "Confirm"), Is.Not.Null);
                Assert.That(Find(root, "Background"), Is.Null);
                Assert.That(Find(root, "FeedbackRow"), Is.Null);
                Assert.That(Find(root, "BackButton"), Is.Null);

                var canvas = root.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.sortingOrder, Is.EqualTo(SettingsStyle.GameplayOverlaySortingOrder));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform;
                }
            }

            return null;
        }
    }
}
