using System;
using Game.Core.Lobby;

namespace Game.Core.Rooms
{
    /// <summary>
    /// One row of the room list, in terms presentation can render directly.
    /// </summary>
    /// <remarks>
    /// Carries no networking types and no address: <see cref="Id"/> is opaque
    /// and the room code is absent on purpose, since knowing a code is what
    /// lets someone enter a locked room.
    /// </remarks>
    public readonly struct RoomSummary
    {
        public RoomId Id { get; }
        public string RoomId => Id.Value;
        public RoomSettings Settings { get; }
        public string DisplayName => Settings.Title;
        public string MapId => Settings.MapId;
        public int PlayerCount { get; }
        public int CurrentPlayerCount => PlayerCount;
        public int MaxPlayers => Settings.MaxPlayers;
        public bool IsLocked => Settings.IsLocked;
        public bool IsOpen { get; }
        public RoomStatus Status { get; }

        public RoomSummary(
            RoomId id,
            string displayName,
            string mapId,
            int playerCount,
            int maxPlayers,
            bool isLocked,
            bool isOpen,
            RoomStatus status = RoomStatus.Waiting)
            : this(
                id,
                new RoomSettings(displayName?.Trim(), isLocked, maxPlayers, mapId?.Trim()),
                playerCount,
                isOpen,
                status)
        {
        }

        public RoomSummary(
            string roomId,
            RoomSettings settings,
            int currentPlayerCount,
            bool isOpen,
            RoomStatus status = RoomStatus.Waiting)
            : this(new RoomId(roomId?.Trim()), settings, currentPlayerCount, isOpen, status)
        {
        }

        private RoomSummary(
            RoomId id,
            RoomSettings settings,
            int playerCount,
            bool isOpen,
            RoomStatus status)
        {
            if (!settings.IsValid)
            {
                throw new ArgumentException("Room settings are invalid.", nameof(settings));
            }

            if (!id.IsValid || string.IsNullOrWhiteSpace(id.Value))
            {
                throw new ArgumentException("Room id is required.", nameof(id));
            }

            if (playerCount < 0 || playerCount > settings.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            Id = id;
            Settings = settings;
            PlayerCount = playerCount;
            IsOpen = isOpen;
            Status = status;
        }

        public bool IsFull => MaxPlayers > 0 && PlayerCount >= MaxPlayers;
        public bool CanJoin => Status == RoomStatus.Waiting && IsOpen && !IsFull;
        public bool CanEnter => CanJoin;

        public bool MatchesTitle(string searchText) =>
            string.IsNullOrWhiteSpace(searchText) ||
            DisplayName.IndexOf(
                searchText.Trim(),
                StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
