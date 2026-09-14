using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries what the profile screen changes to the account it belongs to: a
    /// rename, and whether this player turns up in nickname searches.
    /// </summary>
    /// <remarks>
    /// The screen changes the name locally the moment it is asked, because a
    /// field that only updates after a round trip feels broken. This sends that
    /// change on and puts the old name back when the server refuses it, the same
    /// shape <see cref="HomeFriendBridge"/> uses for a friend request.
    /// <para>
    /// Sign-in already carries the name the other way: the account's nickname
    /// replaces the local one as soon as the server answers. This is the return
    /// path, and between the two the server owns the name.
    /// </para>
    /// </remarks>
    public sealed class HomeProfileBridge : IStartable, IDisposable
    {
        private readonly IHomeMenuView view;
        private readonly IAccountGateway accounts;
        private readonly PlayerProfile profile;
        private readonly BackendSignIn signIn;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        public HomeProfileBridge(
            IHomeMenuView view,
            IAccountGateway accounts,
            PlayerProfile profile,
            BackendSignIn signIn)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        }

        public void Start()
        {
            view.NicknameChangeRequested += OnNicknameChangeRequested;
            view.NicknameSearchAllowedChanged += OnSearchAllowedChanged;
            ShowSignedInAccountAsync().Forget();
        }

        public void Dispose()
        {
            view.NicknameChangeRequested -= OnNicknameChangeRequested;
            view.NicknameSearchAllowedChanged -= OnSearchAllowedChanged;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        /// <summary>
        /// Draws what sign-in already learned, once it has.
        /// </summary>
        /// <remarks>
        /// The panel is built before the server answers and has to show
        /// something meanwhile, so it starts with the toggle off and the name
        /// unsettled. Neither is known to be true — this replaces both with the
        /// account's own answer rather than leaving the guess on screen.
        /// </remarks>
        private async UniTaskVoid ShowSignedInAccountAsync()
        {
            if (!await signIn.Ready || lifetime.IsCancellationRequested)
            {
                return;
            }

            var account = signIn.Account;
            if (account.HasValue)
            {
                Show(account.Value);
            }
        }

        private void OnNicknameChangeRequested(string nickname)
        {
            RenameAsync(nickname).Forget();
        }

        private void OnSearchAllowedChanged(bool searchable)
        {
            SetSearchableAsync(searchable).Forget();
        }

        /// <summary>
        /// Asks the account to turn nickname search on or off.
        /// </summary>
        /// <remarks>
        /// The knob has not moved: the screen raised what was asked for and
        /// left the setting alone. So there is nothing to roll back here — a
        /// refusal only has to say so, and the toggle is already showing the
        /// setting that is still in force.
        /// <para>
        /// Idempotent on the server, so a double press is two calls that land
        /// where the second one asked rather than an error.
        /// </para>
        /// </remarks>
        private async UniTaskVoid SetSearchableAsync(bool searchable)
        {
            if (lifetime.IsCancellationRequested || !await signIn.Ready)
            {
                Refuse("서버에 연결되어 있지 않습니다");
                return;
            }

            if (lifetime.IsCancellationRequested)
            {
                return;
            }

            var result = await accounts.SetSearchableAsync(searchable, lifetime.Token);

            if (result.Ok)
            {
                // From the answer, not from what was asked. The two agree today
                // and the account is the one that decides.
                signIn.Adopt(result.Value);
                view.SetNicknameSearchAllowed(result.Value.Searchable);
                view.SetNicknameSearchAllowedError(string.Empty);
                return;
            }

            if (result.Failure == BackendFailure.Cancelled)
            {
                return;
            }

            Refuse(HomeStyle.Profile.SearchAllowFailedMessage);
            Debug.LogWarning($"[Profile] Search setting refused: {result.Failure}.");
        }

        private void Refuse(string message)
        {
            view.SetNicknameSearchAllowedError(message);
        }

        private async UniTaskVoid RenameAsync(string nickname)
        {
            if (lifetime.IsCancellationRequested || !await signIn.Ready)
            {
                await RevertAsync("서버에 연결되어 있지 않습니다");
                return;
            }

            if (lifetime.IsCancellationRequested)
            {
                return;
            }

            var result = await accounts.RenameAsync(nickname, lifetime.Token);

            if (result.Ok)
            {
                // Taken from the answer rather than assumed. The server trims and
                // is the one that decides what the stored name is — including
                // whether this rename was the one the account is allowed.
                Show(result.Value);
                view.SetNicknameError(string.Empty);
                return;
            }

            if (result.Failure == BackendFailure.Cancelled)
            {
                return;
            }

            await RevertAsync(Explain(result.Failure));
        }

        /// <summary>
        /// Puts the account's real name back on screen after a refused rename.
        /// </summary>
        /// <remarks>
        /// Asked for rather than remembered. The presenter also listens for this
        /// event and applies the new name before this runs, so by the time we get
        /// here the local profile already holds the name the server just refused
        /// — reading it and calling it "the previous name" would put the refused
        /// name back and call that a rollback.
        /// <para>
        /// Subscribing first would fix the order today and break the day someone
        /// reorders two registrations. Asking the account cannot go stale.
        /// </para>
        /// <para>
        /// When even that call fails there is nothing to put back, so the name on
        /// screen is left alone and only the message is shown. Saying something
        /// false about the account is worse than showing a name that has not been
        /// confirmed yet.
        /// </para>
        /// </remarks>
        private async UniTask RevertAsync(string message)
        {
            view.SetNicknameAppliedFeedbackVisible(false);
            view.SetNicknameError(message);
            Debug.LogWarning($"[Profile] Rename refused: {message}");

            if (lifetime.IsCancellationRequested)
            {
                return;
            }

            var account = await accounts.RefreshAsync(lifetime.Token);
            if (account.Ok)
            {
                Show(account.Value);

                // Shown again: setting the name raises Changed, and the presenter
                // clears the error when it redraws the profile.
                view.SetNicknameError(message);
            }
        }

        /// <summary>
        /// Puts the account the server just described on screen, name and
        /// spent-change alike.
        /// </summary>
        /// <remarks>
        /// The two travel together on purpose. The screen changes the name
        /// optimistically but never marks the change spent, so this is the only
        /// place a refused rename can be told apart from one that took — and a
        /// rename that was refused has to leave the one chance intact.
        /// </remarks>
        private void Show(AccountSnapshot account)
        {
            // Kept beside the profile so the next Home load — this bridge is
            // rebuilt with the scene — starts from this answer rather than from
            // what sign-in heard before the rename.
            signIn.Adopt(account);
            profile.TryChangeNickname(account.Nickname, out _);
            profile.MarkNicknameSet(account.NicknameSet);
            view.SetNickname(account.Nickname);
            view.SetNicknameSettled(account.NicknameSet);
            view.SetNicknameSearchAllowed(account.Searchable);
        }

        /// <remarks>
        /// Written here rather than taken from the server's message, which is
        /// allowed to change wording and is not part of the contract.
        /// </remarks>
        private static string Explain(BackendFailure failure)
        {
            switch (failure)
            {
                case BackendFailure.NicknameTaken:
                    return "이미 사용 중인 이름입니다";

                case BackendFailure.InvalidRequest:
                    return "한글, 영문, 숫자로 2~12글자여야 합니다";

                case BackendFailure.AccountNotFound:
                    return "계정을 찾을 수 없습니다";

                case BackendFailure.Offline:
                case BackendFailure.Timeout:
                    return "서버에 연결할 수 없습니다";

                default:
                    return "이름을 바꾸지 못했습니다";
            }
        }
    }
}
