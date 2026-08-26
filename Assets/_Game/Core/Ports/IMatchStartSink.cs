using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Match;

namespace Game.Core.Ports
{
    /// <summary>
    /// Receives the authority's decision about starting a match.
    /// </summary>
    public interface IMatchStartSink
    {
        /// <summary>
        /// The confirmed line-up, in play order. A participant's position in
        /// this list is their <c>playerIndex</c> for the whole match.
        /// </summary>
        /// <remarks>
        /// Called on every peer once the decision replicates, and again with an
        /// empty list when the room is left. Presentation should read the order
        /// from here rather than from seat numbers, which are reused and can
        /// have gaps.
        /// </remarks>
        void MatchStarted(IReadOnlyList<MatchParticipant> participants);

        /// <summary>
        /// Called only on the peer that asked, and only when the authority said
        /// no. The reason is the same one the lobby rules use.
        /// </summary>
        void MatchStartRefused(RoomStartResult reason);
    }
}
