using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Players;
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
        public async Task ARenameThatWorks_SurvivesTheBridgeBeingBuiltAgain()
        {
            // Home is loaded again after a room folds, and the scene builds a
            // new bridge each time. The first one drew what sign-in heard, so
            // a rename done on the earlier visit came back as the old name —
            // and overwrote the profile every peer's nameplate reads.
            var accounts = new FakeAccounts("옛이름");
            var profile = new PlayerProfile("옛이름");
            var signIn = new BackendSignIn(accounts, profile);
            await signIn.StartAsync(CancellationToken.None);

            var firstView = new FakeView();
            using (var first = new HomeProfileBridge(firstView, accounts, profile, signIn))
            {
                first.Start();
                await UniTask.Yield();

                accounts.Nickname = "새이름";
                firstView.RaiseNicknameChangeRequested("새이름");
                await UniTask.Yield();
            }

            var secondView = new FakeView();
            using var second = new HomeProfileBridge(secondView, accounts, profile, signIn);
            second.Start();
            await UniTask.Yield();

            Assert.That(profile.Nickname, Is.EqualTo("새이름"));
            Assert.That(secondView.Nickname, Is.EqualTo("새이름"));
            Assert.That(signIn.Account.Value.Nickname, Is.EqualTo("새이름"));
        }

        [Test]
        public async Task TurningSearchOff_SurvivesTheBridgeBeingBuiltAgain()
        {
            // Same road as the rename: the toggle was put back to what sign-in
            // heard the next time Home opened, while the server kept the change.
            var accounts = new FakeAccounts("서버이름") { Searchable = true };
            var profile = new PlayerProfile("서버이름");
            var signIn = new BackendSignIn(accounts, profile);
            await signIn.StartAsync(CancellationToken.None);

            var firstView = new FakeView();
            using (var first = new HomeProfileBridge(firstView, accounts, profile, signIn))
            {
                first.Start();
                await UniTask.Yield();

                firstView.RaiseSearchAllowedChanged(false);
                await UniTask.Yield();
            }

            var secondView = new FakeView();
            using var second = new HomeProfileBridge(secondView, accounts, profile, signIn);
            second.Start();
            await UniTask.Yield();

            Assert.That(secondView.SearchAllowed, Is.False);
            Assert.That(signIn.Account.Value.Searchable, Is.False);
        }

        [Test]
        public async Task ARenameThatWorks_SpendsTheOneChange()
        {
            var accounts = new FakeAccounts("서버이름") { NicknameSet = false };
            var profile = new PlayerProfile("서버이름");
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, profile);
            Assert.That(profile.NicknameSet, Is.False);

            view.RaiseNicknameChangeRequested("새이름");
            await UniTask.Yield();

            Assert.That(profile.NicknameSet, Is.True);
            Assert.That(view.NicknameSettled, Is.True);
        }

        [Test]
        public async Task ARefusedRename_LeavesTheOneChanceIntact()
        {
            var accounts = new FakeAccounts("서버이름") { NicknameSet = false };
            var profile = new PlayerProfile("서버이름");
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, profile);

            // The screen applies the name before the server has seen it, the
            // same as in the test above.
            profile.TryChangeNickname("남의이름", out _);
            accounts.RenameFailure = BackendFailure.NicknameTaken;

            LogAssert.Expect(
                LogType.Warning, new System.Text.RegularExpressions.Regex("Rename refused"));
            view.RaiseNicknameChangeRequested("남의이름");
            await UniTask.Yield();

            // Losing the only rename to a name somebody else already had would
            // leave this player on the server's temporary name for good.
            Assert.That(profile.NicknameSet, Is.False);
            Assert.That(view.NicknameSettled, Is.False);
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

        [Test]
        public async Task TheAccountsSettings_AreOnScreenOnceSignInAnswers()
        {
            var accounts = new FakeAccounts("서버이름")
            {
                NicknameSet = true,
                Searchable = false
            };
            var view = new FakeView();

            using var bridge = await StartAsync(view, accounts, new PlayerProfile("서버이름"));
            await UniTask.Yield();

            // The panel is built before the server answers, so it starts with
            // the toggle off and the name unsettled. Neither is known to be
            // true, and leaving the guess up is how a player ends up believing
            // they can still rename themselves.
            Assert.That(view.SearchAllowed, Is.False);
            Assert.That(view.NicknameSettled, Is.True);
        }

        [Test]
        public async Task TurningSearchOff_TellsTheAccount()
        {
            var accounts = new FakeAccounts("서버이름");
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, new PlayerProfile("서버이름"));
            await UniTask.Yield();
            Assert.That(view.SearchAllowed, Is.True, "계정은 검색 허용으로 시작한다.");

            view.RaiseSearchAllowedChanged(false);
            await UniTask.Yield();

            Assert.That(accounts.Searchable, Is.False);
            Assert.That(view.SearchAllowed, Is.False);
            Assert.That(view.SearchAllowedError, Is.Empty);
        }

        [Test]
        public async Task ARefusedSetting_LeavesTheToggleWhereItWas()
        {
            var accounts = new FakeAccounts("서버이름") { SearchableFailure = BackendFailure.Offline };
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, new PlayerProfile("서버이름"));
            await UniTask.Yield();

            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("Search setting refused"));
            view.RaiseSearchAllowedChanged(false);
            await UniTask.Yield();

            // The screen never moved the knob, so there is nothing to roll back
            // — it is still showing the setting that is actually in force.
            Assert.That(accounts.Searchable, Is.True);
            Assert.That(view.SearchAllowed, Is.True);
            Assert.That(view.SearchAllowedError, Is.Not.Empty);
        }

        [Test]
        public async Task WithoutAnAccount_TheSettingIsNotSent()
        {
            var accounts = new FakeAccounts("서버이름") { SignInFails = true };
            var view = new FakeView();
            using var bridge = await StartAsync(view, accounts, new PlayerProfile("서버이름"));
            await UniTask.Yield();

            view.RaiseSearchAllowedChanged(false);
            await UniTask.Yield();

            Assert.That(accounts.Searchable, Is.True, "보내지 않았다.");
            Assert.That(view.SearchAllowedError, Is.EqualTo("서버에 연결되어 있지 않습니다"));
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

            /// <summary>
            /// Whether this account has spent its one rename. True by default so
            /// the tests that are not about it read as they did.
            /// </summary>
            public bool NicknameSet { get; set; } = true;

            public BackendFailure RenameFailure { get; set; } = BackendFailure.None;

            public BackendFailure RefreshFailure { get; set; } = BackendFailure.None;

            public BackendFailure SearchableFailure { get; set; } = BackendFailure.None;

            public string Renamed { get; private set; }

            /// <summary>Whether this account turns up in nickname searches.</summary>
            public bool Searchable { get; set; } = true;

            public UniTask<BackendResult<AccountSnapshot>> SignInAsync(
                CancellationToken cancellation)
            {
                return UniTask.FromResult(
                    SignInFails
                        ? BackendResult<AccountSnapshot>.Failed(BackendFailure.Offline)
                        : BackendResult<AccountSnapshot>.Success(
                            new AccountSnapshot(
                                "user-1", Nickname, NicknameSet, Searchable)));
            }

            public UniTask<BackendResult<AccountSnapshot>> RefreshAsync(
                CancellationToken cancellation)
            {
                return UniTask.FromResult(
                    RefreshFailure == BackendFailure.None
                        ? BackendResult<AccountSnapshot>.Success(
                            new AccountSnapshot(
                                "user-1", Nickname, NicknameSet, Searchable))
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
                NicknameSet = true;
                return UniTask.FromResult(
                    BackendResult<AccountSnapshot>.Success(
                        new AccountSnapshot("user-1", nickname, true, Searchable)));
            }

            public UniTask<BackendResult<AccountSnapshot>> SetSearchableAsync(
                bool searchable, CancellationToken cancellation)
            {
                if (SearchableFailure != BackendFailure.None)
                {
                    return UniTask.FromResult(
                        BackendResult<AccountSnapshot>.Failed(SearchableFailure));
                }

                Searchable = searchable;
                return UniTask.FromResult(
                    BackendResult<AccountSnapshot>.Success(
                        new AccountSnapshot("user-1", Nickname, NicknameSet, searchable)));
            }

            public UniTask<BackendResult<AccountSnapshot>> SetAppearanceAsync(
                AvatarAppearance appearance, CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<AccountSnapshot>.Success(
                        new AccountSnapshot(
                            "user-1", Nickname, NicknameSet, Searchable, true, appearance)));

            public UniTask<BackendResult<AccountSnapshot>> ClearAppearanceAsync(
                CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<AccountSnapshot>.Success(
                        new AccountSnapshot("user-1", Nickname, NicknameSet, Searchable)));

            public UniTask<BackendResult> DeleteAccountAsync(CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());
        }

        /// <summary>Only the parts of the screen this bridge touches.</summary>
        private sealed class FakeView : IHomeMenuView
        {
            public string Nickname { get; private set; } = string.Empty;

            public string NicknameError { get; private set; } = string.Empty;

            public bool AppliedFeedbackVisible { get; private set; }

            public bool NicknameSettled { get; private set; }

            public bool SearchAllowed { get; private set; }

            public string SearchAllowedError { get; private set; } = string.Empty;

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
            public event Action<string> RoomInviteAccepted;
            public event Action<string> RoomInviteDeclined;
            public event Action ServerSettingsDismissed;
            public event Action<string> RegionSelected;
            public event Action<string, bool, int> RoomCreationRequested;
            public event Action CreateRoomDismissed;

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

            public void SetSuspendedNoticeVisible(bool visible) =>
                SuspendedNoticeVisible = visible;

            public bool SuspendedNoticeVisible { get; private set; }

            public bool FriendListVisible { get; private set; }
            public void SetFriendListVisible(bool visible) { FriendListVisible = visible; }

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

            public void SetNicknameSettled(bool settled) => NicknameSettled = settled;
            public void SetRoomInvites(IReadOnlyList<RoomInvite> invites) { }

            public void SetServerSettingsVisible(bool visible) { }

            public void SetSelectedRegion(string code) { }

            public void SetCreateRoomVisible(bool visible) { }

            public event Action<bool> NicknameSearchAllowedChanged;

            public void RaiseSearchAllowedChanged(bool allowed) =>
                NicknameSearchAllowedChanged?.Invoke(allowed);

            public void SetNicknameSearchAllowed(bool allowed) => SearchAllowed = allowed;

            public void SetNicknameSearchAllowedError(string message) =>
                SearchAllowedError = message ?? string.Empty;

            public void ShowConnectionError(string message) { }

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
                ServerSettingsDismissed?.Invoke();
                RegionSelected?.Invoke(null);
                RoomCreationRequested?.Invoke(null, false, 0);
                CreateRoomDismissed?.Invoke();
            }
        }
    }
}
