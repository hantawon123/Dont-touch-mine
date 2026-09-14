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
    public sealed class BackendSignIn : IAsyncStartable, IAccountReady
    {
        private readonly IAccountGateway accounts;
        private readonly PlayerProfile profile;
        private readonly IPhotonCredentialStore credentials;

        private readonly UniTaskCompletionSource<bool> signedIn =
            new UniTaskCompletionSource<bool>();

        public BackendSignIn(
            IAccountGateway accounts,
            PlayerProfile profile,
            IPhotonCredentialStore credentials = null)
        {
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

            // Optional so the tests that only care about signing in do not all
            // have to hand one over. Null means the fallback below is skipped,
            // which is the behaviour those tests already expect.
            this.credentials = credentials;
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

        /// <summary>
        /// The account this machine signed in as, or null when it could not.
        /// </summary>
        /// <remarks>
        /// Kept so the screens that open right afterwards can draw what the
        /// account already said — whether the name has been settled, whether
        /// this player turns up in searches — instead of each asking the server
        /// for the answer sign-in has already been given. Read it after
        /// awaiting <see cref="Ready"/>; before that it is null because nothing
        /// has answered yet, not because there is no account.
        /// </remarks>
        public AccountSnapshot? Account { get; private set; }

        /// <summary>
        /// Replaces <see cref="Account"/> with what the server just said about
        /// the account, after a rename, a search-setting change or an
        /// appearance save.
        /// </summary>
        /// <remarks>
        /// Without this the snapshot stays what sign-in heard. The Home scene's
        /// bridges are built again every time Home loads — after a room folds,
        /// for one — and each of them draws <see cref="Account"/> first, so a
        /// rename done on an earlier visit was put back to the old name the
        /// next time Home opened. The server has already stored the new value;
        /// this only keeps the client's copy of it current.
        /// <para>
        /// Ignored before sign-in has produced an account. Every caller awaits
        /// <see cref="Ready"/> before it can reach the server, so there is
        /// nothing to adopt in that case, and a null here still means "sign-in
        /// failed" rather than "someone renamed before signing in".
        /// </para>
        /// </remarks>
        public void Adopt(AccountSnapshot account)
        {
            if (!Account.HasValue)
            {
                return;
            }

            Account = account;
        }

        /// <summary>
        /// Why sign-in did not produce an account, or
        /// <see cref="BackendFailure.None"/> when it did.
        /// </summary>
        /// <remarks>
        /// Kept because the reasons are not interchangeable. Offline and
        /// timeout are worth retrying and the player can carry on meanwhile;
        /// <see cref="BackendFailure.Suspended"/> is neither, and the home
        /// screen has to say so rather than leave every button failing in
        /// silence. Before this the reason only reached a log line.
        /// <para>
        /// Read it after awaiting <see cref="Ready"/>. Before that it is
        /// None because nothing has answered yet, not because it went well.
        /// </para>
        /// </remarks>
        public BackendFailure Failure { get; private set; }

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
                    Failure = result.Failure;
                    Debug.LogWarning($"[Backend] Could not sign in: {result.Failure}.");
                    UseRememberedCredentials(result.Failure);
                    return;
                }

                Account = result.Value;
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
        /// <summary>
        /// Falls back to the pair saved by an earlier launch so Photon still
        /// has something to authenticate with (S15P21D205-925).
        /// </summary>
        /// <remarks>
        /// Without this, opening the game while the backend is down leaves no
        /// token, Photon is connected to anonymously, and the dashboard turns
        /// that away - so a server restart would stop people who were never
        /// suspended from playing at all.
        /// <para>
        /// <b>Not done for every failure.</b> Suspended and AccountNotFound are
        /// answers, not outages: the server was reached and said no. Reusing an
        /// old token there would try the same rejected account again, and for
        /// AccountNotFound it would name an account that no longer exists, so
        /// the pair is dropped instead.
        /// </para>
        /// <para>
        /// Only the id and token are restored. <see cref="Account"/> stays null
        /// and <see cref="Ready"/> still answers false, so the friend panel and
        /// the heartbeat skip their work exactly as before - this changes what
        /// Photon is told, not whether the backend is considered reachable.
        /// </para>
        /// </remarks>
        private void UseRememberedCredentials(BackendFailure failure)
        {
            if (credentials == null)
            {
                return;
            }

            if (failure == BackendFailure.Suspended || failure == BackendFailure.AccountNotFound)
            {
                credentials.Clear();
                return;
            }

            if (!credentials.TryLoad(out var userId, out var photonToken))
            {
                return;
            }

            profile.AdoptUserId(userId, photonToken);
            Debug.Log("[Backend] Signing in failed; using the credentials saved by an earlier launch.");
        }

        private void AdoptServerNickname(AccountSnapshot account)
        {
            // The account id rides with the nickname into every room this
            // player joins, so the host can say whose actions it reports. Set
            // here, once, from the same answer that settles the name.
            profile.AdoptUserId(account.UserId, account.PhotonToken);
            credentials?.Save(account.UserId, account.PhotonToken);

            // Mirrored first, and whether the name itself changed or not: a
            // player who renamed on another machine comes back with the same
            // name and a chance that is already spent.
            profile.MarkNicknameSet(account.NicknameSet);

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
