using Game.Client.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class SettingsViewLobbyChromeTests
    {
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

                // 건너편 전체 변경 취소와 같은 높이로 같은 선 위에 앉습니다.
                var resetAll = Find(root, "ResetAllButton").GetComponent<RectTransform>();
                Assert.That(rect.rect.height, Is.EqualTo(SettingsStyle.Chrome.PlateHeight));
                Assert.That(resetAll.rect.height, Is.EqualTo(SettingsStyle.Chrome.PlateHeight));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(resetAll.anchoredPosition.y));
                Assert.That(
                    resetAll.GetComponent<Image>().sprite,
                    Is.Not.Null,
                    "전체 변경 취소도 판 위에 앉습니다. 테두리는 나가기 쪽에만 둡니다.");
                var close = Find(root, "CloseButton");
                Assert.That(close, Is.Not.Null);
                Assert.That(close.GetComponent<Image>().sprite, Is.Not.Null);
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
                var panel = Find(root, "Panel") as RectTransform;
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.localScale, Is.EqualTo(Vector3.one));
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
