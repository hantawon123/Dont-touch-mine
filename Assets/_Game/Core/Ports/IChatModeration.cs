using System;

namespace Game.Core.Ports
{
    /// <summary>Where a chat message was said. Matches the backend's scope column.</summary>
    public enum ChatScope
    {
        Lobby,
        Match
    }

    /// <summary>One line of chat, as the authority saw it before masking.</summary>
    public readonly struct ChatLogRecord
    {
        /// <param name="roomCode">
        /// The session name. A report carries a context key whose part before the '#' is this
        /// same value, which is how an investigator finds the conversation.
        /// </param>
        /// <param name="userPublicId">
        /// The speaker's backend account, or null when this peer never got one. That happens
        /// when the backend was down at launch and the client came in on the credentials it had
        /// saved (S15P21D205-925).
        /// </param>
        /// <param name="senderRef">
        /// Tells speakers apart inside this room when the account is unknown. The network player
        /// id, which means nothing once the room is gone.
        /// </param>
        /// <param name="message">
        /// <b>The original.</b> What everyone sees is the masked text; what an investigator
        /// needs is this.
        /// </param>
        public ChatLogRecord(
            string roomCode,
            ChatScope scope,
            string userPublicId,
            string senderRef,
            string message,
            DateTimeOffset saidAt)
        {
            RoomCode = roomCode;
            Scope = scope;
            UserPublicId = userPublicId;
            SenderRef = senderRef;
            Message = message;
            SaidAt = saidAt;
        }

        public string RoomCode { get; }

        public ChatScope Scope { get; }

        public string UserPublicId { get; }

        public string SenderRef { get; }

        public string Message { get; }

        public DateTimeOffset SaidAt { get; }
    }

    /// <summary>
    /// Cleans up chat and keeps a copy for report investigation (S15P21D205-1028).
    /// </summary>
    /// <remarks>
    /// Only the authority calls this, at the one place every message already
    /// passes through on its way to the other players.
    /// <para>
    /// <b>Neither call waits on the backend.</b> The word list is fetched once at
    /// startup and the judgement is made from memory, so a message is never held
    /// up by a round trip. Recording is fire-and-forget. Chat keeps working with
    /// the backend down; what is lost is the filtering and the record, not the
    /// conversation.
    /// </para>
    /// </remarks>
    public interface IChatModeration
    {
        /// <summary>
        /// The text to broadcast: the same message with forbidden words covered.
        /// </summary>
        /// <remarks>
        /// Covering rather than dropping, because a dropped message leaves the
        /// sender believing it was sent while nobody else saw it.
        /// </remarks>
        string Mask(string message);

        /// <summary>Keeps the original for an investigator. Returns immediately.</summary>
        void Record(ChatLogRecord record);
    }
}
