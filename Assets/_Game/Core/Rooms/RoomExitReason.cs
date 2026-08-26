namespace Game.Core.Rooms
{
    /// <summary>
    /// Why a player is no longer in a room. Presentation reacts differently to a
    /// departure the player asked for than to one forced on them.
    /// </summary>
    public enum RoomExitReason
    {
        /// <summary>The player asked to leave. Nothing to explain to them.</summary>
        Left = 0,

        /// <summary>The authority left, so the room no longer exists.</summary>
        HostClosed,

        /// <summary>The connection dropped.</summary>
        Disconnected,

        /// <summary>
        /// The authority removed this player.
        /// </summary>
        /// <remarks>
        /// Not produced yet. A kick and the authority simply leaving arrive as
        /// the same shutdown reason, so telling them apart needs the authority to
        /// say so first, over its own message, before closing the connection.
        /// </remarks>
        Kicked,

        /// <summary>Ended for a reason that does not map to the above.</summary>
        Unknown,
    }
}
