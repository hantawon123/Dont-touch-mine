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

        /// <summary>
        /// Explains an empty list, or hides that explanation when given null.
        /// An empty list on its own says nothing about why.
        /// </summary>
        void SetEmptyMessage(string message);

        /// <summary>
        /// Reports why a room refused the player. Nothing else says so: a failed
        /// entry leaves the screen exactly as it was.
        /// </summary>
        void ShowEntryFailure(RoomEntryFailure failure);

        void ShowDisconnection(string message);
    }
}
