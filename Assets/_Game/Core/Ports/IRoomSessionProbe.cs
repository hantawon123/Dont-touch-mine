namespace Game.Core.Ports
{
    /// <summary>
    /// Whether this player is in a Photon room right now, and which one.
    /// </summary>
    /// <remarks>
    /// Two questions the network layer already answers, lifted into a port so
    /// that the thing which reports presence can be tested against a fake
    /// instead of a live Fusion runner. The runner implements it with the
    /// members it already had; nothing about the network changes.
    /// </remarks>
    public interface IRoomSessionProbe
    {
        /// <summary>
        /// True from the moment a room is being joined until it is left,
        /// including while connecting and while a host migration is in flight.
        /// False in a standalone scene and while browsing the room list.
        /// </summary>
        bool HasRoomSession { get; }

        /// <summary>
        /// The room's Photon session name — what identifies it to anyone else —
        /// or null while there is no valid session.
        /// </summary>
        string RoomCode { get; }
    }
}
