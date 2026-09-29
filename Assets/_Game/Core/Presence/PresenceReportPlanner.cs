using System;
using Game.Core.Flow;
using Game.Core.Ports;

namespace Game.Core.Presence
{
    /// <summary>
    /// One thing this player could tell the server about where they are.
    /// </summary>
    /// <remarks>
    /// A value, so two of them compare by what they say. "Same room, now a
    /// match" and "same room, still the lobby" are different reports even though
    /// the room is the same, which is the whole reason the kind is in here.
    /// </remarks>
    public readonly struct PresenceReport : IEquatable<PresenceReport>
    {
        /// <summary>Not in any room. What the server reads as ONLINE.</summary>
        public static readonly PresenceReport OutOfRoom = default;

        public PresenceReport(string sessionId, RoomSessionKind kind)
        {
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
            Kind = kind;
        }

        /// <summary>The room, or null when out of one.</summary>
        public string SessionId { get; }

        /// <summary>Meaningless when <see cref="SessionId"/> is null.</summary>
        public RoomSessionKind Kind { get; }

        public bool InRoom => SessionId != null;

        public bool Equals(PresenceReport other)
        {
            if (SessionId == null || other.SessionId == null)
            {
                // Out of a room there is no kind to disagree about.
                return SessionId == other.SessionId;
            }

            return string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                && Kind == other.Kind;
        }

        public override bool Equals(object obj) => obj is PresenceReport other && Equals(other);

        public override int GetHashCode()
        {
            return SessionId == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(SessionId) * 31 + (int)Kind;
        }

        public override string ToString() => InRoom ? $"{SessionId} ({Kind})" : "out of room";
    }

    /// <summary>
    /// Decides what to report and whether now is a moment to report it.
    /// </summary>
    /// <remarks>
    /// Pure, so the rules are tested without a clock or a socket. The loop that
    /// owns it reads the world every second and asks; this only remembers what
    /// was last said and whether the link was up last time it was asked.
    /// <para>
    /// There is no timer in these rules. Being online is proven by the
    /// notification link existing, so nothing has to be repeated to stay online.
    /// A report goes out when it would say something new, and once more each
    /// time the link comes back — the server may have been restarted in between
    /// and know only that this client connected, not that it is in a match.
    /// </para>
    /// </remarks>
    public sealed class PresenceReportPlanner
    {
        private PresenceReport? lastReported;

        /// <summary>
        /// What the link was doing the last time this was asked. Null until the
        /// first ask.
        /// </summary>
        /// <remarks>
        /// Nullable on purpose. A plain false would make the very first ask with
        /// the link up look like a comeback, and then a report goes out while
        /// nothing has changed — which is the thirty-second heartbeat this class
        /// exists to remove, back for one tick. "Never observed" is not a
        /// transition.
        /// </remarks>
        private bool? linkWasConnected;

        /// <summary>
        /// What the world currently says this player should report.
        /// </summary>
        /// <remarks>
        /// The room code alone cannot tell the lobby from the match — they are
        /// one Photon room — so the application's own flow state settles it. In
        /// a room during any of the match phases is a match; in a room otherwise
        /// is the lobby, which is where anyone who just joined a room is.
        /// </remarks>
        public static PresenceReport Current(bool hasRoomSession, string roomCode, AppFlowState flow)
        {
            if (!hasRoomSession || string.IsNullOrWhiteSpace(roomCode))
            {
                return PresenceReport.OutOfRoom;
            }

            var kind = flow is AppFlowState.InGame or AppFlowState.Highlight or AppFlowState.Result
                ? RoomSessionKind.Match
                : RoomSessionKind.Lobby;

            return new PresenceReport(roomCode, kind);
        }

        /// <summary>
        /// Whether <paramref name="current"/> should go out now.
        /// </summary>
        /// <param name="linkConnected">Whether the notification link is up at this moment.</param>
        /// <returns>
        /// True the first time, whenever the report differs from the last one that
        /// landed, and once each time the link goes from down to up.
        /// </returns>
        public bool ShouldReport(PresenceReport current, bool linkConnected)
        {
            var linkCameBack = linkConnected && linkWasConnected == false;
            linkWasConnected = linkConnected;

            if (lastReported == null)
            {
                return true;
            }

            if (!lastReported.Value.Equals(current))
            {
                return true;
            }

            return linkCameBack;
        }

        /// <summary>
        /// A report landed. Until it is called, <see cref="ShouldReport"/> keeps
        /// asking for the same report, which is what makes a failed send retry.
        /// </summary>
        public void Reported(PresenceReport report)
        {
            lastReported = report;
        }

        /// <summary>Whether anything has ever landed. Decides if there is a presence to withdraw on quit.</summary>
        public bool HasReported => lastReported != null;
    }
}
