using Game.Client.Home;
using Game.Client.Match;
using Game.Core.Lobby;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class LobbyChatViewTests
    {
        [Test]
        public void Awake_BuildsPlaygroundHud_AndKeepsChromeVisible()
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
                    new LobbyChatMessage("d", "싸피생4", "넷"),
                    new LobbyChatMessage("e", "금오산냥냥이", "안녕하십니까 여러분")
                });

                Assert.That(view.IsActivated, Is.False);
                Assert.That(view.transform.Find("HistoryPanel").gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("InputPanel").gameObject.activeSelf, Is.True);
                Assert.That(
                    view.transform.Find("HistoryPanel/Items/Row0/Name").GetComponent<TMP_Text>().text,
                    Is.EqualTo("싸피생1"));
                Assert.That(
                    view.transform.Find("HistoryPanel/Items/Row4/Body").GetComponent<TMP_Text>().text,
                    Is.EqualTo("안녕하십니까 여러분"));
                var input = view.transform.Find("InputPanel").GetComponent<TMP_InputField>();
                Assert.That(input.placeholder is TMP_Text placeholder
                    ? placeholder.text
                    : null, Is.EqualTo(MatchChatView.PlaceholderText));
                Assert.That(view.transform.Find("InputPanel/Send"), Is.Not.Null);
                view.Deactivate();
                Assert.That(view.transform.Find("HistoryPanel").gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("InputPanel").gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Bubble_MatchesPlaygroundStyle()
        {
            var parent = new GameObject("ChatRoot");
            var player = new GameObject("Player");
            try
            {
                var bubbles = MatchChatBubbleView.Create(parent.transform);
                bubbles.BindPlayer("P1", player.transform);
                bubbles.Show(new LobbyChatMessage("P1", "플레이어", "짧음"));

                var bubble = player.transform.Find("Match Chat Bubble")
                    .GetComponent<RectTransform>();
                var shortWidth = bubble.sizeDelta.x;
                bubbles.Show(new LobbyChatMessage(
                    "P1",
                    "플레이어",
                    "이 메시지는 짧은 메시지보다 훨씬 길어서 말풍선 너비가 더 넓어져야 합니다."));

                Assert.That(bubble.sizeDelta.x, Is.GreaterThan(shortWidth));
                Assert.That(
                    bubble.sizeDelta.x,
                    Is.LessThanOrEqualTo(MatchChatBubbleView.MaxBubbleWidth));
                var bubbleText = bubble.GetComponentInChildren<TMP_Text>();
                Assert.That(bubbleText.fontSize, Is.EqualTo(MatchChatBubbleView.FontSize));
                Assert.That(bubbleText.color, Is.EqualTo(MatchChatBubbleView.TextColor));
                Assert.That(bubbleText.color, Is.EqualTo(Color.white));
                var panel = bubble.Find("Panel")?.GetComponent<Image>();
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(MatchChatBubbleView.BubbleRadius, Is.EqualTo(6));
                Assert.That(
                    panel.sprite,
                    Is.EqualTo(HomeUiFonts.Rounded(MatchChatBubbleView.BubbleRadius)));
                Assert.That(panel.pixelsPerUnitMultiplier, Is.EqualTo(1f));
                Assert.That(panel.color, Is.EqualTo(MatchChatBubbleView.BubbleColor));
                Assert.That(panel.color.a, Is.EqualTo(0.8f));
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(player);
            }
        }
    }
}
