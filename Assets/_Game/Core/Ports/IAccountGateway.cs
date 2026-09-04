using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;

namespace Game.Core.Ports
{
    /// <summary>
    /// The account this machine plays as. Implemented by whichever layer talks
    /// to the backend, called by presentation.
    /// </summary>
    /// <remarks>
    /// No method takes a device identifier. The account's credential belongs to
    /// the adapter that stores it, and a port that accepted one would let any
    /// caller hold a credential it has no reason to see.
    /// </remarks>
    public interface IAccountGateway
    {
        /// <summary>
        /// Gets this machine's account, issuing one the first time.
        /// </summary>
        /// <remarks>
        /// Idempotent: calling it on every launch returns the same account
        /// rather than piling up new ones, so a retry after a lost response
        /// cannot split a player into two accounts. Everything else here needs
        /// this to have succeeded first.
        /// </remarks>
        UniTask<BackendResult<AccountSnapshot>> SignInAsync(CancellationToken cancellation);

        /// <summary>Re-reads the account, in case another client renamed it.</summary>
        UniTask<BackendResult<AccountSnapshot>> RefreshAsync(CancellationToken cancellation);

        /// <summary>
        /// Renames this account. Answers <see cref="BackendFailure.NicknameTaken"/>
        /// when someone else holds the name.
        /// </summary>
        /// <remarks>
        /// Nicknames are case sensitive on the server: "player" and "Player" are
        /// two names and both can exist. A client that checked availability
        /// case-insensitively would disagree with the server it is asking.
        /// </remarks>
        UniTask<BackendResult<AccountSnapshot>> RenameAsync(
            string nickname, CancellationToken cancellation);

        /// <summary>
        /// Deletes this account, its friendships, its blocks and its presence.
        /// </summary>
        /// <remarks>
        /// There is no undo and no recovery, so presentation must confirm before
        /// calling. Issuing an account again afterwards produces a new one, not
        /// the old one back.
        /// </remarks>
        UniTask<BackendResult> DeleteAccountAsync(CancellationToken cancellation);
    }
}
