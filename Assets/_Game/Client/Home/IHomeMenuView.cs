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

        void SetNickname(string nickname);

        void SetLevel(int level);

        void SetProfileSettingsVisible(bool visible);

        void SetNicknameAppliedFeedbackVisible(bool visible);

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
    }
}
