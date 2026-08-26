using System;
using System.Collections.Generic;
using Game.Core.Rooms;

namespace Game.Client.Rooms
{
    public interface IRoomBrowserView
    {
        event Action<string> SearchTextChanged;
        event Action RefreshRequested;
        event Action RoomCodeSearchRequested;
        event Action CreateRoomRequested;
        event Action BackRequested;
        event Action<string> RoomSelected;

        void SetRooms(IReadOnlyList<RoomSummary> rooms);
    }
}
