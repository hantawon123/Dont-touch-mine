using System;
using System.Collections.Generic;
using Game.Core.Home;
using Game.Core.Lobby;

namespace Game.Client.Lobby
{
    public interface ILobbyPlayerListView
    {
        event Action<string, string> KickClicked;
        event Action<string, string> InviteClicked;
        event Action<string, string> ReportClicked;

        void SetParticipants(
            IReadOnlyList<LobbyParticipant> participants,
            bool localIsHost,
            string localPlayerId,
            bool namesReady = true);

        void SetFriends(IReadOnlyList<FriendSummary> friends);
    }
}
