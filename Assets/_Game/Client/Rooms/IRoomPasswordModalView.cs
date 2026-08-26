using System;
using Game.Core.Rooms;

namespace Game.Client.Rooms
{
    public interface IRoomPasswordModalView
    {
        event Action CloseRequested;
        event Action<string> SubmitRequested;

        bool IsOpen { get; }

        void Open(string roomTitle);
        void Close();
        void SetBusy(bool isBusy);
        void ShowFailure(RoomEntryFailure failure);
    }
}
