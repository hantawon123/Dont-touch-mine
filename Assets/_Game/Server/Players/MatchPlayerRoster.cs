using System;
using System.Collections.Generic;
using Game.SOAP.Config;

namespace Game.Server.Players
{
    public readonly struct MatchPlayerState
    {
        internal MatchPlayerState(int playerIndex, string playerId, bool isActive)
        {
            PlayerIndex = playerIndex;
            PlayerId = playerId;
            IsActive = isActive;
        }

        public int PlayerIndex { get; }
        public string PlayerId { get; }
        public bool IsActive { get; }
    }

    public sealed class MatchPlayerRoster
    {
        private readonly MatchPlayerState[] players;
        private readonly Dictionary<string, int> playerIndexById;

        public MatchPlayerRoster(IReadOnlyList<string> participantIds)
        {
            if (participantIds == null)
            {
                throw new ArgumentNullException(nameof(participantIds));
            }

            if (participantIds.Count < MatchRulesSO.MinPlayerCount ||
                participantIds.Count > MatchRulesSO.MaxPlayerCount)
            {
                throw new ArgumentException(
                    $"Between {MatchRulesSO.MinPlayerCount} and " +
                    $"{MatchRulesSO.MaxPlayerCount} participants are required.",
                    nameof(participantIds));
            }

            players = new MatchPlayerState[participantIds.Count];
            playerIndexById = new Dictionary<string, int>(
                participantIds.Count,
                StringComparer.Ordinal);

            for (var playerIndex = 0; playerIndex < participantIds.Count; playerIndex++)
            {
                var playerId = participantIds[playerIndex]?.Trim();
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    throw new ArgumentException(
                        "Every participant requires a player id.",
                        nameof(participantIds));
                }

                if (!playerIndexById.TryAdd(playerId, playerIndex))
                {
                    throw new ArgumentException(
                        $"Duplicate player id: {playerId}",
                        nameof(participantIds));
                }

                players[playerIndex] = new MatchPlayerState(playerIndex, playerId, true);
            }

            Players = Array.AsReadOnly(players);
            ActivePlayerCount = players.Length;
        }

        public IReadOnlyList<MatchPlayerState> Players { get; }
        public int ActivePlayerCount { get; private set; }

        public MatchPlayerState GetPlayer(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= players.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            return players[playerIndex];
        }

        public bool IsActive(int playerIndex)
        {
            return GetPlayer(playerIndex).IsActive;
        }

        public bool TryDeactivate(int playerIndex)
        {
            var player = GetPlayer(playerIndex);
            if (!player.IsActive)
            {
                return false;
            }

            players[playerIndex] = new MatchPlayerState(
                player.PlayerIndex,
                player.PlayerId,
                false);
            ActivePlayerCount--;
            return true;
        }

        public bool TryGetPlayerIndex(string playerId, out int playerIndex)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                playerIndex = -1;
                return false;
            }

            if (playerIndexById.TryGetValue(playerId.Trim(), out playerIndex))
            {
                return true;
            }

            playerIndex = -1;
            return false;
        }
    }
}
