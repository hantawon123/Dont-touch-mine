using System;
using System.Collections.Generic;
using Game.Core.Home;

namespace Game.Client.Home
{
    public interface IHomeMenuView
    {
        event Action<HomeMenuAction> ActionClicked;

        event Action FriendListDismissed;

        event Action ProfileSettingsDismissed;

        event Action<string> NicknameChangeRequested;

        event Action<string> NicknameEdited;

        event Action FriendSearchOpened;

        event Action FriendSearchClosed;

        event Action<string> FriendSearchRequested;

        event Action<string> FriendRequestClicked;

        event Action<string> FriendRequestAccepted;

        event Action<string> FriendRequestDeclined;

        /// <summary>A request this player sent, taken back.</summary>
        event Action<string> FriendRequestCancelled;

        /// <summary>The friend list, asked for again.</summary>
        event Action FriendListRefreshRequested;

        /// <summary>
        /// A friendship, ended. Raised on the first press: either player can ask
        /// again afterwards, so there is nothing here to confirm.
        /// </summary>
        event Action<string> FriendRemoved;

        /// <summary>
        /// A friend, blocked. Raised only after the player confirms, because
        /// blocking also ends the friendship.
        /// </summary>
        event Action<string> FriendBlocked;

        void SetNickname(string nickname);

        void SetProfileSettingsVisible(bool visible);

        void SetNicknameAppliedFeedbackVisible(bool visible);

        /// <summary>
        /// Says why a rename was refused. An empty message clears it.
        /// </summary>
        void SetNicknameError(string message);

        void SetFriendListVisible(bool visible);

        void SetFriends(
            IReadOnlyList<FriendSummary> onlineFriends,
            IReadOnlyList<FriendSummary> offlineFriends);

        void SetFriendSearchVisible(bool visible);

        void SetFriendSearchResults(IReadOnlyList<FriendSearchHit> results);

        /// <summary>
        /// Shows the requests waiting for this player to answer. An empty list
        /// hides the section rather than leaving an empty heading behind.
        /// </summary>
        void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests);

        /// <summary>
        /// Shows the requests this player is waiting on an answer to. An empty
        /// list hides the section.
        /// </summary>
        void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests);
    }
}
