using System;
using Game.Core.Rooms;

namespace Game.Client.Rooms
{
    public interface IRoomCodeModalView
    {
        event Action CloseRequested;

        /// <summary>The field holds a full-length code, ready to be looked up.</summary>
        event Action<string> CodeCompleted;

        /// <summary>The code became short again, so any answer about it is stale.</summary>
        event Action CodeCleared;

        /// <summary>The shortened code was clicked to type a different one.</summary>
        event Action CodeEditRequested;

        /// <summary>Enter was asked for, with the password when one was shown.</summary>
        event Action<string, string> EnterRequested;

        bool IsOpen { get; }

        void Open();
        void Close();
        void SetBusy(bool isBusy);

        /// <summary>Back to typing a code, with nothing said about a room.</summary>
        void ShowCodeEntry();

        /// <summary>The code names a room anyone can walk into.</summary>
        void ShowOpenRoom();

        /// <summary>The code names a locked room, so ask for its password.</summary>
        void ShowLockedRoom();

        void ShowFailure(RoomEntryFailure failure);
    }
}
