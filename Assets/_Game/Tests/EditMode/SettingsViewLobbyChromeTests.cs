using Game.Client.Character;
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
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(SettingsStyle.Back.Position.x));
                var label = leave.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                Assert.That(label.text, Is.EqualTo(SettingsStyle.Buttons.LeaveLabel));

                // 판과 테두리와 아이콘 (S15P21D205-1086). 글자만 두면 뒤에 오는 맵 미리보기
                // 밝기에 따라 묻힙니다.
                var plate = leave.GetComponent<Image>();
                Assert.That(plate.sprite, Is.Not.Null, "판이 없으면 밝은 맵에서 글자가 묻힙니다.");
                Assert.That(plate.color, Is.EqualTo(SettingsStyle.Palette.ChromePlateFill));
                var stroke = leave.transform.Find("Stroke").GetComponent<Image>();
                Assert.That(stroke.sprite, Is.Not.Null);
                Assert.That(stroke.color, Is.EqualTo(SettingsStyle.Palette.Accent));
                Assert.That(leave.transform.Find("Icon"), Is.Not.Null);

                // 주황은 호버에서만. Button 도 포인터를 받으므로 그쪽은 건너뛰고 부릅니다.
                var gradient = leave.GetComponent<UiLinearGradient>();
                Assert.That(gradient, Is.Not.Null);
                Assert.That(gradient.enabled, Is.False, "평소에는 어두운 판입니다.");
                foreach (var handler in
                    leave.GetComponents<UnityEngine.EventSystems.IPointerEnterHandler>())
                {
                    if (handler is Button)
                    {
                        continue;
                    }

                    handler.OnPointerEnter(null);
                }

                Assert.That(gradient.enabled, Is.True, "포인터가 올라오면 주황으로 채웁니다.");
                Assert.That(stroke.color, Is.EqualTo(SettingsStyle.Palette.TextPrimary));

                // 건너편 전체 변경 취소와 판 높이는 같지만, 그쪽은 한 줄 아래에 앉습니다
                // (2026-09-21). 게임 나가기는 맨 위 줄에 그대로 둡니다.
                var resetAll = Find(root, "ResetAllButton").GetComponent<RectTransform>();
                Assert.That(rect.rect.height, Is.EqualTo(SettingsStyle.Chrome.PlateHeight));
                Assert.That(resetAll.rect.height, Is.EqualTo(SettingsStyle.Chrome.PlateHeight));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(-SettingsStyle.Chrome.TopRowCentreY));
                Assert.That(
                    resetAll.anchoredPosition.y,
                    Is.EqualTo(rect.anchoredPosition.y - SettingsStyle.ResetAll.LobbyDropFromTopRow),
                    "로비에서는 전체 변경 취소가 맨 위 줄에서 한 줄 내려앉습니다.");
                Assert.That(
                    SettingsStyle.ResetAll.LobbyDropFromTopRow,
                    Is.GreaterThan(SettingsStyle.Chrome.PlateHeight),
                    "두 줄이 겹치면 안 됩니다.");
                Assert.That(
                    resetAll.GetComponent<Image>().sprite,
                    Is.Not.Null,
                    "전체 변경 취소도 판 위에 앉습니다. 테두리는 나가기 쪽에만 둡니다.");
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
                    Is.EqualTo("Discard"));
                Assert.That(Find(root, "DisplayModeRow").Find("Label").GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Display Mode"));
                Assert.That(Find(root, "DeviceRow").Find("Label").GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Microphone Device"));
                Assert.That(Find(root, "BackButton").GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text,
                    Is.EqualTo("← Back"));
                Assert.That(Find(root, "FeedbackButton").GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text,
                    Is.EqualTo("Send Feedback"));

                Assert.That(Find(Find(root, "Feedback").gameObject, "Title")
                        .GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Send Feedback"));

                view.ShowConfirm(SettingsConfirmKind.ResetAll, SettingsTab.General);
                Assert.That(Find(Find(root, "Confirm").gameObject, "Title")
                        .GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Discard all settings changes?"));
                Assert.That(Find(root, "DeclineButton").GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text,
                    Is.EqualTo("Cancel"));

                view.ShowConfirm(SettingsConfirmKind.ResetTab, SettingsTab.Graphics);
                Assert.That(Find(Find(root, "Confirm").gameObject, "Title")
                        .GetComponent<TMPro.TextMeshProUGUI>().text,
                    Is.EqualTo("Discard changes to Graphics settings?"));

                var confirmPanel = Find(Find(root, "Confirm").gameObject, "Panel") as RectTransform;
                var confirmTitle = Find(Find(root, "Confirm").gameObject, "Title") as RectTransform;
                var confirmSubtitle = Find(Find(root, "Confirm").gameObject, "Subtitle") as RectTransform;
                Assert.That(confirmPanel, Is.Not.Null);
                Assert.That(confirmTitle, Is.Not.Null);
                Assert.That(confirmSubtitle, Is.Not.Null);
                Assert.That(confirmTitle.sizeDelta.y, Is.GreaterThan(CharacterClosetStyle.Modal.TitleFontSize * 1.4f));
                Assert.That(confirmPanel.sizeDelta.y, Is.GreaterThan(CharacterClosetStyle.Modal.PanelSize.y));
                Assert.That(
                    confirmSubtitle.anchoredPosition.y,
                    Is.LessThan(-(CharacterClosetStyle.Modal.TitleTop + confirmTitle.sizeDelta.y)));

                var feedback = Find(root, "FeedbackButton") as RectTransform;
                var feedbackLabel = feedback.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                Assert.That(
                    feedback.sizeDelta.x,
                    Is.EqualTo(feedbackLabel.preferredWidth + (SettingsStyle.FeedbackRow.ButtonPaddingX * 2f)).Within(0.5f));
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

                // 판은 로비만의 것이 아닙니다. 같은 컨트롤이라 홈에서도 같은 모양입니다.
                var resetAll = Find(root, "ResetAllButton").GetComponent<Image>();
                Assert.That(resetAll.sprite, Is.Not.Null);
                Assert.That(resetAll.color, Is.EqualTo(SettingsStyle.Palette.ChromePlateFill));

                // 자리는 로비와 다릅니다 (S15P21D205-1098). 홈에서는 판이 화면을 거의 다 채워서
                // 한 줄 내리면 단추가 판 안으로 들어가 겹칩니다. 그래서 맨 위 줄에 그대로 둡니다.
                var resetRect = resetAll.GetComponent<RectTransform>();
                Assert.That(
                    resetRect.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        -SettingsStyle.ResetAll.RightMargin, -SettingsStyle.Chrome.TopRowCentreY)),
                    "홈에서는 전체 변경 취소가 맨 위 줄에 있어야 합니다.");
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
