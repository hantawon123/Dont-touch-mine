using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using R3;
using VContainer.Unity;

namespace Game.Client.Match
{
    public sealed class MatchChatPresenter : IStartable, IDisposable
    {
        private readonly ILobbyChatLog chatLog;
        private readonly IMatchChatTransport transport;
        private readonly IMatchChatView view;
        private readonly IMatchChatBubbleView bubbleView;
        private IDisposable messagesSubscription;

        public MatchChatPresenter(
            ILobbyChatLog chatLog,
            IMatchChatTransport transport,
            IMatchChatView view)
            : this(chatLog, transport, view, null)
        {
        }

        public MatchChatPresenter(
            ILobbyChatLog chatLog,
            IMatchChatTransport transport,
            IMatchChatView view,
            IMatchChatBubbleView bubbleView)
        {
            this.chatLog = chatLog ?? throw new ArgumentNullException(nameof(chatLog));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.bubbleView = bubbleView;
        }

        public void Start()
        {
            view.SendRequested += HandleSend;
            transport.MatchChatReceived += HandleReceived;
            messagesSubscription = chatLog.Messages.Subscribe(HandleMessagesChanged);
            HandleMessagesChanged(chatLog.Messages.CurrentValue);
        }

        public void Dispose()
        {
            view.SendRequested -= HandleSend;
            transport.MatchChatReceived -= HandleReceived;
            messagesSubscription?.Dispose();
        }

        private void HandleSend(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (transport.TrySendMatchChat(text))
            {
                view.ClearInput();
            }
        }

        private void HandleReceived(LobbyChatMessage message)
        {
            chatLog.Append(message);
            bubbleView?.Show(message);
        }

        private void HandleMessagesChanged(IReadOnlyList<LobbyChatMessage> messages) =>
            view.SetMessages(messages ?? Array.Empty<LobbyChatMessage>());
    }
}
