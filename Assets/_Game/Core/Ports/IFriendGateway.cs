using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Home;

namespace Game.Core.Ports
{
    /// <summary>
    /// Friends, and the requests that become friends. Implemented by whichever
    /// layer talks to the backend, called by presentation.
    /// </summary>
    /// <remarks>
    /// Every operation is a request the caller made, so each returns its own
    /// answer instead of reporting through a shared channel. Nothing here is
    /// pushed: the server holds no live connection to this client, and a change
    /// another player made is seen on the next call.
    /// </remarks>
    public interface IFriendGateway
    {
        /// <summary>
        /// The friend list with each friend's presence.
        /// </summary>
        /// <remarks>
        /// Online and offline arrive in one list. <see cref="FriendListSystem"/>
        /// splits them by <see cref="FriendSummary.IsOnline"/>, and a server that
        /// split them too would be a second place to keep that rule.
        /// </remarks>
        UniTask<BackendResult<IReadOnlyList<FriendSummary>>> ListFriendsAsync(
            CancellationToken cancellation);

        /// <summary>
        /// Finds users whose nickname starts with <paramref name="nickname"/>.
        /// </summary>
        /// <remarks>
        /// Prefix match, at least two characters, and the same character rules a
        /// nickname obeys. A shorter or stranger query is refused with
        /// <see cref="BackendFailure.InvalidRequest"/> rather than scanning.
        /// <para>
        /// Results carry <see cref="FriendPresence.Offline"/> throughout. The
        /// server does not report presence for search results and the search row
        /// does not show it; the value is a placeholder, not an answer, and
        /// nothing should read it.
        /// </para>
        /// <para>
        /// People who have blocked this player are absent, and people this player
        /// blocked are absent too. Neither is distinguishable from not existing.
        /// </para>
        /// </remarks>
        UniTask<BackendResult<IReadOnlyList<FriendSummary>>> SearchAsync(
            string nickname, CancellationToken cancellation);

        /// <summary>
        /// Sends a friend request. Answers whether it is pending or already
        /// settled into a friendship.
        /// </summary>
        /// <param name="playerId">
        /// The other player's id, not their nickname. Nicknames change and are
        /// case sensitive, so only an id names the row the player picked.
        /// </param>
        UniTask<BackendResult<FriendRequestOutcome>> SendRequestAsync(
            string playerId, CancellationToken cancellation);

        /// <summary>Requests waiting for this player to answer.</summary>
        UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>> ListIncomingRequestsAsync(
            CancellationToken cancellation);

        /// <summary>
        /// Requests this player sent that nobody has answered yet.
        /// </summary>
        /// <remarks>
        /// <see cref="FriendRequestSummary.RequestedAtUtc"/> is when it was sent
        /// rather than received, since it is the same row read from the other
        /// side.
        /// </remarks>
        UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>> ListOutgoingRequestsAsync(
            CancellationToken cancellation);

        /// <summary>Accepts a received request, making both players friends.</summary>
        UniTask<BackendResult> AcceptRequestAsync(
            string playerId, CancellationToken cancellation);

        /// <summary>
        /// Removes a pending request.
        /// </summary>
        /// <remarks>
        /// Declining one received and cancelling one sent are the same operation
        /// on the same row, so they are one method. Two names here would be two
        /// spellings of one call, and a reader would look for a difference that
        /// does not exist. Which of the two it was is a question for the screen.
        /// </remarks>
        UniTask<BackendResult> DeclineRequestAsync(
            string playerId, CancellationToken cancellation);

        /// <summary>
        /// Ends a friendship. The row is deleted, so either player can request
        /// again afterwards.
        /// </summary>
        UniTask<BackendResult> RemoveFriendAsync(
            string playerId, CancellationToken cancellation);
    }
}
