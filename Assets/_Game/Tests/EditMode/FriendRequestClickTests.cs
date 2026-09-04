using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Flow;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;

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
            var wiring = new Wiring();

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
        public async Task OnlyOnePlaceMarksTheRow()
        {
            var wiring = new Wiring();
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
        private sealed class Wiring
        {
            private readonly HomeMenuPresenter presenter;

            public Wiring()
            {
                View = new ClickView();
                Search = new FriendSearchSystem();
                Gateway = new RecordingGateway();

                var friends = new FriendListSystem();
                Commands = new FriendUiCommands(Gateway, new NoBlocks(), friends, Search);

                presenter = new HomeMenuPresenter(
                    new PlayerProfile("나"),
                    new HomeMenuSystem(),
                    View,
                    new SilentHost(),
                    new AppFlowSystem(),
                    friends,
                    Search);
                presenter.Start();

                // What HomeFriendBridge does.
                View.FriendRequestClicked += OnClicked;
            }

            public ClickView View { get; }

            public FriendSearchSystem Search { get; }

            public RecordingGateway Gateway { get; }

            public FriendUiCommands Commands { get; }

            public async UniTask SearchAsync(string query)
            {
                Gateway.Found = new[]
                {
                    new FriendSummary("b", "나그네", FriendPresence.Offline)
                };

                await Commands.SearchAsync(query, Array.Empty<string>(), CancellationToken.None);
            }

            /// <remarks>
            /// The gateway answers without ever yielding, so one turn is enough
            /// for the fire-and-forget the bridge starts.
            /// </remarks>
            public async UniTask Settle() => await UniTask.Yield();

            private void OnClicked(string playerId)
            {
                Commands.SendRequestAsync(playerId, CancellationToken.None).Forget();
            }
        }

        private sealed class RecordingGateway : IFriendGateway
        {
            public IReadOnlyList<FriendSummary> Found { get; set; } =
                Array.Empty<FriendSummary>();

            public string SentTo { get; private set; }

            public int SendCount { get; private set; }

            public UniTask<BackendResult<FriendRequestOutcome>> SendRequestAsync(
                string playerId, CancellationToken cancellation)
            {
                SentTo = playerId;
                SendCount++;
                return UniTask.FromResult(
                    BackendResult<FriendRequestOutcome>.Success(FriendRequestOutcome.Sent));
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

        private sealed class NoBlocks : IBlockGateway
        {
            public UniTask<BackendResult> BlockAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            public UniTask<BackendResult> UnblockAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            public UniTask<BackendResult<IReadOnlyList<BlockedPlayer>>> ListBlockedAsync(
                CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<BlockedPlayer>>.Success(
                        Array.Empty<BlockedPlayer>()));
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
            public event Action<string> FriendBlocked;

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
                FriendBlocked?.Invoke(null);
            }
        }
    }
}
