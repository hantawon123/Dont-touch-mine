using System;
using Game.Core.Ports;

namespace Game.Client.Lobby
{
    public interface ILobbyConfirmView
    {
        event Action Confirmed;
        event Action Cancelled;

        ReportReason SelectedReason { get; }

        string Note { get; }

        void Show(string message, string confirmLabel);
        void Show(string message, string confirmLabel, bool chooseReason);
        void Hide();
    }
}
