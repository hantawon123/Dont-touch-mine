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
    /// Carries a rename from the profile screen to the account it belongs to.
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
        }

        public void Dispose()
        {
            view.NicknameChangeRequested -= OnNicknameChangeRequested;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private void OnNicknameChangeRequested(string nickname)
        {
            RenameAsync(nickname).Forget();
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
                // is the one that decides what the stored name is.
                Show(result.Value.Nickname);
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
                Show(account.Value.Nickname);

                // Shown again: setting the name raises Changed, and the presenter
                // clears the error when it redraws the profile.
                view.SetNicknameError(message);
            }
        }

        private void Show(string nickname)
        {
            profile.TryChangeNickname(nickname, out _);
            view.SetNickname(nickname);
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
