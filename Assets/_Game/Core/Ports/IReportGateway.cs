using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;

namespace Game.Core.Ports
{
    /// <summary>
    /// Why someone is being reported.
    /// </summary>
    /// <remarks>
    /// A fixed list rather than free text, so that whoever reviews reports can
    /// sort them. Free text alone means ten people describe the same behaviour
    /// ten ways and nobody can count anything.
    /// <para>
    /// The names must match the server's list exactly — they travel as strings.
    /// The server may add values; it will not quietly change what one means.
    /// </para>
    /// </remarks>
    public enum ReportReason
    {
        /// <summary>Insults, slurs, harassment.</summary>
        Abuse,

        /// <summary>Cheating.</summary>
        Cheating,

        /// <summary>Flooding, advertising.</summary>
        Spam,

        /// <summary>A nickname that should not be on screen.</summary>
        InappropriateName,

        /// <summary>None of the above. The note matters most for this one.</summary>
        Other
    }

    /// <summary>
    /// Reports a player to whoever moderates the game.
    /// </summary>
    /// <remarks>
    /// <b>Reporting does nothing to the person reported.</b> They are not told,
    /// they do not disappear from search, and friend requests between the two
    /// keep working. This is the whole difference from the blocking this
    /// replaced: a block took effect immediately and search, friend requests and
    /// invites all had to consult it; a report is written down and read later by
    /// a person.
    /// <para>
    /// So a screen built on this should say "신고했습니다" and nothing more.
    /// Promising more would be a lie.
    /// </para>
    /// <para>
    /// One report per person per match (S15P21D205-1017). The second attempt
    /// in the same match answers <see cref="BackendFailure.ReportAlreadySent"/>,
    /// which is the one moment a screen may say "이미 신고했습니다". Which
    /// match it is comes from <see cref="IReportContext"/>. There is still no
    /// way to list or undo a report.
    /// </para>
    /// </remarks>
    public interface IReportGateway
    {
        /// <summary>
        /// Reports <paramref name="playerId"/>.
        /// </summary>
        /// <param name="note">
        /// What happened, in the reporter's words. Optional, and at most 500
        /// characters — longer is refused as
        /// <see cref="BackendFailure.InvalidRequest"/>. Null and blank are the
        /// same thing to the server.
        /// </param>
        /// <param name="contextKey">
        /// Which match this is about — <see cref="IReportContext.CurrentKey"/>.
        /// Null or blank is sent as nothing, and the server then allows one
        /// report per person per 24 hours instead. Always pass the real key
        /// from a screen; the fallback is for builds that predate it.
        /// </param>
        /// <remarks>
        /// Friendship is not required. The common case is reporting someone met
        /// in a room, so requiring it would rule out the reason this exists.
        /// <para>
        /// Reporting yourself answers <see cref="BackendFailure.TargetNotFound"/>,
        /// the same as a player who is not there.
        /// </para>
        /// </remarks>
        UniTask<BackendResult> ReportAsync(
            string playerId, ReportReason reason, string note, string contextKey,
            CancellationToken cancellation);
    }
}
