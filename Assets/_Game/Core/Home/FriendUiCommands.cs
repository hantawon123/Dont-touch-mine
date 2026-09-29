using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;

namespace Game.Core.Home
{
    /// <summary>
    /// Commands the friend service on behalf of UI and feeds the answers into
    /// the systems the screen reads.
    /// </summary>
    /// <remarks>
    /// The systems hold what is on screen and know nothing about where it came
    /// from; the gateway knows the server and nothing about what is on screen.
    /// This is the one place that joins them, so a screen never has to remember
    /// that a search must reach the server before its own filter can run.
    /// <para>
    /// Every method answers with the failure rather than throwing, so a caller
    /// can show a message. <see cref="BackendFailure.None"/> means it worked.
    /// </para>
    /// </remarks>
    public sealed class FriendUiCommands
    {
        private readonly IFriendGateway gateway;
        private readonly FriendListSystem friends;
        private readonly FriendSearchSystem search;
        private int searchRevision;
        private int refreshRevision;
        private bool refreshInFlight;
        private CancellationToken refreshCancellation;
        private UniTask<BackendFailure> activeRefresh;

        public FriendUiCommands(
            IFriendGateway gateway,
            FriendListSystem friends,
            FriendSearchSystem search)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
            this.search = search ?? throw new ArgumentNullException(nameof(search));
        }

        /// <summary>
        /// Replaces the friend list with the server's, presence included.
        /// </summary>
        /// <remarks>
        /// The list is left alone when the call fails. Emptying it would tell
        /// the player they have no friends, which is a different statement from
        /// "this did not load".
        /// </remarks>
        public UniTask<BackendFailure> RefreshFriendsAsync(CancellationToken cancellation, bool force = false)
        {
            if (refreshInFlight && !force && refreshCancellation == cancellation) return activeRefresh;
            refreshInFlight = true;
            refreshCancellation = cancellation;
            // AsyncLazy supports multiple callers awaiting before the response arrives.
            activeRefresh = RefreshFriendsCoreAsync(++refreshRevision, cancellation).ToAsyncLazy().Task;
            return activeRefresh;
        }

        private async UniTask<BackendFailure> RefreshFriendsCoreAsync(int revision, CancellationToken cancellation)
        {
            try
            {
                var answer = await gateway.ListFriendsAsync(cancellation);
                if (cancellation.IsCancellationRequested || revision != refreshRevision) return BackendFailure.Cancelled;
                if (!answer.Ok) return answer.Failure;
                friends.ReplaceFriends(answer.Value);
                search.ExcludeFriends(FriendIds());
                return BackendFailure.None;
            }
            finally
            {
                if (revision == refreshRevision) refreshInFlight = false;
            }
        }

        /// <summary>
        /// Searches the server and shows what came back.
        /// </summary>
        /// <remarks>
        /// Two steps, because the two filters are not the same one.
        /// <see cref="FriendSearchSystem.Search"/> narrows what this client
        /// already holds and decides which rows are already friends;
        /// the server decides who exists and matches the whole nickname rather than a
        /// substring. Replacing the directory
        /// re-runs the local pass against the new rows, so the order here is
        /// what makes both apply.
        /// </remarks>
        public async UniTask<BackendFailure> SearchAsync(
            string query, IEnumerable<string> existingFriendIds, CancellationToken cancellation)
        {
            var revision = ++searchRevision;
            query = query?.Trim() ?? string.Empty;
            search.Search(query, existingFriendIds);
            var answer = await gateway.SearchAsync(query, cancellation);
            if (cancellation.IsCancellationRequested || revision != searchRevision) return BackendFailure.Cancelled;
            if (!answer.Ok)
            {
                return answer.Failure;
            }

            search.ReplaceDirectory(answer.Value);
            var excluded = new HashSet<string>(existingFriendIds);
            excluded.UnionWith(FriendIds());
            search.Search(query, excluded);
            return BackendFailure.None;
        }

        /// <summary>
        /// Sends a friend request and settles what the row should say.
        /// </summary>
        /// <remarks>
        /// The row is marked pending before the call so the button answers the
        /// click, and taken back if the server refuses. Leaving it would show
        /// "요청 중" for a request that was never made, and the player would
        /// wait for an answer that cannot come.
        /// <para>
        /// When the server settles it into a friendship instead — which happens
        /// when the other player asked first — the friend list is refreshed
        /// rather than the pending mark being kept, because there is nothing
        /// left to wait for.
        /// </para>
        /// <para>
        /// Only rows the search is currently showing can be sent to. That is
        /// every caller today, since the button lives on such a row.
        /// </para>
        /// </remarks>
        public async UniTask<BackendFailure> SendRequestAsync(
            string playerId, CancellationToken cancellation)
        {
            if (!search.TrySendRequest(playerId))
            {
                // The row refused: already a friend, already asked, or somebody
                // who asked first and is shown with an accept button instead.
                // Sending anyway spends a round trip to be told something this
                // screen already knows, and answers with a failure the player
                // cannot act on.
                //
                // This is why marking the row belongs here and nowhere else. A
                // screen that marked it first would leave this looking at a row
                // that is already waiting, and the request would be dropped
                // while the row claimed it had been sent.
                return BackendFailure.None;
            }

            var answer = await gateway.SendRequestAsync(playerId, cancellation);
            if (!answer.Ok)
            {
                if (answer.Failure == BackendFailure.RequestAlreadySent)
                    return BackendFailure.None;
                search.CancelPendingRequest(playerId);
                return answer.Failure;
            }

            if (answer.Value == FriendRequestOutcome.BecameFriends)
            {
                search.CancelPendingRequest(playerId);
                return await RefreshFriendsAsync(cancellation, force: true);
            }

            return BackendFailure.None;
        }

