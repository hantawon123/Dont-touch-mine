using System.Collections.Generic;
using Game.Core.Rooms;

namespace Game.Core.Ports
{
    /// <summary>
    /// Receives room list updates. Implemented by presentation, called by
    /// whichever layer talks to matchmaking.
    /// </summary>
    /// <remarks>
    /// A sink rather than a returned value because the list is pushed: it
    /// arrives unprompted and is replaced whenever matchmaking reports a change.
    /// </remarks>
    public interface IRoomListSink
    {
        /// <summary>
        /// Replaces the whole list. The collection is only valid for the
        /// duration of the call, so implementations copy what they keep.
        /// </summary>
        void SetRooms(IReadOnlyList<RoomSummary> rooms);
    }
}
