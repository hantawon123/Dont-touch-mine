using System.Collections.Generic;
using System.Text;
using Game.Core.Ports;
using Game.Core.Rooms;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Prints the room list to the console and keeps the last one.
    /// </summary>
    /// <remarks>
    /// Temporary stand-in so matchmaking can be exercised before any lobby UI
    /// exists. The client's reactive room store replaces this later; nothing
    /// else changes, because both sides only know <see cref="IRoomListSink"/>.
    /// </remarks>
    public sealed class DebugRoomListSink : IRoomListSink
    {
        private readonly List<RoomSummary> _rooms = new List<RoomSummary>();
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>Last list received. Empty until matchmaking reports one.</summary>
        public IReadOnlyList<RoomSummary> Rooms => _rooms;

        public void SetRooms(IReadOnlyList<RoomSummary> rooms)
        {
            _rooms.Clear();
            _rooms.AddRange(rooms);

            _builder.Clear();
            _builder.Append("[Rooms] ").Append(_rooms.Count).Append(" room(s)");

            for (var i = 0; i < _rooms.Count; i++)
            {
                var room = _rooms[i];
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
