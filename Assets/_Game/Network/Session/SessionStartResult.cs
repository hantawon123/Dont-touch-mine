using Fusion;

namespace Game.Network.Session
{
    /// <summary>
    /// Why a session failed to start, in terms the lobby UI can show directly.
    /// </summary>
    public enum SessionFailure
    {
        None = 0,

        /// <summary>No room exists for the entered code.</summary>
        RoomNotFound,

        /// <summary>The room already holds its maximum players.</summary>
        RoomFull,

        /// <summary>A room already uses this code, so a new one must be drawn.</summary>
        CodeTaken,

        /// <summary>The host rejected the connection, typically a wrong password.</summary>
        Rejected,

        /// <summary>Could not reach Photon at all.</summary>
        ConnectionFailed,

        /// <summary>A session is already running on this service.</summary>
        AlreadyRunning,

        /// <summary>
        /// The attempt was called off before it finished, which is what leaving
        /// the screen mid-connect looks like. Not a failure to report: nobody
        /// asked for an answer any more.
        /// </summary>
        Canceled,

        /// <summary>Anything Fusion reported that does not map to the above.</summary>
        Unknown,
    }

    public readonly struct SessionStartResult
    {
        public readonly bool Ok;
        public readonly SessionFailure Failure;

        /// <summary>Fusion's own message, for logs rather than for players.</summary>
        public readonly string Detail;

        private SessionStartResult(bool ok, SessionFailure failure, string detail)
        {
            Ok = ok;
            Failure = failure;
            Detail = detail;
        }

        public static SessionStartResult Success() =>
            new SessionStartResult(true, SessionFailure.None, null);

        public static SessionStartResult Failed(SessionFailure failure, string detail) =>
            new SessionStartResult(false, failure, detail);

        /// <summary>
        /// Translates Fusion's shutdown reason into a failure the UI understands.
        /// </summary>
        public static SessionFailure Classify(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound:
                    return SessionFailure.RoomNotFound;
                case ShutdownReason.GameIsFull:
                    return SessionFailure.RoomFull;
                case ShutdownReason.GameIdAlreadyExists:
                    return SessionFailure.CodeTaken;
                case ShutdownReason.ConnectionRefused:
                    return SessionFailure.Rejected;
                case ShutdownReason.ConnectionTimeout:
                    return SessionFailure.ConnectionFailed;
                case ShutdownReason.AlreadyRunning:
                    return SessionFailure.AlreadyRunning;
                case ShutdownReason.OperationCanceled:
                    return SessionFailure.Canceled;
                default:
                    return SessionFailure.Unknown;
            }
        }
    }
}
