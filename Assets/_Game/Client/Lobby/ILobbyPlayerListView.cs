using System.Collections.Generic;
using Game.Core.Lobby;

namespace Game.Client.Lobby
{
    public interface ILobbyPlayerListView
    {
        void SetParticipants(IReadOnlyList<LobbyParticipant> participants);
    }
}
