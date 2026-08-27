using System;
using System.Collections.Generic;
using R3;

namespace Game.Core.Lobby
{
    public readonly struct LobbyChatMessage
    {
        public const int MaxTextLength = 80;

        public LobbyChatMessage(string senderId, string senderName, string text)
        {
            if (string.IsNullOrWhiteSpace(senderId))
            {
                throw new ArgumentException("Sender id is required.", nameof(senderId));
            }

            if (string.IsNullOrWhiteSpace(senderName))
            {
                throw new ArgumentException("Sender name is required.", nameof(senderName));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Message text is required.", nameof(text));
            }

            SenderId = senderId.Trim();
            SenderName = senderName.Trim();
            Text = ClampText(text.Trim());
        }

        public string SenderId { get; }
        public string SenderName { get; }
        public string Text { get; }

        public static string ClampText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxTextLength)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, MaxTextLength);
        }
    }

    public interface ILobbyChatTransport
    {
        event Action<LobbyChatMessage> ChatReceived;

        bool TrySendChat(string text);
    }

    public interface ILobbyChatLog
    {
        string LocalPlayerId { get; }
        string LocalDisplayName { get; }
        ReadOnlyReactiveProperty<IReadOnlyList<LobbyChatMessage>> Messages { get; }

        bool TryAppendLocal(string text, out LobbyChatMessage message);
        void Append(LobbyChatMessage message);
    }

    public sealed class LobbyChatLog : ILobbyChatLog, IDisposable
    {
        private const int MaxMessages = 50;
        private readonly ReactiveProperty<IReadOnlyList<LobbyChatMessage>> messages;
        private readonly List<LobbyChatMessage> buffer = new();

        public LobbyChatLog(
            string localPlayerId,
            string localDisplayName,
            IReadOnlyList<LobbyChatMessage> initialMessages = null)
        {
            if (string.IsNullOrWhiteSpace(localPlayerId))
            {
                throw new ArgumentException("Local player id is required.", nameof(localPlayerId));
            }

            if (string.IsNullOrWhiteSpace(localDisplayName))
            {
                throw new ArgumentException("Local display name is required.", nameof(localDisplayName));
            }

            LocalPlayerId = localPlayerId.Trim();
            LocalDisplayName = localDisplayName.Trim();

            if (initialMessages != null)
            {
                for (var i = 0; i < initialMessages.Count; i++)
                {
                    buffer.Add(initialMessages[i]);
                }
            }

            messages = new ReactiveProperty<IReadOnlyList<LobbyChatMessage>>(CloneBuffer());
        }

        public string LocalPlayerId { get; }
        public string LocalDisplayName { get; }
        public ReadOnlyReactiveProperty<IReadOnlyList<LobbyChatMessage>> Messages => messages;

        public bool TryAppendLocal(string text, out LobbyChatMessage message)
        {
            message = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            message = new LobbyChatMessage(LocalPlayerId, LocalDisplayName, text);
            Append(message);
            return true;
        }

        public void Append(LobbyChatMessage message)
        {
            buffer.Add(message);
            while (buffer.Count > MaxMessages)
            {
                buffer.RemoveAt(0);
            }

            messages.Value = CloneBuffer();
        }

        public void Dispose() => messages.Dispose();

        private IReadOnlyList<LobbyChatMessage> CloneBuffer()
        {
            if (buffer.Count == 0)
            {
                return Array.Empty<LobbyChatMessage>();
            }

            return buffer.ToArray();
        }
    }
}
