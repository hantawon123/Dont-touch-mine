using Game.Core.Rooms;

namespace Game.Core.Ports
{
    /// <summary>
    /// Reports what happens inside the room the player is in. Implemented by
    /// presentation, called by whichever layer holds the session.
    /// </summary>
    /// <remarks>
    /// Only unrequested events belong here. Opening, entering and leaving all
    /// return their own answer through <see cref="IRoomBrowser"/>, so this
    /// carries the things nobody asked for: other players coming and going, and
    /// the room ending underneath them.
    /// <para>
    /// Separate from <see cref="IRoomListSink"/> because the subjects differ.
    /// That one describes other rooms; this one describes the room you are in.
    /// </para>
    /// </remarks>
    public interface IRoomSessionSink
    {
        /// <summary>
        /// Players in the room changed. Also fires on entry, so presentation can
        /// show a count without asking for one.
        /// </summary>
        void PlayerCountChanged(int current, int max);

        /// <summary>
        /// The player is out of the room. Fires once per session, including when
        /// the player asked to leave, so presentation can return to the browser
        /// from a single place.
        /// </summary>
        void RoomClosed(RoomExitReason reason);
    }
}
