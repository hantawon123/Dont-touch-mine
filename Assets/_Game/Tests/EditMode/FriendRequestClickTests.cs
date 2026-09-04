using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Flow;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// The friend request button, from the click to the server call.
    /// </summary>
    /// <remarks>
    /// The presenter and the command were tested apart and each behaved. Put
    /// together they did not: both answered the same click, the presenter marked
    /// the row waiting first, and the command then saw a row that had already
    /// been sent and dropped the request. The row said 요청 중 and nothing ever
    /// left the machine.
    /// <para>
    /// Nothing here would have caught that, because nothing put the two in one
    /// place. This does.
    /// </para>
    /// </remarks>
    public sealed class FriendRequestClickTests
    {
        [Test]
        public async Task PressingTheButton_SendsTheRequest()
        {
            using var wiring = await Wiring.StartAsync();

            await wiring.SearchAsync("나");
            wiring.View.RaiseFriendRequestClicked("b");
            await wiring.Settle();

            Assert.That(
                wiring.Gateway.SentTo, Is.EqualTo("b"),
                "the click must reach the server");
            Assert.That(
                wiring.Search.Results[0].IsPending, Is.True,
                "and the row must show it is waiting");
        }

        [Test]
        public async Task ARefusedRequest_SaysWhyOnTheScreen()
        {
            using var wiring = await Wiring.StartAsync();
            await wiring.SearchAsync("나");

            wiring.Gateway.Failure = BackendFailure.AlreadyFriends;
            LogAssert.Expect(
                LogType.Warning, new System.Text.RegularExpressions.Regex("friend request failed"));
            wiring.View.RaiseFriendRequestClicked("b");
            await wiring.Settle();

            // The log alone left the player looking at a button that appeared to
            // do nothing.
            Assert.That(wiring.View.FriendActionError, Is.EqualTo("이미 친구입니다"));
        }

        [Test]
        public async Task BeingBlocked_ReadsAsNotFound()
        {
            using var wiring = await Wiring.StartAsync();
            await wiring.SearchAsync("나");

            wiring.Gateway.Failure = BackendFailure.TargetNotFound;
            LogAssert.Expect(
                LogType.Warning, new System.Text.RegularExpressions.Regex("friend request failed"));
            wiring.View.RaiseFriendRequestClicked("b");
            await wiring.Settle();

            // The server refuses to distinguish a blocked target from an account
            // that does not exist. Saying which it is would undo that.
            Assert.That(wiring.View.FriendActionError, Is.EqualTo("그 사용자를 찾을 수 없습니다"));
            Assert.That(wiring.View.FriendActionError, Does.Not.Contain("차단"));
        }

        [Test]
        public async Task ASuccessAfterAFailure_ClearsTheMessage()
        {
            using var wiring = await Wiring.StartAsync();
            await wiring.SearchAsync("나");

            wiring.Gateway.Failure = BackendFailure.Offline;
            LogAssert.Expect(
                LogType.Warning, new System.Text.RegularExpressions.Regex("friend request failed"));
            wiring.View.RaiseFriendRequestClicked("b");
            await wiring.Settle();
            Assert.That(wiring.View.FriendActionError, Is.Not.Empty);

            wiring.Gateway.Failure = BackendFailure.None;
            wiring.View.RaiseFriendRequestClicked("b");
            await wiring.Settle();

            // A message must not outlive the thing it was about.
            Assert.That(wiring.View.FriendActionError, Is.Empty);
        }

        [Test]
        public async Task OnlyOnePlaceMarksTheRow()
        {
            using var wiring = await Wiring.StartAsync();
            await wiring.SearchAsync("나");

            // The presenter is started and listening. If it also answered this
            // click it would mark the row before the command ran, and the
            // command would find nothing to do.
            wiring.View.RaiseFriendRequestClicked("b");
            await wiring.Settle();

            Assert.That(wiring.Gateway.SendCount, Is.EqualTo(1));
        }

        /// <summary>
        /// The home screen as the container builds it: a presenter on the view,
        /// and a bridge carrying the same events to the command.
        /// </summary>
        /// <summary>
        /// The home screen as the container builds it: the real presenter and
        /// the real bridge on one view.
        /// </summary>
        /// <remarks>
        /// Both are the production classes. A stand-in bridge here would be a
        /// second copy of the wiring and the message table, and the test would
        /// pass while the real ones disagreed.
        /// </remarks>
        private sealed class Wiring : IDisposable
        {
            private readonly HomeMenuPresenter presenter;
            private readonly HomeFriendBridge bridge;

            private Wiring(BackendSignIn signIn)
            {
                View = new ClickView();
                Search = new FriendSearchSystem();

                var friends = new FriendListSystem();
                Commands = new FriendUiCommands(Gateway, friends, Search);

                presenter = new HomeMenuPresenter(
                    new PlayerProfile("나"),
                    new HomeMenuSystem(),
                    View,
                    new SilentHost(),
                    new AppFlowSystem(),
                    friends,
                    Search);
                presenter.Start();

                bridge = new HomeFriendBridge(View, Commands, signIn);
                bridge.Start();
            }

            public ClickView View { get; }

            public FriendSearchSystem Search { get; }

            public RecordingGateway Gateway { get; } = new RecordingGateway();

            public FriendUiCommands Commands { get; }

            public static async UniTask<Wiring> StartAsync()
            {
                var signIn = new BackendSignIn(new SignedInAccounts(), new PlayerProfile("나"));
                await signIn.StartAsync(CancellationToken.None);
                return new Wiring(signIn);
            }

            public async UniTask SearchAsync(string query)
            {
                Gateway.Found = new[]
                {
                    new FriendSummary("b", "나그네", FriendPresence.Offline)
                };

                await Commands.SearchAsync(query, Array.Empty<string>(), CancellationToken.None);
            }

            /// <remarks>
            /// The fakes answer without ever yielding, so a couple of turns
            /// carry the bridge's fire-and-forget through to the end.
            /// </remarks>
            public async UniTask Settle()
            {
                await UniTask.Yield();
                await UniTask.Yield();
                await UniTask.Yield();
            }

            public void Dispose()
            {
                bridge.Dispose();
                presenter.Dispose();
            }
        }

        /// <summary>An account gateway that signs in and does nothing else.</summary>
        private sealed class SignedInAccounts : IAccountGateway
        {
            public UniTask<BackendResult<AccountSnapshot>> SignInAsync(
                CancellationToken cancellation) => Account();

            public UniTask<BackendResult<AccountSnapshot>> RefreshAsync(
                CancellationToken cancellation) => Account();

            public UniTask<BackendResult<AccountSnapshot>> RenameAsync(
                string nickname, CancellationToken cancellation) => Account();

            public UniTask<BackendResult> DeleteAccountAsync(CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            private static UniTask<BackendResult<AccountSnapshot>> Account() =>
                UniTask.FromResult(
                    BackendResult<AccountSnapshot>.Success(
                        new AccountSnapshot("me", "나", true)));
        }

        private sealed class RecordingGateway : IFriendGateway
        {
            public IReadOnlyList<FriendSummary> Found { get; set; } =
                Array.Empty<FriendSummary>();

            public string SentTo { get; private set; }

            public int SendCount { get; private set; }

            public BackendFailure Failure { get; set; } = BackendFailure.None;

            public UniTask<BackendResult<FriendRequestOutcome>> SendRequestAsync(
                string playerId, CancellationToken cancellation)
            {
                SentTo = playerId;
                SendCount++;
                return UniTask.FromResult(
                    Failure == BackendFailure.None
                        ? BackendResult<FriendRequestOutcome>.Success(FriendRequestOutcome.Sent)
                        : BackendResult<FriendRequestOutcome>.Failed(Failure));
            }

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> SearchAsync(
                string nickname, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult<IReadOnlyList<FriendSummary>>.Success(Found));

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> ListFriendsAsync(
                CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<FriendSummary>>.Success(
                        Array.Empty<FriendSummary>()));

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
                ListIncomingRequestsAsync(CancellationToken cancellation) => NoRequests();

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
                ListOutgoingRequestsAsync(CancellationToken cancellation) => NoRequests();

            public UniTask<BackendResult> AcceptRequestAsync(
                string playerId, CancellationToken cancellation) => Ok();

            public UniTask<BackendResult> DeclineRequestAsync(
                string playerId, CancellationToken cancellation) => Ok();

            public UniTask<BackendResult> RemoveFriendAsync(
                string playerId, CancellationToken cancellation) => Ok();

            private static UniTask<BackendResult> Ok() =>
                UniTask.FromResult(BackendResult.Success());

            private static UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
                NoRequests() =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<FriendRequestSummary>>.Success(
                        Array.Empty<FriendRequestSummary>()));
        }

        private sealed class SilentHost : IHomeApplicationHost
        {
            public void Quit() { }

            public void OpenHome() { }

            public void OpenRoomBrowser() { }

            public void OpenLobby() { }
        }

        /// <summary>Only the parts this test drives.</summary>
        private sealed class ClickView : IHomeMenuView
        {
            public IReadOnlyList<FriendSearchHit> SearchResults { get; private set; } =
                Array.Empty<FriendSearchHit>();

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

            public void RaiseFriendRequestClicked(string playerId) =>
                FriendRequestClicked?.Invoke(playerId);

            public void SetFriendSearchResults(IReadOnlyList<FriendSearchHit> results) =>
                SearchResults = results;

            public void SetNickname(string nickname) { }

            public void SetNicknameError(string message) { }

            public void SetNicknameAppliedFeedbackVisible(bool visible) { }

            public void SetProfileSettingsVisible(bool visible) { }

            public void SetFriendListVisible(bool visible) { }

            public void SetFriends(
                IReadOnlyList<FriendSummary> onlineFriends,
                IReadOnlyList<FriendSummary> offlineFriends) { }

            public void SetFriendSearchVisible(bool visible) { }

            public void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests) { }

            public string FriendActionError { get; private set; } = string.Empty;

            public void SetFriendActionError(string message)
            {
                FriendActionError = message ?? string.Empty;
            }

            public void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests) { }

            /// <remarks>Declared to satisfy the interface; this test raises one.</remarks>
            public void Unused()
            {
                ActionClicked?.Invoke(default);
                FriendListDismissed?.Invoke();
                ProfileSettingsDismissed?.Invoke();
                NicknameChangeRequested?.Invoke(null);
                NicknameEdited?.Invoke(null);
                FriendSearchOpened?.Invoke();
                FriendSearchClosed?.Invoke();
                FriendSearchRequested?.Invoke(null);
                FriendRequestAccepted?.Invoke(null);
                FriendRequestDeclined?.Invoke(null);
                FriendRequestCancelled?.Invoke(null);
                FriendListRefreshRequested?.Invoke();
                FriendRemoved?.Invoke(null);
            }
        }
    }
}
