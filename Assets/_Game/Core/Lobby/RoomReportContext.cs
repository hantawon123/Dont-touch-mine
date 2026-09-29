using System;
using System.Collections.Generic;
using Game.Core.Match;
using Game.Core.Ports;
using R3;

namespace Game.Core.Lobby
{
    /// <summary>
    /// <see cref="IReportContext"/> read off the room: the room code and how
    /// many matches this peer has seen start since it came in.
    /// </summary>
    /// <remarks>
    /// <c>7K2M9P#0</c> is a room nobody has played in yet, <c>7K2M9P#2</c> is
    /// the same room after its second match. The count moves when the
    /// confirmed line-up goes from empty to full — that is <c>MatchStarted</c>
    /// arriving with players — and goes back to zero when the room is left.
    /// <para>
    /// Counted here rather than taken from the host's analytics match id,
    /// because that id exists only on the host: nobody else in the room ever
    /// sees it. The count is something every peer can work out for itself,
    /// and the key only has to agree with this peer's own earlier reports
    /// (S15P21D205-1018).
    /// </para>
    /// <para>
    /// A report made in the room before any match — an inappropriate name,
    /// say — is <c>#0</c>, and is allowed once too.
    /// </para>
    /// </remarks>
    public sealed class RoomReportContext : IReportContext, IDisposable
    {
        private readonly RoomBrowserSystem room;
        private readonly List<IDisposable> subscriptions = new();
        private int matchesSeen;
        private bool lineUpWasEmpty = true;

        public RoomReportContext(RoomBrowserSystem room)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));

            subscriptions.Add(room.MatchParticipants.Subscribe(OnLineUp));
            subscriptions.Add(room.IsInRoom.Subscribe(OnInRoom));
        }

        /// <summary>How many matches this peer has seen start in the current room.</summary>
        public int MatchesSeen => matchesSeen;

        public string CurrentKey
        {
            get
            {
                var code = room.RoomCode.CurrentValue;
                return string.IsNullOrWhiteSpace(code) || !room.IsInRoom.CurrentValue
                    ? null
                    : code + "#" + matchesSeen;
            }
        }

        private void OnLineUp(IReadOnlyList<MatchParticipant> lineUp)
        {
            var empty = lineUp == null || lineUp.Count == 0;

            // Only the edge counts. The line-up is published again on host
            // migration and again, empty, when the room returns to waiting;
            // neither is a new match.
            if (lineUpWasEmpty && !empty)
            {
                matchesSeen++;
            }

            lineUpWasEmpty = empty;
        }

        private void OnInRoom(bool inRoom)
        {
            if (!inRoom)
            {
                matchesSeen = 0;
                lineUpWasEmpty = true;
            }
        }

        public void Dispose()
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }

            subscriptions.Clear();
        }
    }
}
