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

        /// <summary>
        /// Whether the screen is waiting on matchmaking. A refresh that gives no
        /// sign of running invites a second one on top of the first.
        /// </summary>
        void SetBusy(bool busy);

        void ShowDisconnection(string message);
    }
}
