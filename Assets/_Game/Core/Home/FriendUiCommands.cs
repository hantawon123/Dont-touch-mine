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
        private readonly IBlockGateway blocks;
        private readonly FriendListSystem friends;
        private readonly FriendSearchSystem search;

        public FriendUiCommands(
            IFriendGateway gateway,
            IBlockGateway blocks,
            FriendListSystem friends,
            FriendSearchSystem search)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
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
        public async UniTask<BackendFailure> RefreshFriendsAsync(CancellationToken cancellation)
        {
            var answer = await gateway.ListFriendsAsync(cancellation);
            if (!answer.Ok)
            {
                return answer.Failure;
            }

            friends.ReplaceFriends(answer.Value);

            // The search is told too. A result list built before this refresh
            // would still offer to befriend whoever just became a friend, and
            // the server answers that button with ALREADY_FRIENDS.
            search.ExcludeFriends(FriendIds());
            return BackendFailure.None;
        }

        /// <summary>
        /// Searches the server and shows what came back.
        /// </summary>
        /// <remarks>
        /// Two steps, because the two filters are not the same one.
        /// <see cref="FriendSearchSystem.Search"/> narrows what this client
        /// already holds and decides which rows are already friends;
        /// the server decides who exists, hides anyone involved in a block, and
        /// matches a prefix rather than a substring. Replacing the directory
        /// re-runs the local pass against the new rows, so the order here is
        /// what makes both apply.
        /// </remarks>
        public async UniTask<BackendFailure> SearchAsync(
            string query, IEnumerable<string> existingFriendIds, CancellationToken cancellation)
        {
            var answer = await gateway.SearchAsync(query, cancellation);
            if (!answer.Ok)
            {
                return answer.Failure;
            }

            search.ReplaceDirectory(answer.Value);
            search.Search(query, existingFriendIds);
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
                return BackendFailure.None;
            }

            var answer = await gateway.SendRequestAsync(playerId, cancellation);
            if (!answer.Ok)
            {
                search.CancelPendingRequest(playerId);
                return answer.Failure;
            }

            if (answer.Value == FriendRequestOutcome.BecameFriends)
            {
                search.CancelPendingRequest(playerId);
                return await RefreshFriendsAsync(cancellation);
            }

            return BackendFailure.None;
        }

        /// <summary>Requests waiting for this player to answer.</summary>
        /// <remarks>
        /// Returned rather than stored in a system, because nothing in
        /// <c>Game.Core</c> holds this list yet. The screen that shows it owns
        /// it until there is a reason for two screens to share one.
        /// <para>
        /// The search is told who they are on the way past, so the same person
        /// cannot appear both as a request to accept and as a stranger to send
        /// one to. Declining puts them back: the next read of this list no
        /// longer names them, and the search stops hiding them.
        /// </para>
        /// </remarks>
        public async UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
            ListIncomingRequestsAsync(CancellationToken cancellation)
        {
            var answer = await gateway.ListIncomingRequestsAsync(cancellation);
            if (answer.Ok)
            {
                search.ExcludeIncomingRequests(RequesterIds(answer.Value));
            }

            return answer;
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

            return await RefreshFriendsAsync(cancellation);
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
            return await gateway.ListOutgoingRequestsAsync(cancellation);
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
        /// Blocks someone, then reloads the friend list.
        /// </summary>
        /// <remarks>
        /// The reload is not optional. Blocking ends the friendship and drops
        /// any request between the two, so a list left as it was would show a
        /// friend who is no longer one and offer to unfriend a row the server
        /// has already removed.
        /// </remarks>
        public async UniTask<BackendFailure> BlockAsync(
            string playerId, CancellationToken cancellation)
        {
            var answer = await blocks.BlockAsync(playerId, cancellation);
            if (!answer.Ok)
            {
                return answer.Failure;
            }

            return await RefreshFriendsAsync(cancellation);
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
