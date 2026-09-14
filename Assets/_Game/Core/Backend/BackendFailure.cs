namespace Game.Core.Backend
{
    /// <summary>
    /// Why a backend call did not succeed, in terms the caller can act on.
    /// </summary>
    /// <remarks>
    /// One enum for transport problems and for the server's own error codes,
    /// because a caller has to handle both anyway and splitting them would make
    /// every call site unpack two failures instead of one.
    /// <para>
    /// The values mirror the <c>code</c> field of the server's error body, which
    /// is the field the client guide says to branch on. The human-readable
    /// <c>message</c> beside it is not carried here on purpose: its wording is
    /// allowed to change, so a client that showed it would be quoting a string
    /// nobody promised to keep.
    /// </para>
    /// </remarks>
    public enum BackendFailure
    {
        None = 0,

        /// <summary>The server could not be reached at all.</summary>
        Offline,

        /// <summary>The request was sent but nothing came back in time.</summary>
        Timeout,

        /// <summary>The caller cancelled before an answer arrived.</summary>
        Cancelled,

        /// <summary>
        /// A call that needs <c>X-User-Id</c> was made before this machine had an
        /// account. Nothing was sent.
        /// </summary>
        NotSignedIn,

        /// <summary>
        /// A required header was missing. This is a client bug, not a state the
        /// player can be in.
        /// </summary>
        MissingHeader,

        /// <summary>The value sent does not fit the server's format.</summary>
        InvalidRequest,

        /// <summary>
        /// The user id was sent without the token that proves it is ours, or
        /// with a token that belongs to someone else. Nothing about the
        /// account itself; signing in again brings the right token.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Aimed at yourself — a friend request. Presentation should prevent
        /// this rather than report it.
        /// </summary>
        SelfRequest,

        /// <summary>
        /// The caller's own account is gone. The client has to issue an account
        /// again before anything else will work.
        /// </summary>
        AccountNotFound,

        /// <summary>
        /// The account is suspended. Not retryable: asking for the account
        /// again answers the same, so the only response is to say so and stop.
        /// </summary>
        Suspended,

        /// <summary>The other user could not be found.</summary>
        TargetNotFound,

        /// <summary>No such pending friend request. The list is stale.</summary>
        RequestNotFound,

        /// <summary>Not friends with that user. The list is stale.</summary>
        NotFriends,

        /// <summary>
        /// The friend is in a room — lobby or match — and the server refused
        /// the room invitation. The toast only shows at home, so they would not
        /// have seen it anyway.
        /// </summary>
        /// <remarks>
        /// Presentation should prevent this by reading the friend's presence
        /// first; this is the answer for the moment between that read and the
        /// click, when the friend has just entered a room.
        /// </remarks>
        TargetInGame,

        /// <summary>Someone else already uses that nickname.</summary>
        NicknameTaken,

        /// <summary>Already friends. The list is stale.</summary>
        AlreadyFriends,

        /// <summary>That friend request was already sent.</summary>
        RequestAlreadySent,

        /// <summary>Two requests collided. Trying again usually works.</summary>
        Conflict,

        /// <summary>The server failed. Trying again may work.</summary>
        ServerError,

        /// <summary>
        /// The failure could not be classified — an unrecognised code, or a body
        /// that was not the server's error shape.
        /// </summary>
        /// <remarks>
        /// Deliberately not guessed from the status code alone. A 404 from a
        /// mistyped path and a 404 meaning "that user does not exist" look the
        /// same from here, and answering <see cref="TargetNotFound"/> to the
        /// first would send presentation to tell the player something false.
        /// </remarks>
        Unknown,
    }
}