        /// <summary>Requests waiting for this player to answer.</summary>
        /// <remarks>
        /// Returned rather than stored in a system, because nothing in
        /// <c>Game.Core</c> holds this list yet. The screen that shows it owns
        /// it until there is a reason for two screens to share one.
        /// <para>
        /// Search results retain these people and show a received-request state
        /// with an accept action. After declining, the next read restores the
        /// ordinary send-request action.
        /// </para>
        /// </remarks>
        public async UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
            ListIncomingRequestsAsync(CancellationToken cancellation)
        {
            var answer = await gateway.ListIncomingRequestsAsync(cancellation);
            if (!answer.Ok)
            {
                return answer;
            }

            search.ReplaceIncomingRequests(RequesterIds(answer.Value));
            return BackendResult<IReadOnlyList<FriendRequestSummary>>.Success(
                FriendRequestOrder.Arrange(answer.Value));
        }

        private static IReadOnlyList<string> RequesterIds(
            IReadOnlyList<FriendRequestSummary> requests)
        {
            var ids = new List<string>(requests.Count);
            for (var index = 0; index < requests.Count; index++)
            {
                ids.Add(requests[index].PlayerId);
            }

            return ids;
        }

        /// <summary>
        /// Accepts a request. The two are friends afterwards, so the friend list
        /// is refreshed before this answers.
        /// </summary>
        public async UniTask<BackendFailure> AcceptRequestAsync(
            string playerId, CancellationToken cancellation)
        {
            var answer = await gateway.AcceptRequestAsync(playerId, cancellation);
            if (!answer.Ok)
            {
                return answer.Failure;
            }

            return await RefreshFriendsAsync(cancellation, force: true);
        }

        /// <summary>Turns a request down. Nothing else changes.</summary>
        public async UniTask<BackendFailure> DeclineRequestAsync(
            string playerId, CancellationToken cancellation)
        {
            var answer = await gateway.DeclineRequestAsync(playerId, cancellation);
            return answer.Ok ? BackendFailure.None : answer.Failure;
        }

        /// <summary>Requests this player sent and nobody has answered.</summary>
        public async UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
            ListOutgoingRequestsAsync(CancellationToken cancellation)
        {
            var answer = await gateway.ListOutgoingRequestsAsync(cancellation);
            if (answer.Ok && !cancellation.IsCancellationRequested)
                search.ReplaceOutgoingRequests(RequesterIds(answer.Value));
            return answer.Ok
                ? BackendResult<IReadOnlyList<FriendRequestSummary>>.Success(
                    FriendRequestOrder.Arrange(answer.Value))
                : answer;
        }

        /// <summary>
        /// Takes back a request this player sent.
        /// </summary>
        /// <remarks>
        /// The same server call that declines one received — it is the same row
        /// either way — but a different thing to the screen, which is why it has
        /// its own name here. The search row goes back to offering a request,
        /// because after this there is none.
        /// </remarks>
        public async UniTask<BackendFailure> CancelSentRequestAsync(
            string playerId, CancellationToken cancellation)
        {
            var answer = await gateway.DeclineRequestAsync(playerId, cancellation);
            if (!answer.Ok)
            {
                return answer.Failure;
            }

            search.CancelPendingRequest(playerId);
            return BackendFailure.None;
        }

        /// <summary>
        /// Ends a friendship, then reloads the friend list.
        /// </summary>
        /// <remarks>
        /// Both people can still find each other afterwards and either can ask
        /// again, so the screen asks for no confirmation before calling it.
        /// <para>
        /// The server also drops any room invite between the two. Someone just
        /// unfriended should not still be able to walk in on a code they were
        /// handed a moment ago.
        /// </para>
        /// </remarks>
        public async UniTask<BackendFailure> RemoveFriendAsync(
            string playerId, CancellationToken cancellation)
        {
            var answer = await gateway.RemoveFriendAsync(playerId, cancellation);
            if (!answer.Ok)
            {
                return answer.Failure;
            }

            return await RefreshFriendsAsync(cancellation, force: true);
        }

        /// <summary>
        /// The ids of everyone already befriended, which
        /// <see cref="SearchAsync"/> needs so results do not offer to befriend
        /// them again.
        /// </summary>
        public IReadOnlyList<string> FriendIds()
        {
            var ids = new List<string>(
                friends.OnlineFriends.Count + friends.OfflineFriends.Count);

            for (var index = 0; index < friends.OnlineFriends.Count; index++)
            {
                ids.Add(friends.OnlineFriends[index].PlayerId);
            }

            for (var index = 0; index < friends.OfflineFriends.Count; index++)
            {
                ids.Add(friends.OfflineFriends[index].PlayerId);
            }

            return ids;
        }
    }
}
