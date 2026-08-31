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
        event Action DisconnectionAcknowledged;

        void SetRooms(IReadOnlyList<RoomSummary> rooms);
        void ShowDisconnection(string message);
    }
}
