using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Backend
{
    /// <summary>
    /// <see cref="IFriendGateway"/> against the backend's friend endpoints.
    /// </summary>
    /// <remarks>
    /// Also where the wire's spellings become the game's types: presence names,
    /// timestamps and the settled-or-pending answer to a request. Doing it here
    /// keeps those strings from reaching anything that draws a screen.
    /// </remarks>
    public sealed class FriendGateway : IFriendGateway
    {
        private const string Friends = "/api/v1/friends";
        private const string FriendRequests = "/api/v1/friend-requests";
        private const string Users = "/api/v1/users";

        /// <summary>
        /// The server's timestamp format: fourteen characters, UTC, no zone.
        /// </summary>
        private const string TimeFormat = "yyyyMMddHHmmss";

        private readonly BackendClient client;

        public FriendGateway(BackendClient client)
        {
            this.client = client;
        }

        public async UniTask<BackendResult<IReadOnlyList<FriendSummary>>> ListFriendsAsync(
            CancellationToken cancellation)
        {
            var answer = await client.CallAsync<FriendListResponseDto>(
                HttpMethod.Get, Friends, null, BackendAuth.UserId, cancellation);

            if (!answer.Ok)
            {
                return BackendResult<IReadOnlyList<FriendSummary>>.Failed(answer.Failure);
            }

            var friends = new List<FriendSummary>();
            var rows = answer.Value.friends ?? Array.Empty<FriendSummaryDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (!Readable(row?.userId, row?.nickname, "friend"))
                {
                    continue;
                }

                friends.Add(new FriendSummary(row.userId, row.nickname, Presence(row.presence)));
            }

            return BackendResult<IReadOnlyList<FriendSummary>>.Success(friends);
        }

        public async UniTask<BackendResult<IReadOnlyList<FriendSummary>>> SearchAsync(
            string nickname, CancellationToken cancellation)
        {
            var path = Users + "?nickname=" + UnityWebRequest.EscapeURL(nickname ?? string.Empty);

            var answer = await client.CallAsync<UserSearchResponseDto>(
                HttpMethod.Get, path, null, BackendAuth.UserId, cancellation);

            if (!answer.Ok)
            {
                return BackendResult<IReadOnlyList<FriendSummary>>.Failed(answer.Failure);
            }

            var found = new List<FriendSummary>();
            var rows = answer.Value.users ?? Array.Empty<UserSummaryDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (!Readable(row?.userId, row?.nickname, "search result"))
                {
                    continue;
                }

                // Offline throughout, because the server does not report presence
                // for a search and the search row does not show it. A placeholder,
                // not an answer.
                found.Add(new FriendSummary(row.userId, row.nickname, FriendPresence.Offline));
            }

            return BackendResult<IReadOnlyList<FriendSummary>>.Success(found);
        }

        public async UniTask<BackendResult<FriendRequestOutcome>> SendRequestAsync(
            string playerId, CancellationToken cancellation)
        {
            var body = new SendFriendRequestRequestDto { userId = playerId };

            var answer = await client.CallAsync<SendFriendRequestResponseDto>(
                HttpMethod.Post, FriendRequests, body, BackendAuth.UserId, cancellation);

            if (!answer.Ok)
            {
                return BackendResult<FriendRequestOutcome>.Failed(answer.Failure);
            }

            // ACCEPTED means the other player had already asked and this call
            // settled it, so the two are friends now and no request is pending.
            var outcome = answer.Value.status == "ACCEPTED"
                ? FriendRequestOutcome.BecameFriends
                : FriendRequestOutcome.Sent;

            return BackendResult<FriendRequestOutcome>.Success(outcome);
        }

        // Incoming is the server's default, written out so that reading these
        // does not require knowing what the default is.
        public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
            ListIncomingRequestsAsync(CancellationToken cancellation) =>
            ListRequestsAsync("incoming", cancellation);

        public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
            ListOutgoingRequestsAsync(CancellationToken cancellation) =>
            ListRequestsAsync("outgoing", cancellation);

        private async UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>>
            ListRequestsAsync(string direction, CancellationToken cancellation)
        {
            var answer = await client.CallAsync<FriendRequestListResponseDto>(
                HttpMethod.Get,
                FriendRequests + "?direction=" + direction,
                null,
                BackendAuth.UserId,
                cancellation);

            if (!answer.Ok)
            {
                return BackendResult<IReadOnlyList<FriendRequestSummary>>.Failed(answer.Failure);
            }

            var requests = new List<FriendRequestSummary>();
            var rows = answer.Value.requests ?? Array.Empty<FriendRequestSummaryDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (!Readable(row?.userId, row?.nickname, "friend request"))
                {
                    continue;
                }

                requests.Add(new FriendRequestSummary(
                    row.userId, row.nickname, RequestedAt(row.requestedAt)));
            }

            return BackendResult<IReadOnlyList<FriendRequestSummary>>.Success(requests);
        }

        public UniTask<BackendResult> AcceptRequestAsync(
            string playerId, CancellationToken cancellation)
        {
            return client.CallAsync(
                HttpMethod.Post,
                FriendRequests + "/" + Segment(playerId) + "/accept",
                null,
                BackendAuth.UserId,
                cancellation);
        }

        public UniTask<BackendResult> DeclineRequestAsync(
            string playerId, CancellationToken cancellation)
        {
            // Declining what was received and cancelling what was sent are the
            // same operation on the same row, so they are the same call.
            return client.CallAsync(
                HttpMethod.Delete,
                FriendRequests + "/" + Segment(playerId),
                null,
                BackendAuth.UserId,
                cancellation);
        }

        public UniTask<BackendResult> RemoveFriendAsync(
            string playerId, CancellationToken cancellation)
        {
            return client.CallAsync(
                HttpMethod.Delete,
                Friends + "/" + Segment(playerId),
                null,
                BackendAuth.UserId,
                cancellation);
        }

        /// <remarks>
        /// Anything unrecognised reads as offline rather than throwing. A status
        /// added to the server later would otherwise take the whole friend list
        /// down on the day it shipped, and offline is the reading that promises
        /// the player least.
        /// </remarks>
        private static FriendPresence Presence(string presence)
        {
            switch (presence)
            {
                case "ONLINE": return FriendPresence.Online;
                case "IN_GAME": return FriendPresence.InGame;
                default: return FriendPresence.Offline;
            }
        }

        /// <remarks>
        /// Parsed as UTC explicitly. Without <see cref="DateTimeStyles.AssumeUniversal"/>
        /// the string is read in the machine's own zone, which puts every request
        /// nine hours out here and looks like a plausible time while doing it.
        /// </remarks>
        private static DateTime RequestedAt(string value)
        {
            if (DateTime.TryParseExact(
                    value,
                    TimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }

            Debug.LogWarning($"[Backend] Unreadable request time: {value}");

            // The row is kept. A request whose time will not parse can still be
            // accepted or declined, and dropping it would hide someone who is
            // waiting for an answer.
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }

        /// <remarks>
        /// One malformed row does not discard the rest. The Core types refuse a
        /// blank id or nickname, so building one from a row like that would throw
        /// and lose every row that came after it.
        /// </remarks>
        private static bool Readable(string userId, string nickname, string what)
        {
            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(nickname))
            {
                return true;
            }

            Debug.LogWarning($"[Backend] Skipped a {what} row with no id or nickname.");
            return false;
        }

        /// <remarks>
        /// Escaped for a path segment, not as form data. The two differ on the
        /// space, which <see cref="UnityWebRequest.EscapeURL"/> writes as "+" —
        /// correct inside a query string and a literal plus sign inside a path.
        /// Today's ids are hyphenated hex and come out the same either way; this
        /// is here so they do not have to stay that way.
        /// </remarks>
        private static string Segment(string playerId)
        {
            return Uri.EscapeDataString(playerId ?? string.Empty);
        }
    }
}
