using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;

namespace Game.Core.Ports
{
    /// <summary>
    /// Sends what a player wrote in 설정 → 피드백 보내기 to whoever reads it.
    /// </summary>
    /// <remarks>
    /// <b>Nothing comes back.</b> There is no reply path — no mail, no
    /// notification — so a screen built on this must not promise one. A success
    /// means the server wrote it down and a person will read it later, which is
    /// the same shape as <see cref="IReportGateway"/>.
    /// <para>
    /// There is no way to list or undo what was sent. The server offers neither,
    /// so a screen cannot show "이미 보냈습니다" either; sending the same words
    /// twice simply writes a second record, and that repetition is itself a
    /// signal to whoever reads them.
    /// </para>
    /// <para>
    /// Which build and platform it came from are not parameters. They are filled
    /// in by the implementation, which is the layer that knows those values, so
    /// that no caller can forget them — a report of "튕깁니다" with no build is
    /// something nobody can reproduce.
    /// </para>
    /// </remarks>
    public interface IFeedbackGateway
    {
        /// <summary>
        /// Sends <paramref name="message"/>.
        /// </summary>
        /// <param name="message">
        /// What the player wrote. At most 500 characters — the same limit the
        /// writing box enforces, so a full box is never refused. Longer, empty
        /// or whitespace-only is <see cref="BackendFailure.InvalidRequest"/>.
        /// </param>
        /// <remarks>
        /// Needs an account: this travels with the player's id so that ten
        /// messages from one person read differently from one message from ten.
        /// Before sign-in the call is refused as
        /// <see cref="BackendFailure.NotSignedIn"/> without being sent.
        /// </remarks>
        UniTask<BackendResult> SendAsync(string message, CancellationToken cancellation);
    }
}
