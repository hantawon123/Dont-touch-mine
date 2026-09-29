namespace Game.Backend
{
    /// <summary>
    /// Puts one frame on the notification socket, if there is one.
    /// </summary>
    /// <remarks>
    /// The only thing this client says on that socket is where it is, and the
    /// gateway that reports presence needs a way to say it without holding the
    /// whole stream. This is that way.
    /// <para>
    /// Synchronous and boolean on purpose. The server acknowledges nothing on
    /// this channel, so "it went out" is all anyone could ever learn; a task to
    /// await would promise more than the wire delivers. The false branch is the
    /// caller's cue to use REST instead.
    /// </para>
    /// </remarks>
    public interface INotificationFrameSender
    {
        /// <summary>
        /// True when the frame was handed to a connected socket. False when there
        /// is no connection right now, in which case nothing was sent.
        /// </summary>
        bool TrySend(string json);
    }
}
