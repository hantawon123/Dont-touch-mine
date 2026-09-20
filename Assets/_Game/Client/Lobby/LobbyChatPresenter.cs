using System;
using System.Collections.Generic;
using Game.Client.Match;
using Game.Core.Lobby;
using R3;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    public sealed class LobbyChatPresenter : IStartable, IDisposable
    {
        private readonly ILobbyChatLog chatLog;
        private readonly ILobbyChatTransport transport;
        private readonly IChatView chatView;
        private readonly IMatchChatBubbleView bubbleView;
        private IDisposable messagesSubscription;
        private int lastRenderedCount;

        public LobbyChatPresenter(
            ILobbyChatLog chatLog,
            ILobbyChatTransport transport,
            IChatView chatView,
            IMatchChatBubbleView bubbleView)
        {
            this.chatLog = chatLog ?? throw new ArgumentNullException(nameof(chatLog));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.chatView = chatView ?? throw new ArgumentNullException(nameof(chatView));
            this.bubbleView = bubbleView ?? throw new ArgumentNullException(nameof(bubbleView));
        }

        public void Start()
        {
            chatView.SendRequested += HandleSend;
            transport.ChatReceived += HandleReceived;
            messagesSubscription = chatLog.Messages.Subscribe(HandleMessagesChanged);
            HandleMessagesChanged(chatLog.Messages.CurrentValue);
        }

        public void Dispose()
        {
            chatView.SendRequested -= HandleSend;
            transport.ChatReceived -= HandleReceived;
            messagesSubscription?.Dispose();
        }

        private void HandleSend(string text)
        {
            text = LobbyChatMessage.NormalizeText(text);
            if (string.IsNullOrEmpty(text) || transport.TrySendChat(text))
            {
                chatView.ClearInput();
                chatView.Deactivate();
            }
        }

        private void HandleReceived(LobbyChatMessage message) => chatLog.Append(message);

        private void HandleMessagesChanged(IReadOnlyList<LobbyChatMessage> messages)
        {
            var list = messages ?? Array.Empty<LobbyChatMessage>();
            chatView.SetMessages(list);

            if (list.Count > lastRenderedCount)
            {
                for (var i = lastRenderedCount; i < list.Count; i++)
                {
                    bubbleView.Show(list[i]);
                }
            }

            lastRenderedCount = list.Count;
        }
    }
}
