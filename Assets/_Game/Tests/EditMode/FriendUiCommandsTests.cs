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
        public async Task OutgoingRequests_RestorePendingStateAfterSearchingAgain()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            gateway.Sent = new[] { Request("b", "나그네") };
            var commands = Build(gateway, out _, out var search);
            await commands.ListOutgoingRequestsAsync(CancellationToken.None);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);
            Assert.That(search.Results[0].IsPending, Is.True);

            gateway.Sent = Array.Empty<FriendRequestSummary>();
            await commands.ListOutgoingRequestsAsync(CancellationToken.None);
            Assert.That(search.Results[0].IsPending, Is.False);
        }

        [Test]
        public async Task AlreadySentResponse_KeepsTheCancelActionAvailable()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);
            gateway.Failure = BackendFailure.RequestAlreadySent;
            Assert.That(await commands.SendRequestAsync("b", CancellationToken.None), Is.EqualTo(BackendFailure.None));
            Assert.That(search.Results[0].IsPending, Is.True);
        }
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
                "나그네", Array.Empty<string>(), CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].Nickname, Is.EqualTo("나그네"));
            Assert.That(gateway.LastQuery, Is.EqualTo("나그네"));
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

            // Asked for separately because the search matches a whole nickname:
            // one query cannot name them both.
            await commands.SearchAsync("가나다", new[] { "a" }, CancellationToken.None);
            Assert.That(
                search.Results, Is.Empty, "이미 친구인 사람을 또 추가하라고 하면 안 된다.");

            await commands.SearchAsync("가나라", new[] { "a" }, CancellationToken.None);

            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].PlayerId, Is.EqualTo("b"));
        }

        [Test]
        public async Task SendingARequest_MarksTheRowPending()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);

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
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);

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
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);

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
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);
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
        public async Task SomeoneWhoAlreadyAsked_IsShownAsIncoming()
        {
            var gateway = new FakeFriendGateway();
            gateway.Requests = new[] { Request("b", "나그네") };
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);

            await commands.ListIncomingRequestsAsync(CancellationToken.None);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);

            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].IsIncoming, Is.True);
        }

        [Test]
        public async Task SomeoneWhoAlreadyAsked_UpdatesAnExistingSearchResult()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);
            Assert.That(search.Results.Count, Is.EqualTo(1), "offered before");

            gateway.Requests = new[] { Request("b", "나그네") };
            await commands.ListIncomingRequestsAsync(CancellationToken.None);

            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].IsIncoming, Is.True);
        }

        [Test]
        public async Task DecliningARequest_AllowsANewRequestFromSearch()
        {
            var gateway = new FakeFriendGateway();
            gateway.Requests = new[] { Request("b", "나그네") };
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.ListIncomingRequestsAsync(CancellationToken.None);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);
            Assert.That(search.Results[0].IsIncoming, Is.True);

            await commands.DeclineRequestAsync("b", CancellationToken.None);

            gateway.Requests = Array.Empty<FriendRequestSummary>();
            await commands.ListIncomingRequestsAsync(CancellationToken.None);

            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].PlayerId, Is.EqualTo("b"));
            Assert.That(search.Results[0].RequestState, Is.EqualTo(FriendRequestState.None));
        }

        [Test]
        public async Task ARequestCannotBeSentToSomeoneWhoAlreadyAsked()
        {
            var gateway = new FakeFriendGateway();
            gateway.Requests = new[] { Request("b", "나그네") };
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.ListIncomingRequestsAsync(CancellationToken.None);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);

            await commands.SendRequestAsync("b", CancellationToken.None);

            Assert.That(gateway.SentTo, Is.Null);
            Assert.That(search.Results.Count, Is.EqualTo(1));
            Assert.That(search.Results[0].IsIncoming, Is.True);
        }

        [Test]
        public async Task CancellingASentRequest_LetsItBeSentAgain()
        {
            var gateway = new FakeFriendGateway();
            gateway.Found = new[] { Friend("b", "나그네", FriendPresence.Offline) };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);
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
            await commands.SearchAsync("나그네", Array.Empty<string>(), CancellationToken.None);
            await commands.SendRequestAsync("b", CancellationToken.None);

            gateway.Failure = BackendFailure.Offline;
            var failure = await commands.CancelSentRequestAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.Offline));

            // The request is still there. Clearing the mark would say it was
            // taken back when it was not.
            Assert.That(search.Results[0].IsPending, Is.True);
        }

        [Test]
        public async Task EndingAFriendship_ReloadsTheFriendList()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[]
            {
                Friend("a", "가", FriendPresence.Online),
                Friend("b", "나", FriendPresence.Online)
            };
            var commands = Build(gateway, out var friends, out _);
            await commands.RefreshFriendsAsync(CancellationToken.None);

            gateway.Friends = new[] { Friend("a", "가", FriendPresence.Online) };
            var failure = await commands.RemoveFriendAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.None));
            Assert.That(gateway.Removed, Is.EqualTo("b"));
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task AFailedUnfriend_LeavesTheListAlone()
        {
            var gateway = new FakeFriendGateway();
            gateway.Friends = new[] { Friend("b", "나", FriendPresence.Online) };
            var commands = Build(gateway, out var friends, out _);
            await commands.RefreshFriendsAsync(CancellationToken.None);

            gateway.Failure = BackendFailure.Offline;
            var failure = await commands.RemoveFriendAsync("b", CancellationToken.None);

            Assert.That(failure, Is.EqualTo(BackendFailure.Offline));

            // Removing it here would show a friendship ended that still stands.
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));
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

        [Test]
        public async Task ConcurrentRefresh_SharesOneGatewayCall()
        {
            var pending = new UniTaskCompletionSource<BackendResult<IReadOnlyList<FriendSummary>>>();
            var gateway = new FakeFriendGateway { DeferredList = _ => pending.Task };
            var commands = Build(gateway, out _, out _);
            var first = commands.RefreshFriendsAsync(CancellationToken.None).AsTask();
            var second = commands.RefreshFriendsAsync(CancellationToken.None).AsTask();
            var third = commands.RefreshFriendsAsync(CancellationToken.None).AsTask();
            Assert.That(gateway.ListCalls, Is.EqualTo(1));
            pending.TrySetResult(BackendResult<IReadOnlyList<FriendSummary>>.Success(Array.Empty<FriendSummary>()));
            Assert.That(await first, Is.EqualTo(BackendFailure.None));
            Assert.That(await second, Is.EqualTo(BackendFailure.None));
            Assert.That(await third, Is.EqualTo(BackendFailure.None));
        }

        [Test]
        public async Task AcceptDuringRefresh_KeepsNewFriendWhenOldResponseArrives()
        {
            var pending = new UniTaskCompletionSource<BackendResult<IReadOnlyList<FriendSummary>>>();
            var gateway = new FakeFriendGateway { DeferredList = _ => pending.Task };
            var commands = Build(gateway, out var friends, out _);
            var old = commands.RefreshFriendsAsync(CancellationToken.None);
            gateway.DeferredList = null;
            gateway.Friends = new[] { Friend("new", "새친구", FriendPresence.Online) };
            await commands.AcceptRequestAsync("new", CancellationToken.None);
            pending.TrySetResult(BackendResult<IReadOnlyList<FriendSummary>>.Success(Array.Empty<FriendSummary>()));
            Assert.That(await old, Is.EqualTo(BackendFailure.Cancelled));
            Assert.That(friends.OnlineFriends.Count, Is.EqualTo(1));
            Assert.That(gateway.ListCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task Search_LateResponseCannotReplaceLatestQuery()
        {
            var pending = new UniTaskCompletionSource<BackendResult<IReadOnlyList<FriendSummary>>>();
            var gateway = new FakeFriendGateway { DeferredSearch = _ => pending.Task };
            var commands = Build(gateway, out _, out var search);
            var old = commands.SearchAsync("이전", Array.Empty<string>(), CancellationToken.None);
            gateway.DeferredSearch = null;
            gateway.Found = new[] { Friend("new", "최신", FriendPresence.Offline) };
            await commands.SearchAsync("최신", Array.Empty<string>(), CancellationToken.None);
            pending.TrySetResult(BackendResult<IReadOnlyList<FriendSummary>>.Success(new[] { Friend("old", "이전", FriendPresence.Offline) }));
            Assert.That(await old, Is.EqualTo(BackendFailure.Cancelled));
            Assert.That(search.Results[0].PlayerId, Is.EqualTo("new"));
        }

        [Test]
        public async Task Search_CancelledScreenCannotPublishLateSuccess()
        {
            using var cancellation = new CancellationTokenSource();
            var pending = new UniTaskCompletionSource<BackendResult<IReadOnlyList<FriendSummary>>>();
            var gateway = new FakeFriendGateway { DeferredSearch = _ => pending.Task };
            var commands = Build(gateway, out _, out var search);
            var task = commands.SearchAsync("사용자", Array.Empty<string>(), cancellation.Token);
            cancellation.Cancel();
            pending.TrySetResult(BackendResult<IReadOnlyList<FriendSummary>>.Success(new[] { Friend("id", "사용자", FriendPresence.Offline) }));
            Assert.That(await task, Is.EqualTo(BackendFailure.Cancelled));
            Assert.That(search.Results, Is.Empty);
        }

        [Test]
        public async Task Search_UsesTrimmedExactCaseSensitiveNicknameBeforeSending()
        {
            var gateway = new FakeFriendGateway { Found = new[] {
                Friend("exact", "Player", FriendPresence.Offline),
                Friend("case", "player", FriendPresence.Offline),
                Friend("prefix", "Player2", FriendPresence.Offline) } };
            var commands = Build(gateway, out _, out var search);
            await commands.SearchAsync(" Player ", Array.Empty<string>(), CancellationToken.None);
            Assert.That(gateway.LastQuery, Is.EqualTo("Player"));
            Assert.That(search.Results.Count, Is.EqualTo(1));
            await commands.SendRequestAsync(search.Results[0].PlayerId, CancellationToken.None);
            Assert.That(gateway.SentTo, Is.EqualTo("exact"));
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
            return new FriendUiCommands(gateway, friends, search);
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

            public Func<CancellationToken, UniTask<BackendResult<IReadOnlyList<FriendSummary>>>> DeferredList;
            public Func<string, UniTask<BackendResult<IReadOnlyList<FriendSummary>>>> DeferredSearch;
            public int ListCalls;
            public string LastQuery { get; private set; }

            public string Accepted { get; private set; }

            public string Declined { get; private set; }

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> ListFriendsAsync(
                CancellationToken cancellation)
            {
                ListCalls++;
                return DeferredList != null ? DeferredList(cancellation) : Answer(Friends);
            }

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> SearchAsync(
                string nickname, CancellationToken cancellation)
            {
                LastQuery = nickname;
                return DeferredSearch != null ? DeferredSearch(nickname) : Answer(Found);
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

            public string Removed { get; private set; }

            public UniTask<BackendResult> RemoveFriendAsync(
                string playerId, CancellationToken cancellation)
            {
                Removed = playerId;
                return Answer();
            }

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
