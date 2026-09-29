using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Lobby;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class LobbyFriendRefreshTests
    {
        [Test]
        public async Task Start_LoadsFriendsOnceWhileTheModalIsClosed()
        {
            using var wiring = new Wiring();
            wiring.Refresh.Start();
            await wiring.Settle();

            Assert.That(wiring.Gateway.ListCalls, Is.EqualTo(1));
            wiring.Refresh.Advance(30f);
            await wiring.Settle();
            Assert.That(wiring.Gateway.ListCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task OpenPlayerModal_ReadsImmediatelyThenEveryThreeSeconds()
        {
            using var wiring = new Wiring();
            wiring.Refresh.Start();
            await wiring.Settle();
            wiring.Gateway.ListCalls = 0;

            wiring.Overlay.Show(LobbyShortcutKind.Players);
            wiring.Refresh.Advance(0f);
            await wiring.Settle();
            Assert.That(wiring.Gateway.ListCalls, Is.EqualTo(1));

            wiring.Refresh.Advance(LobbyFriendRefresh.PresenceRefreshInterval - 0.1f);
            await wiring.Settle();
            Assert.That(wiring.Gateway.ListCalls, Is.EqualTo(1));

            wiring.Refresh.Advance(0.1f);
            await wiring.Settle();
            Assert.That(wiring.Gateway.ListCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task ClosingTheModal_StopsPollingUntilItOpensAgain()
        {
            using var wiring = new Wiring();
            wiring.Refresh.Start();
            await wiring.Settle();

            wiring.Overlay.Show(LobbyShortcutKind.Players);
            wiring.Refresh.Advance(0f);
            await wiring.Settle();
            var afterOpen = wiring.Gateway.ListCalls;

            wiring.Overlay.Hide();
            wiring.Refresh.Advance(30f);
            await wiring.Settle();
            Assert.That(wiring.Gateway.ListCalls, Is.EqualTo(afterOpen));

            wiring.Overlay.Show(LobbyShortcutKind.Players);
            wiring.Refresh.Advance(0f);
            await wiring.Settle();
            Assert.That(wiring.Gateway.ListCalls, Is.EqualTo(afterOpen + 1));
        }

        [Test]
        public async Task OpenPlayerModal_ReplacesPresenceFromTheServer()
        {
            using var wiring = new Wiring();
            wiring.Gateway.Friends = new[]
            {
                new FriendSummary("f-1", "친구", FriendPresence.Online),
            };
            wiring.Refresh.Start();
            await wiring.Settle();
            Assert.That(wiring.Friends.OnlineFriends[0].Presence, Is.EqualTo(FriendPresence.Online));

            wiring.Gateway.Friends = new[]
            {
                new FriendSummary("f-1", "친구", FriendPresence.InGame),
            };
            wiring.Overlay.Show(LobbyShortcutKind.Players);
            wiring.Refresh.Advance(0f);
            await wiring.Settle();
            Assert.That(wiring.Friends.OnlineFriends[0].Presence, Is.EqualTo(FriendPresence.InGame));
        }

        private sealed class Wiring : IDisposable
        {
            public Wiring()
            {
                Gateway = new FakeFriendGateway();
                Friends = new FriendListSystem();
                Overlay = new FakeOverlay();
                Refresh = new LobbyFriendRefresh(
                    new FriendUiCommands(Gateway, Friends, new FriendSearchSystem()),
                    Overlay);
            }

            public FakeFriendGateway Gateway { get; }

            public FriendListSystem Friends { get; }

            public FakeOverlay Overlay { get; }

            public LobbyFriendRefresh Refresh { get; }

            public async UniTask Settle()
            {
                await UniTask.Yield();
                await UniTask.Yield();
                await UniTask.Yield();
            }

            public void Dispose() => Refresh.Dispose();
        }

        private sealed class FakeOverlay : ILobbyShortcutOverlay
        {
            public event Action CloseRequested;

            public LobbyShortcutKind OpenKind { get; private set; }

            public bool IsOpen { get; private set; }

            public void Show(LobbyShortcutKind kind)
            {
                OpenKind = kind;
                IsOpen = kind != LobbyShortcutKind.None;
            }

            public void Hide()
            {
                OpenKind = LobbyShortcutKind.None;
                IsOpen = false;
            }

            public void RequestClose()
            {
                Hide();
                CloseRequested?.Invoke();
            }
        }

        private sealed class FakeFriendGateway : IFriendGateway
        {
            public int ListCalls;
            public IReadOnlyList<FriendSummary> Friends { get; set; } =
                Array.Empty<FriendSummary>();

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> ListFriendsAsync(
                CancellationToken cancellation)
            {
                ListCalls++;
                return UniTask.FromResult(
                    BackendResult<IReadOnlyList<FriendSummary>>.Success(Friends));
            }

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> SearchAsync(
                string nickname, CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<FriendSummary>>.Success(
                        Array.Empty<FriendSummary>()));

            public UniTask<BackendResult<FriendRequestOutcome>> SendRequestAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult<FriendRequestOutcome>.Success(FriendRequestOutcome.Sent));

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
                ListIncomingRequestsAsync(CancellationToken cancellation) =>
                EmptyRequests();

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
                ListOutgoingRequestsAsync(CancellationToken cancellation) =>
                EmptyRequests();

            public UniTask<BackendResult> AcceptRequestAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            public UniTask<BackendResult> DeclineRequestAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            public UniTask<BackendResult> RemoveFriendAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            private static UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>> EmptyRequests() =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<FriendRequestSummary>>.Success(
                        Array.Empty<FriendRequestSummary>()));
        }
    }
}
