using Game.Client.Home;
using Game.Client.Match;
using Game.Core.Lobby;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class MatchChatViewTests
    {
        [Test]
        public void BrowserCancel_ReleasesGameplayInputWithoutSendingDraft()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform, keepChromeVisible: true);
                view.SetMode(MatchChatHudMode.Full);
                var input = view.GetComponentInChildren<TMP_InputField>(true);
                input.text = "한글 초안";
                var sent = false;
                view.SendRequested += _ => sent = true;
                typeof(MatchChatView).GetMethod("SetActivated",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { true });
                Assert.That(MatchChatView.BlocksPlayerInput, Is.True);
                view.OnCancel(new UnityEngine.EventSystems.BaseEventData(null));
                Assert.That(MatchChatView.BlocksPlayerInput, Is.False);
                Assert.That(input.text, Is.EqualTo("한글 초안"));
                Assert.That(sent, Is.False);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void SetMessages_ShowsLastFour_NewestAtBottom()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage("a", "싸피생1", "하나"),
                    new LobbyChatMessage("b", "싸피생2", "둘"),
                    new LobbyChatMessage("c", "싸피생3", "셋"),
                    new LobbyChatMessage("d", "싸피생4", "넷"),
                    new LobbyChatMessage("e", "금오산냥냥이", "안녕하십니까 여러분")
                });

                Assert.That(
                    view.transform.Find("HistoryPanel/Items/Row0").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    view.transform.Find("HistoryPanel/Items/Row0/Name").GetComponent<TMP_Text>().text,
                    Is.EqualTo("싸피생2"));
                Assert.That(
                    view.transform.Find("HistoryPanel/Items/Row3/Name").GetComponent<TMP_Text>().text,
                    Is.EqualTo("금오산냥냥이"));
                Assert.That(
                    view.transform.Find("HistoryPanel/Items/Row3/Body").GetComponent<TMP_Text>().text,
                    Is.EqualTo("안녕하십니까 여러분"));
                Assert.That(view.transform.Find("HistoryPanel/Items").childCount, Is.EqualTo(4));
                var history = view.transform.Find("HistoryPanel");
                Assert.That(history.GetComponent<Mask>(), Is.Null);
                Assert.That(history.Find("Background"), Is.Not.Null);
                var historyBackground = history.Find("Background").GetComponent<Image>();
                Assert.That(historyBackground.type, Is.EqualTo(Image.Type.Simple));
                Assert.That(historyBackground.sprite, Is.Not.Null);
                Assert.That(
                    historyBackground.sprite,
                    Is.Not.EqualTo(HomeUiFonts.Rounded(MatchChatView.PanelRadius)));
                Assert.That(historyBackground.sprite.border, Is.EqualTo(Vector4.zero));
                Assert.That(MatchChatView.PanelRadius, Is.EqualTo(10));
                var body = view.transform.Find("HistoryPanel/Items/Row3/Body").GetComponent<TMP_Text>();
                Assert.That(body.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(body.overflowMode, Is.Not.EqualTo(TextOverflowModes.Ellipsis));
                Assert.That(
                    body.GetComponent<LayoutElement>().preferredHeight,
                    Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetMessages_WrapsLongBodyAndGrowsRow()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage(
                        "a",
                        "싸피생1",
                        new string('가', LobbyChatMessage.MaxTextLength))
                });

                var body = view.transform.Find("HistoryPanel/Items/Row0/Body").GetComponent<TMP_Text>();
                Assert.That(body.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(
                    body.GetComponent<LayoutElement>().preferredHeight,
                    Is.GreaterThan(MatchChatView.BodyFontSize + 8f));
                Assert.That(
                    body.rectTransform.rect.height,
                    Is.GreaterThan(MatchChatView.BodyFontSize + 8f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetMessages_AppliesNameAndBodyStyle()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage("a", "금오산냥냥이", "안녕하십니까 여러분")
                });

                var name = view.transform.Find("HistoryPanel/Items/Row0/Name").GetComponent<TMP_Text>();
                var body = view.transform.Find("HistoryPanel/Items/Row0/Body").GetComponent<TMP_Text>();
                Assert.That(name.fontSize, Is.EqualTo(MatchChatView.NameFontSize));
                Assert.That(name.color, Is.EqualTo(MatchChatView.NameColor));
                Assert.That(body.fontSize, Is.EqualTo(MatchChatView.BodyFontSize));
                Assert.That(body.color, Is.EqualTo(Color.white));
                Assert.That(name.font, Is.Not.Null);
                Assert.That(name.font.name, Does.Contain("Paperlogy").IgnoreCase);
                Assert.That(body.font.name, Does.Contain("Paperlogy").IgnoreCase);
                var inputPanel = view.transform.Find("InputPanel").GetComponent<Image>();
                Assert.That(inputPanel.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(
                    inputPanel.sprite,
                    Is.EqualTo(HomeUiFonts.Rounded(MatchChatView.PanelRadius)));
                var input = view.transform.Find("InputPanel").GetComponent<TMP_InputField>();
                Assert.That(input.fontAsset.name, Does.Contain("Paperlogy").IgnoreCase);
                Assert.That(input.textComponent.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
                Assert.That(input.textComponent.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(input.textComponent.rectTransform.anchorMax.x, Is.EqualTo(0f));
                Assert.That(MatchChatView.SendIconGap, Is.EqualTo(8f));
                Assert.That(input.placeholder, Is.Not.Null);
                Assert.That(
                    (input.placeholder as TMP_Text).text,
                    Is.EqualTo(MatchChatView.PlaceholderText));
                Assert.That(
                    view.transform.Find("InputPanel/TextViewport/Placeholder"),
                    Is.Not.Null);
                Assert.That(view.transform.Find("InputPanel/Send"), Is.Not.Null);
                Assert.That(view.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(view.GetComponent<Canvas>().overrideSorting, Is.True);
                Assert.That(
                    view.transform.Find("InputPanel").GetComponent<Canvas>(),
                    Is.Not.Null);
                Assert.That(
                    (view.transform.Find("InputPanel") as RectTransform).sizeDelta.x,
                    Is.EqualTo(MatchChatView.InputWidth));
                Assert.That(
                    (view.transform.Find("HistoryPanel") as RectTransform).sizeDelta.x,
                    Is.EqualTo(MatchChatView.InputWidth));
                Assert.That(
                    (view.transform.Find("InputPanel/TextViewport") as RectTransform).offsetMin.x,
                    Is.EqualTo(MatchChatView.ContentPadding));
                Assert.That(
                    (view.transform.Find("InputPanel/TextViewport") as RectTransform).offsetMax.x,
                    Is.EqualTo(-(MatchChatView.SendIconSize + MatchChatView.ContentPadding +
                        MatchChatView.SendIconGap)));
                Assert.That(MatchChatView.HistoryFadeAlpha(0f), Is.EqualTo(0f));
                Assert.That(MatchChatView.HistoryFadeAlpha(1f), Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SearchingMode_HidesHistoryAndInputUntilActivated()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage("a", "싸피생1", "하나")
                });
                view.SetMode(MatchChatHudMode.Searching);

                Assert.That(view.Mode, Is.EqualTo(MatchChatHudMode.Searching));
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(
                    view.transform.Find("HistoryPanel").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    view.transform.Find("InputPanel").gameObject.activeSelf,
                    Is.False);
                Assert.That(MatchChatView.ShowsHistory(MatchChatHudMode.Searching), Is.False);
                Assert.That(MatchChatView.ShowsInput(MatchChatHudMode.Searching, false), Is.False);
                Assert.That(MatchChatView.ShowsInput(MatchChatHudMode.Searching, true), Is.True);
                Assert.That(MatchChatView.ShowsHistory(MatchChatHudMode.Full), Is.True);
                Assert.That(MatchChatView.ShowsInput(MatchChatHudMode.Full, false), Is.False);
                Assert.That(MatchChatView.ShowsInput(MatchChatHudMode.Full, true), Is.True);
                Assert.That(MatchChatView.ShowsHistory(MatchChatHudMode.HidingWait), Is.True);
                Assert.That(MatchChatView.ShowsInput(MatchChatHudMode.HidingWait, false), Is.True);
                Assert.That(MatchChatView.ShowsInput(MatchChatHudMode.HidingWait, true), Is.True);

                view.SetMode(MatchChatHudMode.Full);
                Assert.That(
                    view.transform.Find("HistoryPanel").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    view.transform.Find("InputPanel").gameObject.activeSelf,
                    Is.False);
                view.ClearInput();
                Assert.That(view.IsActivated, Is.False);
                Assert.That(
                    view.transform.Find("InputPanel").gameObject.activeSelf,
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void HidingWaitMode_ShowsHistoryAndInputWhileDeactivated()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage("a", "싸피생1", "하나")
                });
                view.SetMode(MatchChatHudMode.HidingWait);

                Assert.That(view.Mode, Is.EqualTo(MatchChatHudMode.HidingWait));
                Assert.That(view.IsActivated, Is.False);
                Assert.That(
                    view.transform.Find("HistoryPanel").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    view.transform.Find("InputPanel").gameObject.activeSelf,
                    Is.True);
                view.Deactivate();
                Assert.That(view.IsActivated, Is.False);
                Assert.That(
                    view.transform.Find("InputPanel").gameObject.activeSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void KeepChromeVisible_ShowsHistoryAndInputWhileDeactivated()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform, keepChromeVisible: true);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage("a", "싸피생1", "하나")
                });

                Assert.That(view.KeepChromeVisible, Is.True);
                Assert.That(view.IsActivated, Is.False);
                Assert.That(
                    view.transform.Find("HistoryPanel").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    view.transform.Find("InputPanel").gameObject.activeSelf,
                    Is.True);
                view.Deactivate();
                Assert.That(view.IsActivated, Is.False);
                Assert.That(
                    view.transform.Find("HistoryPanel").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    view.transform.Find("InputPanel").gameObject.activeSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ShouldOpenOnEnter_IgnoresTheEnterThatClosedChat()
        {
            Assert.That(
                MatchChatView.ShouldOpenOnEnter(false, false, true, 1f, 0.95f),
                Is.False);
            Assert.That(
                MatchChatView.ShouldOpenOnEnter(
                    false,
                    false,
                    true,
                    1f,
                    1f - MatchChatView.OpenCooldownSeconds),
                Is.True);
            Assert.That(
                MatchChatView.ShouldOpenOnEnter(true, false, true, 10f, 0f),
                Is.False);
        }

        [Test]
        public void ChatFont_PrefersBakedStaticRegularWhenPresent()
        {
            var font = MatchChatView.ChatFont();
            Assert.That(font, Is.Not.Null);
            Assert.That(font.name, Does.Contain("Paperlogy").IgnoreCase);
            var baked = Resources.Load<TMP_FontAsset>("Fonts/Paperlogy-4Regular SDF");
            if (baked != null)
            {
                Assert.That(font, Is.SameAs(baked));
                Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            }
        }

        [Test]
        public void ListScale_GrowsItemGapsWithUiAndFontSize()
        {
            Assert.That(MatchChatView.ItemSpacing, Is.EqualTo(10f));
            Assert.That(MatchChatView.NameBodySpacing, Is.EqualTo(2f));
            Assert.That(MatchChatView.ListScale(1f, 1f), Is.EqualTo(1f));
            Assert.That(
                MatchChatView.ScaledItemSpacing(1.15f),
                Is.EqualTo(11.5f).Within(0.0001f));
            Assert.That(
                MatchChatView.ScaledItemSpacing(1.15f, 1.15f),
                Is.EqualTo(13.225f).Within(0.0001f));
            Assert.That(
                MatchChatView.ScaledNameBodySpacing(1.15f),
                Is.EqualTo(2.3f).Within(0.0001f));
        }

        [Test]
        public void ContentHeightForRows_GrowsWithWrappedRows()
        {
            Assert.That(
                MatchChatView.ContentHeightForRows(new[] { 40f }, 1f),
                Is.EqualTo(MatchChatView.ContentPadding * 2f + 40f).Within(0.0001f));
            Assert.That(
                MatchChatView.ContentHeightForRows(new[] { 120f, 120f, 120f, 120f }, 1f),
                Is.EqualTo(
                    (MatchChatView.ContentPadding * 2f)
                    + 480f
                    + (MatchChatView.ItemSpacing * 3f)).Within(0.0001f));
        }

        [Test]
        public void SetMessages_KeepsHistoryFixed_AndScrollsToLatest()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform, keepChromeVisible: true);
                var longText = new string('가', LobbyChatMessage.MaxTextLength);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage("a", "싸피생1", longText),
                    new LobbyChatMessage("b", "싸피생2", longText),
                    new LobbyChatMessage("c", "싸피생3", longText),
                    new LobbyChatMessage("d", "싸피생4", longText)
                });

                var history = view.transform.Find("HistoryPanel") as RectTransform;
                Assert.That(history.sizeDelta.y, Is.EqualTo(MatchChatView.MinHistoryHeight));
                var scroll = history.GetComponent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.content, Is.SameAs(view.transform.Find("HistoryPanel/Items")));
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(0.001f));
                var items = view.transform.Find("HistoryPanel/Items");
                Assert.That(
                    (items as RectTransform).rect.height,
                    Is.GreaterThan(MatchChatView.MinHistoryHeight));
                for (var index = 0; index < MatchChatView.VisibleMessageCount - 1; index++)
                {
                    var upper = items.Find($"Row{index}") as RectTransform;
                    var lower = items.Find($"Row{index + 1}") as RectTransform;
                    Assert.That(RowsOverlapVertically(upper, lower), Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ApplyListMetrics_ScalesGapsSoRowsDoNotOverlap()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchChatView.Create(canvas.transform, keepChromeVisible: true);
                view.SetMessages(new[]
                {
                    new LobbyChatMessage("a", "싸피생1", "하나"),
                    new LobbyChatMessage("b", "싸피생2", "둘"),
                    new LobbyChatMessage("c", "싸피생3", "셋"),
                    new LobbyChatMessage("d", "싸피생4", "넷")
                });
                view.ApplyListMetrics(1.15f);

                var items = view.transform.Find("HistoryPanel/Items");
                Assert.That(
                    items.GetComponent<VerticalLayoutGroup>().spacing,
                    Is.EqualTo(MatchChatView.ScaledItemSpacing(1.15f)).Within(0.0001f));
                Assert.That(
                    items.Find("Row0").GetComponent<VerticalLayoutGroup>().spacing,
                    Is.EqualTo(MatchChatView.ScaledNameBodySpacing(1.15f)).Within(0.0001f));

                for (var index = 0; index < MatchChatView.VisibleMessageCount - 1; index++)
                {
                    var upper = items.Find($"Row{index}") as RectTransform;
                    var lower = items.Find($"Row{index + 1}") as RectTransform;
                    Assert.That(RowsOverlapVertically(upper, lower), Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void VisibleMessages_KeepsOldestOfWindowFirst()
        {
            var messages = new[]
            {
                new LobbyChatMessage("1", "A", "1"),
                new LobbyChatMessage("2", "B", "2"),
                new LobbyChatMessage("3", "C", "3"),
                new LobbyChatMessage("4", "D", "4"),
                new LobbyChatMessage("5", "E", "5")
            };

            var visible = MatchChatView.VisibleMessages(messages);
            Assert.That(visible.Count, Is.EqualTo(4));
            Assert.That(visible[0].Text, Is.EqualTo("2"));
            Assert.That(visible[3].Text, Is.EqualTo("5"));
        }

        private static bool RowsOverlapVertically(RectTransform upper, RectTransform lower)
        {
            var upperCorners = new Vector3[4];
            var lowerCorners = new Vector3[4];
            upper.GetWorldCorners(upperCorners);
            lower.GetWorldCorners(lowerCorners);
            return upperCorners[0].y < lowerCorners[2].y &&
                   lowerCorners[0].y < upperCorners[2].y;
        }
    }
}
