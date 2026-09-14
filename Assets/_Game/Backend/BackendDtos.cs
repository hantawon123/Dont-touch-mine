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
        public bool searchable;
        public string createdAt;

        /// <summary>
        /// Whether <see cref="appearance"/> means anything. JsonUtility turns
        /// a null object into an object of empty strings, so the flag is the
        /// only way to tell "never chosen" from "chosen".
        /// </summary>
        public bool appearanceSet;

        public AppearanceDto appearance;

        /// <summary>
        /// Proves to Photon that this client really is <see cref="userId"/>.
        /// </summary>
        /// <remarks>
        /// Empty when the server has no signing secret configured, which also
        /// means Photon custom authentication is off and there is nothing to
        /// carry. Sent alongside the id on every Photon connection; the id on
        /// its own would let a suspended player type someone else's and get in.
        /// </remarks>
        public string photonToken;
    }

    /// <summary>
    /// The four parts, as the server stores them. Field names are the wire
    /// contract; renaming one makes it silently absent.
    /// </summary>
    [Serializable]
    internal sealed class AppearanceDto
    {
        public string bodyColor;
        public string hood;
        public string shoes;
        public string face;
    }

    [Serializable]
    internal sealed class UpdateAppearanceRequestDto
    {
        public string bodyColor;
        public string hood;
        public string shoes;
        public string face;
    }

    [Serializable]
    internal sealed class UpdateNicknameRequestDto
    {
        public string nickname;
    }

    [Serializable]
    internal sealed class UpdateSearchableRequestDto
    {
        public bool searchable;
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
    internal sealed class SendReportRequestDto
    {
        public string userId;

        /// <summary>ReportReason 의 서버 이름. ReportGateway 가 옮깁니다.</summary>
        public string reason;

        /// <summary>선택. 200자까지. 빈 문자열과 없음을 서버가 같게 봅니다.</summary>
        public string memo;
    }

    [Serializable]
    internal sealed class SendFeedbackRequestDto
    {
        /// <summary>플레이어가 쓴 글. 500자까지. 화면의 입력 상한과 같은 값입니다.</summary>
        public string message;

        /// <summary>
        /// 어느 빌드에서 왔나. 32자까지이고 선택입니다.
        /// </summary>
        /// <remarks>
        /// JsonUtility 가 null 을 "" 로 쓰는 것이 여기서는 문제가 되지 않습니다. 서버가
        /// 빈 문자열과 없음을 같게 보므로(client-guide 12절) 값이 없으면 저장에서 빠집니다.
        /// </remarks>
        public string buildVer;

        /// <summary>실행 환경. 16자까지이고 선택입니다.</summary>
        public string platform;
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

        /// <summary>LOBBY or MATCH. The server reads a missing value as MATCH.</summary>
        public string sessionKind;
    }

    /// <summary>
    /// The room report as a frame on the notification socket. Same two fields as
    /// the REST body, plus the type that tells the server what the frame is.
    /// </summary>
    [Serializable]
    internal sealed class PresenceFrameDto
    {
        public string type;
        public string sessionId;
        public string sessionKind;
    }

    /// <summary>
    /// The out-of-room report as a frame. Only the type, for the same reason the
    /// REST body is <see cref="EmptyBodyDto"/>: a sessionId field that is present
    /// but empty would be read as a room.
    /// </summary>
    [Serializable]
    internal sealed class PresenceOutOfRoomFrameDto
    {
        public string type;
    }

    /// <summary>
    /// The first frame on the notification socket. Says who this client is.
    /// </summary>
    /// <remarks>
    /// A frame rather than a header because a browser's WebSocket cannot set
    /// headers, and a query string would put the id in nginx's access log.
    /// </remarks>
    [Serializable]
    internal sealed class HelloFrameDto
    {
        public string type;
        public string userId;

        /// <summary>
        /// The same signature that rides in X-Account-Token. Empty when the
        /// server issued none, which the server reads as absent.
        /// </summary>
        public string token;
    }

    /// <summary>
    /// Every frame the server sends, including <c>HELLO_ACK</c>, which carries
    /// only <see cref="type"/>. The rest are null or empty for it.
    /// </summary>
    /// <remarks>
    /// <see cref="roomCode"/> is only meaningful for a room invite. JsonUtility
    /// reads a JSON null string as either null or empty depending on the field's
    /// starting state, so the Core type treats both as absent.
    /// </remarks>
    [Serializable]
    internal sealed class NotificationFrameDto
    {
        public string type;

        /// <summary>yyyyMMddHHmmss, UTC.</summary>
        public string sentAt;

        public UserSummaryDto from;
        public string roomCode;
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
