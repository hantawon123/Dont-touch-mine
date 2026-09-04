using Game.Core.Backend;

namespace Game.Backend
{
    /// <summary>
    /// The server's error codes, translated one for one.
    /// </summary>
    /// <remarks>
    /// Kept in its own file so it can be read beside the server's
    /// GlobalExceptionHandler, which is the only place these strings are
    /// produced. A code that is not in this table means the two have drifted
    /// apart, and answering <see cref="BackendFailure.Unknown"/> says so instead
    /// of guessing.
    /// </remarks>
    internal static class BackendErrorCodes
    {
        public static BackendFailure ToFailure(string code)
        {
            switch (code)
            {
                case "MISSING_HEADER": return BackendFailure.MissingHeader;
                case "INVALID_REQUEST": return BackendFailure.InvalidRequest;

                // Two codes, one meaning for presentation: the player aimed at
                // themselves and the UI should have prevented it.
                case "SELF_FRIEND_REQUEST": return BackendFailure.SelfRequest;
                case "SELF_BLOCK": return BackendFailure.SelfRequest;

                case "ACCOUNT_NOT_FOUND": return BackendFailure.AccountNotFound;
                case "TARGET_NOT_FOUND": return BackendFailure.TargetNotFound;
                case "FRIEND_REQUEST_NOT_FOUND": return BackendFailure.RequestNotFound;
                case "NOT_FRIENDS": return BackendFailure.NotFriends;
                case "NICKNAME_TAKEN": return BackendFailure.NicknameTaken;
                case "ALREADY_FRIENDS": return BackendFailure.AlreadyFriends;
                case "REQUEST_ALREADY_SENT": return BackendFailure.RequestAlreadySent;
                case "CONFLICT": return BackendFailure.Conflict;

                // The server failed to invent a temporary nickname. Nothing the
                // player did, and retrying is the whole response, so it joins the
                // other server faults rather than getting a case of its own.
                case "NICKNAME_GENERATION_FAILED": return BackendFailure.ServerError;

                default: return BackendFailure.Unknown;
            }
        }
    }
}
