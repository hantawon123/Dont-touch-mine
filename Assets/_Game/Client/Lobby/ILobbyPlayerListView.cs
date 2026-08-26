using System;
using System.Collections.Generic;
using Game.Core.Lobby;

namespace Game.Client.Lobby
{
    public interface ILobbyPlayerListView
    {
        event Action<string, string> KickClicked;
        event Action<string, string> TransferClicked;

        void SetParticipants(
            IReadOnlyList<LobbyParticipant> participants,
            bool localIsHost,
            string localPlayerId);
    }
}
