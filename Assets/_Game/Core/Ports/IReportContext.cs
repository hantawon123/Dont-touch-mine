namespace Game.Core.Ports
{
    /// <summary>
    /// Which match a report made right now would be about.
    /// </summary>
    /// <remarks>
    /// The server takes one report per reporter, per target, per match
    /// (S15P21D205-1017). It cannot know which match a report belongs to —
    /// reports come in from the room screen, after the fact — so the client
    /// says, with a short key the server compares and does not interpret.
    /// <para>
    /// The key only has to be consistent for the one reporter: it is their
    /// own earlier reports it is matched against. Two players in the same
    /// room may hold different keys for the same match and nothing is lost.
    /// </para>
    /// </remarks>
    public interface IReportContext
    {
        /// <summary>
        /// The key for a report made now, or null when there is no room to be
        /// in — the gateway then leaves the field out and the server falls
        /// back to a time window.
        /// </summary>
        string CurrentKey { get; }
    }
}
