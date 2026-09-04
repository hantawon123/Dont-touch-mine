using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Gets this machine an account when the application starts, and lets
    /// everything that needs one wait for it.
    /// </summary>
    /// <remarks>
    /// Every backend call but this one identifies itself with the account it
    /// issues, so this runs before them and they await <see cref="Ready"/>
    /// rather than each handling "no account yet" on their own.
    /// <para>
    /// Signing in is idempotent for a given device, so running it on every
    /// launch returns the same account rather than piling up new ones.
    /// </para>
    /// </remarks>
    public sealed class BackendSignIn : IAsyncStartable
    {
        private readonly IAccountGateway accounts;
        private readonly PlayerProfile profile;

        private readonly UniTaskCompletionSource<bool> signedIn =
            new UniTaskCompletionSource<bool>();

        public BackendSignIn(IAccountGateway accounts, PlayerProfile profile)
        {
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>
        /// Completes with whether there is an account. Awaiting it more than
        /// once is fine, and awaiting it after it finished returns immediately.
        /// </summary>
        /// <remarks>
        /// False rather than an exception when sign-in fails, because the game
        /// still runs without a backend — the friend panel is empty and Photon
        /// is untouched. Callers skip their work instead of handling a throw.
        /// </remarks>
        public UniTask<bool> Ready => signedIn.Task;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            try
            {
                var result = await accounts.SignInAsync(cancellation);

                if (!result.Ok)
                {
                    // Not retried here. A retry loop at startup would either
                    // delay the first screen or run forever behind it; the
                    // player can reach the friend panel and see it fail, which
                    // is a place a retry belongs.
                    Debug.LogWarning($"[Backend] Could not sign in: {result.Failure}.");
                    return;
                }

                AdoptServerNickname(result.Value);
                signedIn.TrySetResult(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                // Whatever happened above, this answers. An unset source is the
                // one outcome nobody recovers from: the friend panel and the
                // heartbeat both await it, so leaving it unset stops both of
                // them for the rest of the session without saying anything.
                // Already answered true, this does nothing.
                signedIn.TrySetResult(false);
            }
        }

        /// <remarks>
        /// The server's name wins over the one saved on this machine. Friends
        /// see the server's, so a local name that disagreed would show this
        /// player one thing and everyone else another.
        /// <para>
        /// This is the smaller half of S15P21D205-434. The profile is still
        /// loaded from and saved to this machine; only the name it starts with
        /// now comes from the account.
        /// </para>
        /// </remarks>
        private void AdoptServerNickname(AccountSnapshot account)
        {
            if (string.Equals(profile.Nickname, account.Nickname, StringComparison.Ordinal))
            {
                return;
            }

            if (!profile.TryChangeNickname(account.Nickname, out var error))
            {
                Debug.LogWarning($"[Backend] Server nickname was refused locally: {error}.");
            }
        }
    }
}
