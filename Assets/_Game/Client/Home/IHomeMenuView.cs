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

        /// <summary>
        /// The search-allow toggle was pressed, carrying the setting the player
        /// asked for rather than the one showing.
        /// </summary>
        /// <remarks>
        /// The knob has not moved yet. The server owns this value, so whoever
        /// handles this calls <see cref="SetNicknameSearchAllowed"/> with what
        /// the server settled on — which, when the call fails, is the setting
        /// that was already there.
        /// </remarks>
        event Action<bool> NicknameSearchAllowedChanged;

        event Action FriendSearchOpened;

        event Action FriendSearchClosed;

        event Action<string> FriendSearchRequested;

        event Action<string> FriendRequestClicked;

        event Action<string> FriendRequestAccepted;

        event Action<string> FriendRequestDeclined;

        event Action ServerSettingsDismissed;

        /// <summary>The code of the region the player picked.</summary>
        event Action<string> RegionSelected;

        /// <summary>The player filled the room form in and pressed create.</summary>
        event Action<string, bool, int> RoomCreationRequested;

        event Action CreateRoomDismissed;

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
        /// The player took up a room invitation, by the id the card was shown
        /// under.
        /// </summary>
        event Action<string> RoomInviteAccepted;

        /// <summary>The player turned a room invitation down, by its id.</summary>
        event Action<string> RoomInviteDeclined;

        void SetNickname(string nickname);

        void SetProfileAppearance(Game.Core.Players.AvatarAppearance appearance);

        void SetProfileSettingsVisible(bool visible);

        /// <summary>
        /// Shows the notice that this account is suspended, over everything and
        /// without a way out (S15P21D205-924).
        /// </summary>
        /// <remarks>
        /// Only for <c>BackendFailure.Suspended</c>. Offline and timeout are
        /// worth retrying and the player can carry on meanwhile; telling them
        /// they had been suspended because the wifi dropped would be worse than
        /// saying nothing.
        /// </remarks>
        void SetSuspendedNoticeVisible(bool visible);

        void SetNicknameAppliedFeedbackVisible(bool visible);

        /// <summary>
        /// Says why a rename was refused. An empty message clears it.
        /// </summary>
        void SetNicknameError(string message);

        /// <summary>
        /// Puts the search-allow toggle where the account says it is.
        /// </summary>
        void SetNicknameSearchAllowed(bool allowed);

        /// <summary>
        /// Says the toggle did not take. An empty message clears it.
        /// </summary>
        void SetNicknameSearchAllowedError(string message);

        /// <summary>
        /// Reports that something did not connect, over whatever is on screen.
        /// </summary>
        /// <remarks>
        /// For the failures that belong to no panel — making a room, opening
        /// the room browser. A refusal that belongs to a panel is said in that
        /// panel, where the player is looking.
        /// </remarks>
        void ShowConnectionError(string message);

        bool FriendListVisible { get; }

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

        /// <summary>
        /// Says why the last thing the player asked for did not happen. An empty
        /// message clears it.
        /// </summary>
        void SetFriendActionError(string message);

        void SetServerSettingsVisible(bool visible);

        /// <summary>
        /// Marks which region is in use. A code the picker does not list leaves
        /// every row unmarked.
        /// </summary>
        void SetSelectedRegion(string code);

        void SetCreateRoomVisible(bool visible);

        /// <summary>Whether the one nickname change has been spent.</summary>
        void SetNicknameSettled(bool settled);

        /// <summary>
        /// The room invitations to show, oldest first. Never more than the
        /// stack has cards for; the caller holds the rest back.
        /// </summary>
        void SetRoomInvites(IReadOnlyList<RoomInvite> invites);
    }
}
