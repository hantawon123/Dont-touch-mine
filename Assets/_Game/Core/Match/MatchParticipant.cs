using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;

namespace Game.Core.Match
{
    public readonly struct MatchParticipant
    {
        public MatchParticipant(string playerId, int playerIndex)
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
        }

        public string PlayerId { get; }

        /// <summary>
        /// Stable zero-based index used by match arrays and network state.
        /// </summary>
        public int PlayerIndex { get; }

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
                    playerIndex);
            }

            return matchParticipants;
        }
    }
}
