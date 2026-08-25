using Game.Core.Ports;
using Game.Core.Rooms;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Prints room session events to the console and keeps the last values.
    /// </summary>
    /// <remarks>
    /// Temporary stand-in until the waiting room screen exists. The overlay reads
    /// these back so a build can show them without a console.
    /// </remarks>
    public sealed class DebugRoomSessionSink : IRoomSessionSink
    {
        public int PlayerCount { get; private set; }

        public int MaxPlayers { get; private set; }

        /// <summary>Why the last room ended, or null while still in one.</summary>
        public RoomExitReason? LastExit { get; private set; }

        public void PlayerCountChanged(int current, int max)
        {
            PlayerCount = current;
            MaxPlayers = max;
            LastExit = null;

            Debug.Log($"[Session] Players {current}/{max}");
        }

        public void RoomClosed(RoomExitReason reason)
        {
            LastExit = reason;
            PlayerCount = 0;

            Debug.Log($"[Session] Room closed: {reason}");
        }
    }
}
