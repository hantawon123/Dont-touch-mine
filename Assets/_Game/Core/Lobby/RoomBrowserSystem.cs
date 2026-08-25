using System;
using System.Collections.Generic;
using Game.Core.Rooms;

namespace Game.Core.Lobby
{
    public sealed class RoomBrowserSystem
    {
        private List<RoomSummary> rooms = new List<RoomSummary>();

        public IReadOnlyList<RoomSummary> Rooms => rooms;

        public event Action RefreshRequested;
        public event Action<IReadOnlyList<RoomSummary>> RoomsChanged;

        public void RequestRefresh()
        {
            RefreshRequested?.Invoke();
        }

        public void ReplaceRooms(IEnumerable<RoomSummary> refreshedRooms)
        {
            if (refreshedRooms == null)
            {
                throw new ArgumentNullException(nameof(refreshedRooms));
            }

            rooms = new List<RoomSummary>(refreshedRooms);
            RoomsChanged?.Invoke(rooms);
        }

        public bool TryFindByCode(string roomCode, out RoomSummary room)
        {
            if (!string.IsNullOrWhiteSpace(roomCode))
            {
                var normalizedCode = roomCode.Trim();

                for (var index = 0; index < rooms.Count; index++)
                {
                    if (string.Equals(
                            rooms[index].RoomId,
                            normalizedCode,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        room = rooms[index];
                        return true;
                    }
                }
            }

            room = default;
            return false;
        }
    }
}
