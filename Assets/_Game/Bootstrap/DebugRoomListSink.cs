using System.Collections.Generic;
using System.Text;
using Game.Core.Ports;
using Game.Core.Rooms;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Prints the room list to the console.
    /// </summary>
    /// <remarks>
    /// Temporary stand-in so matchmaking can be verified before any lobby UI
    /// exists. The client's reactive room store replaces this later; nothing
    /// else changes, because both sides only know <see cref="IRoomListSink"/>.
    /// </remarks>
    public sealed class DebugRoomListSink : IRoomListSink
    {
        private readonly StringBuilder _builder = new StringBuilder();

        public void SetRooms(IReadOnlyList<RoomSummary> rooms)
        {
            _builder.Clear();
            _builder.Append("[Rooms] ").Append(rooms.Count).Append(" room(s)");

            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                _builder
                    .AppendLine()
                    .Append("  - ").Append(room.DisplayName)
                    .Append("  ").Append(room.PlayerCount).Append('/').Append(room.MaxPlayers)
                    .Append(room.IsLocked ? "  [locked]" : string.Empty)
                    .Append(room.CanEnter ? string.Empty : "  [cannot enter]");

                if (!string.IsNullOrEmpty(room.MapId))
                {
                    _builder.Append("  map=").Append(room.MapId);
                }
            }

            Debug.Log(_builder.ToString());
        }
    }
}
