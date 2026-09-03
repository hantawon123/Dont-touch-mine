using System;
using System.Collections.Generic;
using Game.Client.Match;
using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MatchChatPresenterTests
    {
        [Test]
        public void Start_RendersCurrentHistory_AndReceivedMessages()
        {
            var log = new LobbyChatLog(
                "host-1",
                "호스트",
                new[] { new LobbyChatMessage("client-1", "클라이언트", "안녕하세요") });
            var view = new FakeView();
            var transport = new FakeTransport();
            var bubbles = new FakeBubbleView();

            using var presenter = new MatchChatPresenter(log, transport, view, bubbles);
            presenter.Start();

            Assert.That(view.LastMessages.Count, Is.EqualTo(1));
            transport.Emit(new LobbyChatMessage("client-1", "클라이언트", "반가워요"));
            Assert.That(view.LastMessages.Count, Is.EqualTo(2));
            Assert.That(bubbles.Shown[0].Text, Is.EqualTo("반가워요"));
        }

        [Test]
        public void BlankSubmit_DoesNotSendOrClear()
        {
            var log = new LobbyChatLog("host-1", "호스트");
            var view = new FakeView();
            var transport = new FakeTransport();

            using var presenter = new MatchChatPresenter(log, transport, view);
            presenter.Start();
            view.EmitSend("   ");

            Assert.That(transport.Sent, Is.Empty);
            Assert.That(view.ClearCount, Is.Zero);
        }

        [Test]
        public void NonBlankSubmit_SendsAndClears()
        {
            var log = new LobbyChatLog("host-1", "호스트");
            var view = new FakeView();
            var transport = new FakeTransport { AcceptsSend = true };

            using var presenter = new MatchChatPresenter(log, transport, view);
            presenter.Start();
            view.EmitSend("테스트");

            Assert.That(transport.Sent, Is.EqualTo(new[] { "테스트" }));
            Assert.That(view.ClearCount, Is.EqualTo(1));
        }

        [Test]
        public void Bubble_WidthFollowsMessageLength_WithinMaximum()
        {
            var parent = new UnityEngine.GameObject("ChatRoot");
            var player = new UnityEngine.GameObject("Player");
            try
            {
                var bubbles = MatchChatBubbleView.Create(parent.transform);
                bubbles.BindPlayer("P1", player.transform);

                bubbles.Show(new LobbyChatMessage("P1", "Player", "짧음"));
                var bubble = player.transform.Find("Match Chat Bubble")
                    .GetComponent<UnityEngine.RectTransform>();
                var shortWidth = bubble.sizeDelta.x;

                bubbles.Show(new LobbyChatMessage(
                    "P1",
                    "Player",
                    "이 메시지는 짧은 메시지보다 훨씬 길어서 말풍선 너비가 더 넓어져야 합니다."));

                Assert.That(bubble.sizeDelta.x, Is.GreaterThan(shortWidth));
                Assert.That(bubble.sizeDelta.x, Is.LessThanOrEqualTo(420f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private sealed class FakeView : IMatchChatView
        {
            public event Action<string> SendRequested;
            public IReadOnlyList<LobbyChatMessage> LastMessages { get; private set; }
                = Array.Empty<LobbyChatMessage>();
            public int ClearCount { get; private set; }

            public void SetMessages(IReadOnlyList<LobbyChatMessage> messages) => LastMessages = messages;

            public void ClearInput() => ClearCount++;

            public void EmitSend(string text) => SendRequested?.Invoke(text);
        }

        private sealed class FakeTransport : IMatchChatTransport
        {
            public event Action<LobbyChatMessage> MatchChatReceived;
            public List<string> Sent { get; } = new();
            public bool AcceptsSend { get; set; }

            public bool TrySendMatchChat(string text)
            {
                Sent.Add(text);
                return AcceptsSend;
            }

            public void Emit(LobbyChatMessage message) => MatchChatReceived?.Invoke(message);
        }

        private sealed class FakeBubbleView : IMatchChatBubbleView
        {
            public List<LobbyChatMessage> Shown { get; } = new();

            public void BindPlayer(string playerId, UnityEngine.Transform playerRoot) { }

            public void Show(LobbyChatMessage message) => Shown.Add(message);

            public void Clear() { }
        }
    }
}
