using System;
using R3;

namespace Game.Core.Ports
{
    /// <summary>
    /// Where the realtime link to the backend stands.
    /// </summary>
    /// <remarks>
    /// <see cref="Connected"/> means the server has acknowledged who this
    /// client is, not merely that a socket is open. Between the two the server
    /// does not yet know whom the socket belongs to and would drop anything it
    /// had to say; a screen that treated "open" as "connected" would miss
    /// exactly the notifications it reconnected to catch.
    /// </remarks>
    public enum NotificationLinkState
    {
        Disconnected,
        Connecting,
        Connected
    }

    /// <summary>
    /// The kinds of notification the server pushes. One for one with the
    /// server's <c>NotificationType</c>.
    /// </summary>
    /// <remarks>
    /// There is no "invite removed". An invite that is gone is discovered the
    /// same way it always was — by asking for the list — because the server
    /// treats a notification as a nudge to re-read, never as the truth itself.
    /// </remarks>
    public enum ServerNotificationKind
    {
        /// <summary>Someone sent this player a friend request.</summary>
        FriendRequestReceived,

        /// <summary>A request this player sent was accepted.</summary>
        FriendRequestAccepted,

        /// <summary>
        /// A request involving this player is gone — declined or withdrawn. The
        /// server does not say which.
        /// </summary>
        FriendRequestRemoved,

        /// <summary>
        /// The other side ended the friendship. Any room invite between the two
        /// went with it.
        /// </summary>
        FriendRemoved,

        /// <summary>
        /// A friend invited this player to a room. Inviting again to the same
        /// room arrives as this kind again.
        /// </summary>
        RoomInviteReceived
    }

    /// <summary>
    /// One thing the server pushed.
    /// </summary>
    /// <remarks>
    /// A signal, not the record. The lists the screens show still come from
    /// REST; this only says that one of them changed and who caused it. A
    /// notification that is missed costs nothing that the next full read does
    /// not repair, which is why the server keeps none for a client that is away.
    /// </remarks>
    public readonly struct ServerNotification
    {
        public ServerNotification(
            ServerNotificationKind kind,
            string fromPlayerId,
            string fromNickname,
            string roomCode,
            DateTime sentAtUtc)
        {
            if (!Enum.IsDefined(typeof(ServerNotificationKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (string.IsNullOrWhiteSpace(fromPlayerId))
            {
                throw new ArgumentException("Sender id is required.", nameof(fromPlayerId));
            }

            Kind = kind;
            FromPlayerId = fromPlayerId;
            FromNickname = fromNickname ?? string.Empty;
            RoomCode = string.IsNullOrEmpty(roomCode) ? null : roomCode;
            SentAtUtc = sentAtUtc;
        }

        public ServerNotificationKind Kind { get; }

        /// <summary>Who caused it.</summary>
        public string FromPlayerId { get; }

        /// <summary>Their name, ready for a toast line.</summary>
        public string FromNickname { get; }

        /// <summary>
        /// The room, for <see cref="ServerNotificationKind.RoomInviteReceived"/>.
        /// Null for everything else.
        /// </summary>
        public string RoomCode { get; }

        public DateTime SentAtUtc { get; }
    }

    /// <summary>
    /// The realtime channel from the backend: what it pushes, and whether it is
    /// currently listening.
    /// </summary>
    /// <remarks>
    /// The port carries no way to send. Screens react to what arrives and act
    /// through the REST ports as they always have; the one thing the client does
    /// say on this channel — where it is — belongs to <c>IPresenceGateway</c>,
    /// which stays the single place that reports presence whichever wire it
    /// happens to use.
    /// <para>
    /// A consumer that needs to catch up should watch for
    /// <see cref="NotificationLinkState.Connected"/> and re-read its lists then.
    /// Every reconnection produces one, so what happened during the gap is
    /// picked up without anyone counting the gaps.
    /// </para>
    /// </remarks>
    public interface INotificationStream
    {
        Observable<ServerNotification> Notifications { get; }

        ReadOnlyReactiveProperty<NotificationLinkState> State { get; }
    }
}
