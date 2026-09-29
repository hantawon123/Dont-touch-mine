using System.Collections.Generic;
using Game.Core.Rooms;

namespace Game.Core.Ports
{
    /// <summary>
    /// Receives who is in the room. Kept apart from
    /// <see cref="IRoomSessionSink"/> because that one reports what happened to
    /// the room, while this one describes who is in it.
    /// </summary>
    public interface IRoomParticipantSink
    {
        /// <summary>
        /// Replaces the whole list. Arrivals and departures are not reported one
        /// by one: the list is rebuilt from what the network currently holds, so
        /// a peer that missed an event still ends up with the right answer.
        /// </summary>
        void SetParticipants(IReadOnlyList<RoomParticipant> participants);

        /// <summary>
        /// Says which participant is the person at this screen, so presentation
        /// can pick themselves out of the list. Null once the room is left.
        /// </summary>
        void SetLocalPlayer(string playerId);
    }
}
