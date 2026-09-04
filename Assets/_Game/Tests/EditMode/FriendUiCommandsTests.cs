using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// Checks what the friend screen does with the answers a server gives it,
    /// against a gateway that gives whichever answer the test wants.
    /// </summary>
    public sealed class FriendUiCommandsTests
    {
        [Test]
        public async Task Refresh_SplitsFriendsByPresence()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[]
            {
                Friend("a", "가", FriendPresence.Online),
                Friend("b", "나", FriendPresence.Offline),
                Friend("c", "다", FriendPresence.InGame)
            };
            var commands = Build(gateway, out var friends, out _);

            var failure = await commands.RefreshFriendsAsync(CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(2));
            Assert.That(friends.OfflineFriends.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task AFailedRefresh_LeavesTheListAlone()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[] { Friend("a", "가", FriendPresence.Online) };
            var commands = Build(gateway, out var friends, out _);
            await commands.RefreshFriendsAsync(CancellationToken.None);

            gateway.Failure = BackendFailure.Offline;
            var failure = await commands.RefreshFriendsAsync(CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.Offline));

            // Emptying it would tell the player they have no friends, which is a
            // different statement from "this did not load".
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Search_ShowsWhatTheServerFound()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);

            var failure = await commands.SearchAsync(
                "나", Array.Empty<string>(), CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].Nickname, Is.EqualTo("나그네"));
            Assert.That(gateway.LastQuery, Is.EqualTo("나"));
        }

        [Test]
        public async Task Search_HidesPeopleWhoAreAlreadyFriends()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[]
            {
                Friend("a", "가나다", FriendPresence.Offline),
                Friend("b", "가나라", FriendPresence.Offline)
            };
            var commands = Build(gateway, out _, out var search);

            await commands.SearchAsync("가나", new[] { "a" }, CancellationToken.None);

            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].PlayerId, Is.EqualTo("b"));
        }

        [Test]
        public async Task SendingARequest_MarksTheRowPending()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);

            var failure = await commands.SendRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(search.Results[0].IsPending, Is.True);
        }

        [Test]
        public async Task ARefusedRequest_TakesThePendingMarkBack()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);

            gateway.Failure = BackendFailure.TargetNotFound;
            var failure = await commands.SendRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.TargetNotFound));

            // Left pending, the row would say "요청 중" for a request that was
            // never made, and the player would wait for an answer that cannot
            // come.
            Assert.That(search.Results[0].IsPending, Is.False);
        }

        [Test]
        public async Task ARequestTheServerSettles_RefreshesTheFriendListInstead()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out var friends, out var search);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);

            // The other player had already asked, so this call made them friends
            // rather than leaving a request pending.
            gateway.Outcome = FriendRequestOutcome.BecameFriends;
            gateway.Friends = new[] { Friend("b", "나그네", FriendPresence.Online) };

            var failure = await commands.SendRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));

            // Gone from the results, not merely no longer pending: the refresh
            // this triggered told the search they are a friend now, and a
            // friend is not somebody to offer a friend request to.
            Assert.That(search.Results, Is.Empty);
        }

        [Test]
        public async Task ANewFriend_StopsBeingOfferedInTheOpenSearch()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);
            Assert.That(search.Results.Count, Is.EqualTo(1), "offered before");

            // Accepting their request makes them a friend. The search is still
            // on screen, and its results were built when they were a stranger.
            gateway.Friends = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            await commands.AcceptRequestAsync("b", CancellationToken.None);

            Assert.That(
                search.Results, Is.Empty,
                "the row would still say 친구요청, and the server answers that ALREADY_FRIENDS");
        }

        [Test]
        public async Task SomeoneWhoAlreadyAsked_IsNotOfferedInTheSearch()
        {
            var gateway = new FakeFriendGateway();
            gateway.Requests = new[] { Request("b", "나그네") };
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);

            await commands.ListIncomingRequestsAsync(CancellationToken.None);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);

            // They are already on screen just above, with an accept button.
            // Offering to send them a request as well showed one person as two
            // things at once.
            Assert.That(search.Results, Is.Empty);
        }

        [Test]
        public async Task SomeoneWhoAlreadyAsked_IsHiddenEvenWhenTheSearchCameFirst()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);
            Assert.That(search.Results.Count, Is.EqualTo(1), "offered before");

            gateway.Requests = new[] { Request("b", "나그네") };
            await commands.ListIncomingRequestsAsync(CancellationToken.None);

            Assert.That(search.Results, Is.Empty);
        }

        [Test]
        public async Task DecliningARequest_PutsThatPersonBackInTheSearch()
        {
            var gateway = new FakeFriendGateway();
            gateway.Requests = new[] { Request("b", "나그네") };
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.ListIncomingRequestsAsync(CancellationToken.None);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);
            Assert.That(search.Results, Is.Empty, "hidden while they are waiting");

            await commands.DeclineRequestAsync("b", CancellationToken.None);

            // The screen re-reads the requests after answering one, and this
            // time the list does not name them.
            gateway.Requests = Array.Empty<FriendRequestSummary>();
            await commands.ListIncomingRequestsAsync(CancellationToken.None);

            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].PlayerId, Is.EqualTo("b"));
        }

        [Test]
        public async Task ARequestCannotBeSentToSomeoneWhoAlreadyAsked()
        {
            var gateway = new FakeFriendGateway();
            gateway.Requests = new[] { Request("b", "나그네") };
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.ListIncomingRequestsAsync(CancellationToken.None);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);

            await commands.SendRequestAsync("b", CancellationToken.None);

            // Not merely invisible: nothing was sent. The server would answer
            // this by making them friends on the spot, which is a second and
            // worse way to reach what the accept button above does.
            Assert.That(gateway.SentTo, Is.Null);
            Assert.That(search.Results, Is.Empty);
        }

        [Test]
        public async Task CancellingASentRequest_LetsItBeSentAgain()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);
            await commands.SendRequestAsync("b", CancellationToken.None);
            Assert.That(search.Results[0].IsPending, Is.True);

            var failure = await commands.CancelSentRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(gateway.Declined, Is.EqualTo("b"), "cancel and decline are one call");
            Assert.That(
                search.Results[0].IsPending, Is.False,
                "there is no request now, so the row offers one again");
        }

        [Test]
        public async Task AFailedCancel_LeavesTheRowWaiting()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나", Array.Empty<string>(), CancellationToken.None);
            await commands.SendRequestAsync("b", CancellationToken.None);

            gateway.Failure = BackendFailure.Offline;
            var failure = await commands.CancelSentRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.Offline));

            // The request is still there. Clearing the mark would say it was
            // taken back when it was not.
            Assert.That(search.Results[0].IsPending, Is.True);
        }

        [Test]
        public async Task BlockingSomeone_ReloadsTheFriendList()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[]
            {
                Friend("a", "가", FriendPresence.Online),
                Friend("b", "나", FriendPresence.Online)
            };
            var commands = Build(gateway, out var friends, out _);
            await commands.RefreshFriendsAsync(CancellationToken.None);
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(2));

            // The server ends the friendship as part of blocking, so the list it
            // answers with afterwards is shorter.
            gateway.Friends = new[] { Friend("a", "가", FriendPresence.Online) };
            var failure = await commands.BlockAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));
            Assert.That(friends.OnlineFriends[0].PlayerId, Is.EqualTo("a"));
        }

        [Test]
        public async Task SentRequests_AreReadFromTheOutgoingDirection()
        {
            var gateway = new FakeFriendGateway();
            gateway.Sent = new[] { Request("b", "나그네") };
            var commands = Build(gateway, out _, out _);

            var answer = await commands.ListOutgoingRequestsAsync(CancellationToken.None);

            Assert.That(answer.Ok, Is.True);
            Assert.That(answer.Value.Count, Is.EqualTo(1));
            Assert.That(answer.Value[0].PlayerId, Is.EqualTo("b"));
        }

        [Test]
        public async Task AcceptingARequest_RefreshesTheFriendList()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[] { Friend("b", "나그네", FriendPresence.Online) };
            var commands = Build(gateway, out var friends, out _);

            var failure = await commands.AcceptRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(gateway.Accepted, Is.EqualTo("b"));
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task DecliningARequest_ChangesNothingElse()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[] { Friend("a", "가", FriendPresence.Online) };
            var commands = Build(gateway, out var friends, out _);
            await commands.RefreshFriendsAsync(CancellationToken.None);

            var failure = await commands.DeclineRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(gateway.Declined, Is.EqualTo("b"));
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task FriendIds_CoversBothHalvesOfTheList()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[]
            {
                Friend("a", "가", FriendPresence.Online),
                Friend("b", "나", FriendPresence.Offline)
            };
            var commands = Build(gateway, out _, out _);
            await commands.RefreshFriendsAsync(CancellationToken.None);

            Assert.That(commands.FriendIds(), Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test]
        public void CancellingAPendingRequest_IsSafeWhenNothingIsPending()
        {
            var search = new FriendSearchSystem();

            Assert.DoesNotThrow(() => search.CancelPendingRequest("nobody"));
        }

        private static FriendSummary Friend(string id, string nickname, FriendPresence presence) =>
            new FriendSummary(id, nickname, presence);

        private static FriendRequestSummary Request(string id, string nickname) =>
            new FriendRequestSummary(id, nickname, DateTime.UtcNow);

        private static FriendUiCommands Build(
            IFriendGateway gateway,
            out FriendListSystem friends,
            out FriendSearchSystem search)
        {
            friends = new FriendListSystem();
            search = new FriendSearchSystem();
            return new FriendUiCommands(gateway, new FakeBlockGateway(), friends, search);
        }

        /// <summary>Accepts every block and remembers the last one.</summary>
        private sealed class FakeBlockGateway : IBlockGateway
        {
            public string Blocked { get; private set; }

            public UniTask<BackendResult> BlockAsync(
                string playerId, CancellationToken cancellation)
            {
                Blocked = playerId;
                return UniTask.FromResult(BackendResult.Success());
            }

            public UniTask<BackendResult> UnblockAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            public UniTask<BackendResult<IReadOnlyList<BlockedPlayer>>> ListBlockedAsync(
                CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<BlockedPlayer>>.Success(
                        Array.Empty<BlockedPlayer>()));
        }

        /// <summary>Answers whatever the test set, and records what it was asked.</summary>
        private sealed class FakeFriendGateway : IFriendGateway
        {
            public BackendFailure Failure { get; set; } = BackendFailure.None;

            public IReadOnlyList<FriendSummary> Friends { get; set; } =
                Array.Empty<FriendSummary>();

            public IReadOnlyList<FriendSummary> Found { get; set; } =
                Array.Empty<FriendSummary>();

            public IReadOnlyList<FriendRequestSummary> Requests { get; set; } =
                Array.Empty<FriendRequestSummary>();

            public FriendRequestOutcome Outcome { get; set; } = FriendRequestOutcome.Sent;

            public string LastQuery { get; private set; }

            public string Accepted { get; private set; }

            public string Declined { get; private set; }

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> ListFriendsAsync(
                CancellationToken cancellation) => Answer(Friends);

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> SearchAsync(
                string nickname, CancellationToken cancellation)
            {
                LastQuery = nickname;
                return Answer(Found);
            }

            public string SentTo { get; private set; }

            public UniTask<BackendResult<FriendRequestOutcome>> SendRequestAsync(
                string playerId, CancellationToken cancellation)
            {
                SentTo = playerId;
                return UniTask.FromResult(
                    Failure == BackendFailure.None
                        ? BackendResult<FriendRequestOutcome>.Success(Outcome)
                        : BackendResult<FriendRequestOutcome>.Failed(Failure));
            }

            public IReadOnlyList<FriendRequestSummary> Sent { get; set; } =
                Array.Empty<FriendRequestSummary>();

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
                ListIncomingRequestsAsync(CancellationToken cancellation) => Answer(Requests);

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
                ListOutgoingRequestsAsync(CancellationToken cancellation) => Answer(Sent);

            public UniTask<BackendResult> AcceptRequestAsync(
                string playerId, CancellationToken cancellation)
            {
                Accepted = playerId;
                return Answer();
            }

            public UniTask<BackendResult> DeclineRequestAsync(
                string playerId, CancellationToken cancellation)
            {
                Declined = playerId;
                return Answer();
            }

            public UniTask<BackendResult> RemoveFriendAsync(
                string playerId, CancellationToken cancellation) => Answer();

            private UniTask<BackendResult> Answer()
            {
                return UniTask.FromResult(
                    Failure == BackendFailure.None
                        ? BackendResult.Success()
                        : BackendResult.Failed(Failure));
            }

            private UniTask<BackendResult<T>> Answer<T>(T value)
            {
                return UniTask.FromResult(
                    Failure == BackendFailure.None
                        ? BackendResult<T>.Success(value)
                        : BackendResult<T>.Failed(Failure));
            }
        }
    }
}
