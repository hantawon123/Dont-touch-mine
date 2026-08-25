using System;

namespace Game.Core.Lobby
{
    public enum RoomStartResult
    {
        Started,
        NotHost,
        NotEnoughPlayers,
        AlreadyStarted
    }

    public sealed class RoomLobbySystem
    {
        private string hostPlayerId;

        public RoomLobbySystem(
            RoomSettings settings,
            string hostPlayerId,
            int currentPlayerCount)
        {
            if (!settings.IsValid)
            {
                throw new ArgumentException("Room settings are invalid.", nameof(settings));
            }

            Settings = settings;
            UpdateHost(hostPlayerId);
            UpdatePlayerCount(currentPlayerCount);
        }

        public RoomSettings Settings { get; }
        public int CurrentPlayerCount { get; private set; }
        public bool IsStarted { get; private set; }

        public event Action<RoomSettings> Started;

        public void UpdateHost(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Host player id is required.", nameof(playerId));
            }

            hostPlayerId = playerId.Trim();
        }

        public void UpdatePlayerCount(int playerCount)
        {
            if (playerCount < 0 || playerCount > Settings.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            CurrentPlayerCount = playerCount;
        }

        public RoomStartResult TryStart(string requesterPlayerId)
        {
            if (IsStarted)
            {
                return RoomStartResult.AlreadyStarted;
            }

            if (!string.Equals(
                    requesterPlayerId,
                    hostPlayerId,
                    StringComparison.Ordinal))
            {
                return RoomStartResult.NotHost;
            }

            if (CurrentPlayerCount < RoomSettings.MinPlayerCount)
            {
                return RoomStartResult.NotEnoughPlayers;
            }

            IsStarted = true;
            Started?.Invoke(Settings);
            return RoomStartResult.Started;
        }
    }
}
