using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;

namespace Game.Core.Ports
{
    /// <summary>A friend asking this player to come to their room.</summary>
    public readonly struct RoomInvitation
    {
        public RoomInvitation(
            string playerId, string nickname, string roomCode, DateTime invitedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            if (string.IsNullOrWhiteSpace(roomCode))
            {
                throw new ArgumentException("Room code is required.", nameof(roomCode));
            }

            if (invitedAtUtc.Kind == DateTimeKind.Local)
            {
                throw new ArgumentException("Invited time must be UTC.", nameof(invitedAtUtc));
            }

            PlayerId = playerId.Trim();
            Nickname = nickname.Trim();
            RoomCode = roomCode.Trim();
            InvitedAtUtc = invitedAtUtc;
        }

        /// <summary>Who asked. Declining names this value.</summary>
        public string PlayerId { get; }

        public string Nickname { get; }

        /// <summary>
        /// The room to enter, for <see cref="IRoomBrowser.EnterByCodeAsync"/>.
        /// </summary>
        /// <remarks>
        /// A code says which room, not that you may enter it: a locked room still
        /// asks for its password, and a room that has since closed answers
        /// <see cref="Game.Core.Rooms.RoomEntryFailure.NotFound"/>.
        /// </remarks>
        public string RoomCode { get; }

        public DateTime InvitedAtUtc { get; }
    }

    /// <summary>
    /// Handing a room code to a friend, and reading the ones handed to this
    /// player.
    /// </summary>
    /// <remarks>
    /// The server holds no live connection to this client, so an invitation is
    /// not pushed — it waits until this player asks for it. A lobby screen polls
    /// while it is open.
    /// <para>
    /// An invitation lives three minutes. The server does not know Photon's room
    /// list and cannot tell when a room closes, so the code carries its own
    /// expiry instead. That shortens the window in which someone accepts an
    /// invitation to a room that is already gone; it does not close it, and
    /// entering can still fail.
    /// </para>
    /// </remarks>
    public interface IInviteGateway
    {
        /// <summary>
        /// Asks a friend to come to a room.
        /// </summary>
        /// <remarks>
        /// Friends only, answered with <see cref="BackendFailure.NotFriends"/>
        /// otherwise. The screen offers this from the friend list, and limiting
        /// it there keeps a room code from reaching someone this player has not
        /// befriended.
        /// <para>
        /// Asking the same friend to the same room again is not a second
        /// invitation. It restarts the three minutes.
        /// </para>
        /// </remarks>
        UniTask<BackendResult> SendAsync(
            string playerId, string roomCode, CancellationToken cancellation);

        /// <summary>
        /// Invitations waiting for this player, newest first. Expired ones are
        /// already gone.
        /// </summary>
        UniTask<BackendResult<IReadOnlyList<RoomInvitation>>> ListAsync(
            CancellationToken cancellation);

        /// <summary>
        /// Clears what a friend sent, whether it was turned down or accepted.
        /// </summary>
        /// <remarks>
        /// Clearing one that is not there succeeds — an invitation that expired
        /// between being shown and being answered is the ordinary case, not a
        /// failure.
        /// </remarks>
        UniTask<BackendResult> DeclineAsync(string playerId, CancellationToken cancellation);
    }
}
