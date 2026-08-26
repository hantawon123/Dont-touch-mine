using System;
using System.Collections.Generic;
using Game.Core.Rooms;

namespace Game.Client.Rooms
{
    public interface IRoomCreateModalView
    {
        event Action CloseRequested;
        event Action<RoomCreateRequest> CreateRequested;

        bool IsOpen { get; }

        void Open();
        void Close();
        void SetMapOptions(IReadOnlyList<string> mapIds);
        void SetBusy(bool isBusy);
    }
}
