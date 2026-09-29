using System;
using System.Collections.Generic;

namespace Game.Core.Home
{
    /// <summary>
    /// One invitation to a room, as the toast shows it.
    /// </summary>
    public readonly struct RoomInvite
    {
        public RoomInvite(string id, string fromPlayerId, string fromNickname, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Invite id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(fromPlayerId))
            {
                throw new ArgumentException("Sender id is required.", nameof(fromPlayerId));
            }

            if (string.IsNullOrWhiteSpace(roomCode))
            {
                throw new ArgumentException("Room code is required.", nameof(roomCode));
            }

            Id = id;
            FromPlayerId = fromPlayerId;
            FromNickname = string.IsNullOrWhiteSpace(fromNickname) ? "누군가" : fromNickname.Trim();
            RoomCode = roomCode.Trim();
        }

        /// <summary>Names this invite to the view, which hands it back on a press.</summary>
        public string Id { get; }

        public string FromPlayerId { get; }

        /// <summary>The name on the card. Never empty, so the line always reads.</summary>
        public string FromNickname { get; }

        public string RoomCode { get; }
    }

    /// <summary>
    /// The room invitations waiting for an answer, and which of them are on screen.
    /// </summary>
    /// <remarks>
    /// Pure, so the rules are tested without a canvas or a clock: the seconds
    /// are handed in by whoever owns the frame.
    /// <para>
    /// The rules follow the server's own model of an invitation. It keeps one
    /// per friend, and a friend asking again restarts its life rather than
    /// adding a second invitation, so a repeat refreshes the card that is
    /// already up instead of stacking beside it.
    /// <para>
    /// A card leaves after <see cref="Seconds"/> whether or not it was
    /// answered. That is the card's own life, not the invitation's, which the
    /// server holds for longer: the corner of the screen is given back, and a
    /// friend still waiting can ask again.
    /// </para>
    /// </para>
    /// </remarks>
    public sealed class RoomInviteInbox
    {
        public const int VisibleLimit = 3;

        /// <summary>
        /// How long a card stays up.
        /// </summary>
        /// <remarks>
        /// Shorter than the three minutes the invitation itself lives, so a card
        /// leaving does not mean the invitation is gone: it means the corner has
        /// been given back to the screen. Long enough to notice and answer,
        /// short enough that three of them do not sit over the menu.
        /// <para>
        /// Never longer than the invitation, which would leave a button that
        /// cannot work.
        /// </para>
        /// </remarks>
        public const float Seconds = 30f;

        private sealed class Waiting
        {
            public RoomInvite Invite;
            public float SecondsLeft;
        }

        private readonly List<Waiting> pending = new List<Waiting>();
        private readonly List<RoomInvite> visible = new List<RoomInvite>(VisibleLimit);
        private int nextId;

        /// <summary>Raised after every change to <see cref="Visible"/>.</summary>
        public event Action Changed;

        /// <summary>
        /// The cards on screen, oldest first, never more than <see cref="VisibleLimit"/>.
        /// </summary>
        public IReadOnlyList<RoomInvite> Visible => visible;

        /// <summary>Everything waiting, on screen or not.</summary>
        public int PendingCount => pending.Count;

        /// <summary>
        /// Takes an invitation from a friend. One from a friend who already has
        /// a card refreshes that card where it stands; otherwise the newest
        /// arrival pushes out the oldest once the screen is full.
        /// </summary>
        /// <remarks>
        /// The oldest goes because it is the one closest to expiring anyway, and
        /// a friend who asked a moment ago is the one still waiting by their
        /// screen.
        /// </remarks>
        public RoomInvite Receive(string fromPlayerId, string fromNickname, string roomCode)
        {
            for (var index = 0; index < pending.Count; index++)
            {
                if (!string.Equals(pending[index].Invite.FromPlayerId, fromPlayerId, StringComparison.Ordinal))
                {
                    continue;
                }

                // The same invitation, asked again. It keeps its id so a press
                // already on its way still lands, and its place so the column
                // does not reshuffle under the pointer.
                var refreshed = new RoomInvite(
                    pending[index].Invite.Id, fromPlayerId, fromNickname, roomCode);
                pending[index].Invite = refreshed;
                pending[index].SecondsLeft = Seconds;
                Refresh();
                return refreshed;
            }

            var invite = new RoomInvite(
                (++nextId).ToString(), fromPlayerId, fromNickname, roomCode);
            pending.Add(new Waiting { Invite = invite, SecondsLeft = Seconds });

            while (pending.Count > VisibleLimit)
            {
                pending.RemoveAt(0);
            }

            Refresh();
            return invite;
        }

        /// <summary>
        /// Lets the given seconds pass, dropping whatever ran out.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || pending.Count == 0)
            {
                return;
            }

            var dropped = false;
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                pending[index].SecondsLeft -= deltaSeconds;
                if (pending[index].SecondsLeft <= 0f)
                {
                    pending.RemoveAt(index);
                    dropped = true;
                }
            }

            if (dropped)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Removes the invite the player answered, whichever way, and hands it
        /// back so the caller can act on it. False when it is no longer here,
        /// which is a second press on a card already leaving.
        /// </summary>
        public bool Take(string id, out RoomInvite invite)
        {
            for (var index = 0; index < pending.Count; index++)
            {
                if (pending[index].Invite.Id == id)
                {
                    invite = pending[index].Invite;
                    pending.RemoveAt(index);
                    Refresh();
                    return true;
                }
            }

            invite = default;
            return false;
        }

        public void Clear()
        {
            if (pending.Count == 0)
            {
                return;
            }

            pending.Clear();
            Refresh();
        }

        private void Refresh()
        {
            visible.Clear();
            for (var index = 0; index < pending.Count && index < VisibleLimit; index++)
            {
                visible.Add(pending[index].Invite);
            }

            Changed?.Invoke();
        }
    }
}
