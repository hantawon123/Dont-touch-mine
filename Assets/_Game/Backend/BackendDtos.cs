using System;

namespace Game.Backend
{
    // The wire shapes, one per server record.
    //
    // Public fields and no validation because JsonUtility writes fields
    // directly and cannot use constructors or properties. They stay internal so
    // nothing outside this assembly holds a type shaped by the wire; the
    // gateways translate them into Game.Core types at the boundary.
    //
    // Every enum on the wire is typed as a string here. JsonUtility maps enums
    // by their numeric value, so a field declared as an enum would silently read
    // every "ONLINE" and "IN_GAME" as whichever member happens to be zero.

    [Serializable]
    internal sealed class ErrorDto
    {
        public string code;
        public string message;
    }

    [Serializable]
    internal sealed class IssueAccountRequestDto
    {
        public string deviceId;
    }

    [Serializable]
    internal sealed class AccountResponseDto
    {
        public string userId;
        public string nickname;
        public bool nicknameSet;
        public string createdAt;
    }

    [Serializable]
    internal sealed class UpdateNicknameRequestDto
    {
        public string nickname;
    }

    [Serializable]
    internal sealed class UserSummaryDto
    {
        public string userId;
        public string nickname;
    }

    [Serializable]
    internal sealed class UserSearchResponseDto
    {
        public UserSummaryDto[] users;
    }

    [Serializable]
    internal sealed class FriendSummaryDto
    {
        public string userId;
        public string nickname;

        /// <summary>OFFLINE, ONLINE or IN_GAME.</summary>
        public string presence;
    }

    [Serializable]
    internal sealed class FriendListResponseDto
    {
        public FriendSummaryDto[] friends;
    }

    [Serializable]
    internal sealed class FriendRequestSummaryDto
    {
        public string userId;
        public string nickname;

        /// <summary>yyyyMMddHHmmss, UTC.</summary>
        public string requestedAt;
    }

    [Serializable]
    internal sealed class FriendRequestListResponseDto
    {
        public FriendRequestSummaryDto[] requests;
    }

    [Serializable]
    internal sealed class SendFriendRequestRequestDto
    {
        public string userId;
    }

    [Serializable]
    internal sealed class SendFriendRequestResponseDto
    {
        /// <summary>PENDING, or ACCEPTED when this call made them friends.</summary>
        public string status;
    }

    [Serializable]
    internal sealed class SendInviteRequestDto
    {
        public string userId;
        public string roomCode;
    }

    [Serializable]
    internal sealed class InviteSummaryDto
    {
        public string userId;
        public string nickname;
        public string roomCode;

        /// <summary>yyyyMMddHHmmss, UTC.</summary>
        public string invitedAt;
    }

    [Serializable]
    internal sealed class InviteListResponseDto
    {
        public InviteSummaryDto[] invites;
    }

    [Serializable]
    internal sealed class UpdatePresenceRequestDto
    {
        public string sessionId;
    }

    /// <summary>
    /// A body with no fields, serialising to <c>{}</c>.
    /// </summary>
    /// <remarks>
    /// Needed for the online heartbeat. The server reads a null sessionId as
    /// online and any other value as in-game, but JsonUtility writes a null
    /// string as "", which the server would read as being in a game with no
    /// room. Omitting the field is the only way to say null.
    /// </remarks>
    [Serializable]
    internal sealed class EmptyBodyDto
    {
    }
}
