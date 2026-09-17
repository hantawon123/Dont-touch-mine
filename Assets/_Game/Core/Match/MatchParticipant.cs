using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;

namespace Game.Core.Match
{
    public readonly struct MatchParticipant
    {
        /// <param name="userId">
        /// The backend account, or null or empty when the player did not sign
        /// in. Kept optional so the many places that only reason about seats and
        /// indices need not invent one.
        /// </param>
        public MatchParticipant(string playerId, int playerIndex, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            if (playerIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            PlayerId = playerId.Trim();
            PlayerIndex = playerIndex;
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        }

        public string PlayerId { get; }

        /// <summary>
        /// Stable zero-based index used by match arrays and network state.
        /// </summary>
        public int PlayerIndex { get; }

        /// <summary>
        /// The backend account behind this participant, or null when there is
        /// none — a player who never signed in.
        /// </summary>
        /// <remarks>
        /// Null rather than empty, unlike <c>RoomParticipant.UserId</c>, because
        /// this is the value the host puts on the wire to the backend, and the
        /// backend reads a missing account as null. Deciding that here means no
        /// publisher has to remember to translate.
        /// <para>
        /// <see cref="PlayerId"/> stays the key for seats and authority. This is
        /// the key for anything that follows a person past the end of the room.
        /// </para>
        /// </remarks>
        public string UserId { get; }

        /// <summary>
        /// Converts the room roster to a contiguous line-up in seat order.
        /// Play order is shuffled later, at match confirm, so hiding turns
        /// are not join or seat order.
        /// </summary>
        public static MatchParticipant[] FromRoomParticipants(
            IReadOnlyList<RoomParticipant> roomParticipants)
        {
            if (roomParticipants == null)
            {
                throw new ArgumentNullException(nameof(roomParticipants));
            }

            if (roomParticipants.Count < RoomSettings.MinMatchPlayerCount ||
                roomParticipants.Count > RoomSettings.MaxPlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(roomParticipants));
            }

            var ordered = new RoomParticipant[roomParticipants.Count];
            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            var seats = new HashSet<int>();

            for (var index = 0; index < roomParticipants.Count; index++)
            {
                var participant = roomParticipants[index];
                if (string.IsNullOrWhiteSpace(participant.PlayerId) ||
                    participant.Seat < 0 ||
                    participant.Seat >= RoomSettings.MaxPlayerCount ||
                    !playerIds.Add(participant.PlayerId.Trim()) ||
                    !seats.Add(participant.Seat))
                {
                    throw new ArgumentException(
                        "Room participants require unique player ids and non-negative seats.",
                        nameof(roomParticipants));
                }

                ordered[index] = participant;
            }

            Array.Sort(ordered, (left, right) => left.Seat.CompareTo(right.Seat));
            var matchParticipants = new MatchParticipant[ordered.Length];
            for (var playerIndex = 0; playerIndex < ordered.Length; playerIndex++)
            {
                matchParticipants[playerIndex] = new MatchParticipant(
                    ordered[playerIndex].PlayerId,
                    playerIndex,
                    ordered[playerIndex].UserId);
            }

            return matchParticipants;
        }

        /// <summary>
        /// Randomizes play order so hiding turns are not join/seat order.
        /// Each player's id stays paired with the same user id.
        /// </summary>
        public static void ShufflePlayOrder(string[] playerIds, string[] userIds, Random random)
        {
            if (playerIds == null)
            {
                throw new ArgumentNullException(nameof(playerIds));
            }

            if (userIds == null)
            {
                throw new ArgumentNullException(nameof(userIds));
            }

            if (playerIds.Length != userIds.Length)
            {
                throw new ArgumentException(
                    "Player ids and user ids must be the same length.",
                    nameof(userIds));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            for (var index = playerIds.Length - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (playerIds[index], playerIds[swap]) = (playerIds[swap], playerIds[index]);
                (userIds[index], userIds[swap]) = (userIds[swap], userIds[index]);
            }
        }
    }
}
