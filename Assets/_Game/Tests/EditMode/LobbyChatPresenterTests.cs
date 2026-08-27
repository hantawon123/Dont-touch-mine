using System.Collections.Generic;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class LobbyChatPresenterTests
    {
        [Test]
        public void Start_RendersHistoryAndShowsBubbleForEachMessage()
        {
            var log = new LobbyChatLog(
                "host-1",
                "김말갈",
                new[]
                {
                    new LobbyChatMessage("player-2", "김명행", "안녕하세요"),
                    new LobbyChatMessage("host-1", "김말갈", "어서 와"),
                });
            var chatView = new FakeChatView();
            var bubbleView = new FakeBubbleView();

            using var presenter = new LobbyChatPresenter(log, chatView, bubbleView);
            presenter.Start();

            Assert.That(chatView.LastMessages.Count, Is.EqualTo(2));
            Assert.That(bubbleView.Shown.Count, Is.EqualTo(2));
            Assert.That(bubbleView.Shown[0].Text, Is.EqualTo("안녕하세요"));
            Assert.That(bubbleView.Shown[1].Text, Is.EqualTo("어서 와"));
        }

        [Test]
        public void SendRequested_AppendsLocalMessageAndClearsInput()
        {
            var log = new LobbyChatLog("host-1", "김말갈");
            var chatView = new FakeChatView();
            var bubbleView = new FakeBubbleView();

            using var presenter = new LobbyChatPresenter(log, chatView, bubbleView);
            presenter.Start();
            chatView.EmitSend("테스트 메시지");

            Assert.That(log.Messages.CurrentValue.Count, Is.EqualTo(1));
            Assert.That(log.Messages.CurrentValue[0].SenderId, Is.EqualTo("host-1"));
            Assert.That(log.Messages.CurrentValue[0].Text, Is.EqualTo("테스트 메시지"));
            Assert.That(chatView.ClearedInput, Is.True);
            Assert.That(bubbleView.Shown.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryAppendLocal_IgnoresBlank()
        {
            var log = new LobbyChatLog("host-1", "김말갈");
            Assert.That(log.TryAppendLocal("   ", out _), Is.False);
            Assert.That(log.Messages.CurrentValue.Count, Is.EqualTo(0));
        }

        [Test]
        public void BlankSubmit_ExitsChatWithoutAppendingMessage()
        {
            var log = new LobbyChatLog("host-1", "김말갈");
            var chatView = new FakeChatView();

            using var presenter = new LobbyChatPresenter(
                log,
                chatView,
                new FakeBubbleView());
            presenter.Start();
            chatView.EmitSend("   ");

            Assert.That(log.Messages.CurrentValue, Is.Empty);
            Assert.That(chatView.ClearedInput, Is.True);
        }

        [Test]
        public void TryAppendLocal_ClampsToMaxLength()
        {
            var log = new LobbyChatLog("host-1", "김말갈");
            var longText = new string('가', LobbyChatMessage.MaxTextLength + 20);

            Assert.That(log.TryAppendLocal(longText, out var message), Is.True);
            Assert.That(message.Text.Length, Is.EqualTo(LobbyChatMessage.MaxTextLength));
        }

        private sealed class FakeChatView : ILobbyChatView
        {
            public event System.Action<string> SendRequested;
            public IReadOnlyList<LobbyChatMessage> LastMessages { get; private set; }
                = System.Array.Empty<LobbyChatMessage>();
            public bool ClearedInput { get; private set; }

            public void SetMessages(IReadOnlyList<LobbyChatMessage> messages) =>
                LastMessages = messages;

            public void ClearInput() => ClearedInput = true;

            public void EmitSend(string text) => SendRequested?.Invoke(text);
        }

        private sealed class FakeBubbleView : ILobbyChatBubbleView
        {
            public List<LobbyChatMessage> Shown { get; } = new();

            public void Show(LobbyChatMessage message) => Shown.Add(message);

            public void Clear() => Shown.Clear();
        }
    }
}
