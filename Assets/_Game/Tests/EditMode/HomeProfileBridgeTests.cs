using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// Checks the path a rename takes to the account and, when it is refused,
    /// back again.
    /// </summary>
    public sealed class HomeProfileBridgeTests
    {
        [Test]
        public async Task ARenameThatWorks_KeepsTheNameTheServerStored()
        {
            var accounts = new FakeAccounts("서버이름");
            var profile = new PlayerProfile("옛이름");
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, profile);

            accounts.Nickname = "새이름";
            view.RaiseNicknameChangeRequested("새이름");
            await UniTask.Yield();

            Assert.That(accounts.Renamed, Is.EqualTo("새이름"));
            Assert.That(profile.Nickname, Is.EqualTo("새이름"));
            Assert.That(view.NicknameError, Is.Empty);
        }

        [Test]
        public async Task ARefusedRename_PutsTheAccountsNameBack()
        {
            var accounts = new FakeAccounts("옛이름");
            var profile = new PlayerProfile("옛이름");
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, profile);

            // What the screen does before this bridge ever runs: the presenter
            // listens for the same event and applies the new name at once, so the
            // profile already holds the name the server is about to refuse.
            profile.TryChangeNickname("남의이름", out _);

            accounts.RenameFailure = BackendFailure.NicknameTaken;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rename refused"));
            view.RaiseNicknameChangeRequested("남의이름");
            await UniTask.Yield();

            // Read back from the account rather than from what this bridge
            // guessed the previous name was. Guessing put the refused name back
            // and called it a rollback.
            Assert.That(profile.Nickname, Is.EqualTo("옛이름"));
            Assert.That(view.Nickname, Is.EqualTo("옛이름"));
            Assert.That(view.NicknameError, Is.EqualTo("이미 사용 중인 이름입니다"));
            Assert.That(view.AppliedFeedbackVisible, Is.False);
        }

        [Test]
        public async Task WhenTheAccountCannotBeReadEither_TheMessageStillShows()
        {
            var accounts = new FakeAccounts("옛이름");
            var profile = new PlayerProfile("옛이름");
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, profile);

            profile.TryChangeNickname("새이름", out _);
            accounts.RenameFailure = BackendFailure.Offline;
            accounts.RefreshFailure = BackendFailure.Offline;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rename refused"));
            view.RaiseNicknameChangeRequested("새이름");
            await UniTask.Yield();

            // Nothing to put back, so nothing is claimed about the account.
            Assert.That(view.NicknameError, Is.EqualTo("서버에 연결할 수 없습니다"));
        }

        [Test]
        public async Task WithoutAnAccount_NothingIsSent()
        {
            var accounts = new FakeAccounts("옛이름") { SignInFails = true };
            var profile = new PlayerProfile("옛이름");
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, profile);

            // The sign-in warning is already expected by the helper that ran it.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rename refused"));
            view.RaiseNicknameChangeRequested("새이름");
            await UniTask.Yield();

            Assert.That(accounts.Renamed, Is.Null);
            Assert.That(view.NicknameError, Is.EqualTo("서버에 연결되어 있지 않습니다"));
        }

        private static async UniTask<HomeProfileBridge> StartAsync(
            FakeView view, FakeAccounts accounts, PlayerProfile profile)
        {
            var signIn = new BackendSignIn(accounts, profile);

            if (accounts.SignInFails)
            {
                LogAssert.Expect(
                    LogType.Warning, new System.Text.RegularExpressions.Regex("Could not sign in"));
            }

            await signIn.StartAsync(CancellationToken.None);

            var bridge = new HomeProfileBridge(view, accounts, profile, signIn);
            bridge.Start();
            return bridge;
        }

        /// <summary>Answers whatever the test set, and records what it was asked.</summary>
        private sealed class FakeAccounts : IAccountGateway
        {
            public FakeAccounts(string nickname)
            {
                Nickname = nickname;
            }

            public string Nickname { get; set; }

            public bool SignInFails { get; set; }

            public BackendFailure RenameFailure { get; set; } = BackendFailure.None;

            public BackendFailure RefreshFailure { get; set; } = BackendFailure.None;

            public string Renamed { get; private set; }

            public UniTask<BackendResult<AccountSnapshot>> SignInAsync(
                CancellationToken cancellation)
            {
                return UniTask.FromResult(
                    SignInFails
                        ? BackendResult<AccountSnapshot>.Failed(BackendFailure.Offline)
                        : BackendResult<AccountSnapshot>.Success(
                            new AccountSnapshot("user-1", Nickname, true)));
            }

            public UniTask<BackendResult<AccountSnapshot>> RefreshAsync(
                CancellationToken cancellation)
            {
                return UniTask.FromResult(
                    RefreshFailure == BackendFailure.None
                        ? BackendResult<AccountSnapshot>.Success(
                            new AccountSnapshot("user-1", Nickname, true))
                        : BackendResult<AccountSnapshot>.Failed(RefreshFailure));
            }

            public UniTask<BackendResult<AccountSnapshot>> RenameAsync(
                string nickname, CancellationToken cancellation)
            {
                Renamed = nickname;

                if (RenameFailure != BackendFailure.None)
                {
                    return UniTask.FromResult(
                        BackendResult<AccountSnapshot>.Failed(RenameFailure));
                }

                Nickname = nickname;
                return UniTask.FromResult(
                    BackendResult<AccountSnapshot>.Success(
                        new AccountSnapshot("user-1", nickname, true)));
            }

            public UniTask<BackendResult> DeleteAccountAsync(CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());
        }

        /// <summary>Only the parts of the screen this bridge touches.</summary>
        private sealed class FakeView : IHomeMenuView
        {
            public string Nickname { get; private set; } = string.Empty;

            public string NicknameError { get; private set; } = string.Empty;

            public bool AppliedFeedbackVisible { get; private set; }

            public event Action<HomeMenuAction> ActionClicked;
            public event Action FriendListDismissed;
            public event Action ProfileSettingsDismissed;
            public event Action<string> NicknameChangeRequested;
            public event Action<string> NicknameEdited;
            public event Action FriendSearchOpened;
            public event Action FriendSearchClosed;
            public event Action<string> FriendSearchRequested;
            public event Action<string> FriendRequestClicked;
            public event Action<string> FriendRequestAccepted;
            public event Action<string> FriendRequestDeclined;
            public event Action<string> FriendRequestCancelled;
            public event Action FriendListRefreshRequested;
            public event Action<string> FriendRemoved;

            public void RaiseNicknameChangeRequested(string nickname)
            {
                NicknameChangeRequested?.Invoke(nickname);
            }

            public void SetNickname(string nickname) => Nickname = nickname;

            public void SetNicknameError(string message) =>
                NicknameError = message ?? string.Empty;

            public void SetNicknameAppliedFeedbackVisible(bool visible) =>
                AppliedFeedbackVisible = visible;

            public void SetProfileSettingsVisible(bool visible) { }

            public void SetFriendListVisible(bool visible) { }

            public void SetFriends(
                IReadOnlyList<FriendSummary> onlineFriends,
                IReadOnlyList<FriendSummary> offlineFriends) { }

            public void SetFriendSearchVisible(bool visible) { }

            public void SetFriendSearchResults(IReadOnlyList<FriendSearchHit> results) { }

            public void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests) { }

            public string FriendActionError { get; private set; } = string.Empty;

            public void SetFriendActionError(string message)
            {
                FriendActionError = message ?? string.Empty;
            }

            public void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests) { }

            /// <remarks>
            /// Declared so the compiler stops warning that nothing raises them.
            /// This bridge listens to one event and the rest are here to satisfy
            /// the interface.
            /// </remarks>
            public void Unused()
            {
                ActionClicked?.Invoke(default);
                FriendListDismissed?.Invoke();
                ProfileSettingsDismissed?.Invoke();
                NicknameEdited?.Invoke(null);
                FriendSearchOpened?.Invoke();
                FriendSearchClosed?.Invoke();
                FriendSearchRequested?.Invoke(null);
                FriendRequestClicked?.Invoke(null);
                FriendRequestAccepted?.Invoke(null);
                FriendRequestDeclined?.Invoke(null);
                FriendRequestCancelled?.Invoke(null);
                FriendListRefreshRequested?.Invoke();
                FriendRemoved?.Invoke(null);
            }
        }
    }
}
