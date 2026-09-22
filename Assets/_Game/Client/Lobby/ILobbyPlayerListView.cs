using System;
using System.Collections.Generic;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Settings;

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

        /// <summary>
        /// Draws the modal's own words in <paramref name="locale"/>. The names
        /// on the rows belong to the players and are left alone.
        /// </summary>
        void ShowChrome(UiLocale locale);
    }
}
