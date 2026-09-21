using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Settings;
using R3;
using VContainer.Unity;

namespace Game.Client.Match
{
    public sealed class MatchChatPresenter : IStartable, IDisposable
    {
        private readonly ILobbyChatLog chatLog;
        private readonly IMatchChatTransport transport;
        private readonly IChatView view;
        private readonly IMatchChatBubbleView bubbleView;
        private readonly UiLocale locale;
        private IDisposable messagesSubscription;
        private Game.Core.Settings.InterfacePresentation presentation;
        [VContainer.Inject]
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value) => presentation = value;
        private void RefreshPresentation()
        {
            bubbleView?.Clear();
            HandleMessagesChanged(chatLog.Messages.CurrentValue);
        }

        public MatchChatPresenter(
            ILobbyChatLog chatLog,
            IMatchChatTransport transport,
            IChatView view)
            : this(chatLog, transport, view, null)
        {
        }

        public MatchChatPresenter(
            ILobbyChatLog chatLog,
            IMatchChatTransport transport,
            IChatView view,
            IMatchChatBubbleView bubbleView,
            UiLocale locale = null)
        {
            this.chatLog = chatLog ?? throw new ArgumentNullException(nameof(chatLog));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.bubbleView = bubbleView;
            this.locale = locale;
        }

        public void Start()
        {
            if (locale != null && view is MatchChatView chat)
            {
                locale.Changed += OnLocaleChanged;
                chat.ShowChrome(locale);
            }

            if (presentation != null) presentation.Changed += RefreshPresentation;
            view.SendRequested += HandleSend;
            transport.MatchChatReceived += HandleReceived;
            messagesSubscription = chatLog.Messages.Subscribe(HandleMessagesChanged);
            HandleMessagesChanged(chatLog.Messages.CurrentValue);
        }

        public void Dispose()
        {
            if (locale != null)
            {
                locale.Changed -= OnLocaleChanged;
            }

            if (presentation != null) presentation.Changed -= RefreshPresentation;
            view.SendRequested -= HandleSend;
            transport.MatchChatReceived -= HandleReceived;
            messagesSubscription?.Dispose();
        }

        private void OnLocaleChanged()
        {
            if (view is MatchChatView chat)
            {
                chat.ShowChrome(locale);
            }
        }

        private void HandleSend(string text)
        {
            text = LobbyChatMessage.NormalizeText(text);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (transport.TrySendMatchChat(text))
            {
                view.ClearInput();
                view.Deactivate();
            }
        }

        private void HandleReceived(LobbyChatMessage message)
        {
            chatLog.Append(message);
            if (presentation == null || presentation.ShowsChat(message.SenderId)) bubbleView?.Show(message);
        }

        private void HandleMessagesChanged(IReadOnlyList<LobbyChatMessage> messages)
        {
            if (presentation == null) { view.SetMessages(messages ?? Array.Empty<LobbyChatMessage>()); return; }
            var visible = new List<LobbyChatMessage>();
            if (messages != null)
                foreach (var message in messages)
                    if (presentation.ShowsChat(message.SenderId)) visible.Add(message);
            view.SetMessages(visible);
        }
    }
}
