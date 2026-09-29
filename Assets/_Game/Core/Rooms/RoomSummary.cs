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

        /// <summary>
        /// Who opened the room, for the list to credit. Empty when the source
        /// of the listing does not report it.
        /// </summary>
        public string HostNickname { get; }

        /// <summary>
        /// When the room was opened, in seconds since the Unix epoch, so the
        /// list can put the newest first. Zero when the listing does not say,
        /// which sorts such a room to the bottom rather than the top: a room of
        /// unknown age is not a new one.
        /// </summary>
        public int OpenedAt { get; }

        public RoomSummary(
            RoomId id,
            string displayName,
            string mapId,
            int playerCount,
            int maxPlayers,
            bool isLocked,
            bool isOpen,
            RoomStatus status = RoomStatus.Waiting,
            string hostNickname = null,
            int openedAt = 0)
            : this(
                id,
                new RoomSettings(displayName?.Trim(), isLocked, maxPlayers, mapId?.Trim()),
                playerCount,
                isOpen,
                status,
                hostNickname,
                openedAt)
        {
        }

        public RoomSummary(
            string roomId,
            RoomSettings settings,
            int currentPlayerCount,
            bool isOpen,
            RoomStatus status = RoomStatus.Waiting,
            string hostNickname = null,
            int openedAt = 0)
            : this(
                new RoomId(roomId?.Trim()),
                settings,
                currentPlayerCount,
                isOpen,
                status,
                hostNickname,
                openedAt)
        {
        }

        private RoomSummary(
            RoomId id,
            RoomSettings settings,
            int playerCount,
            bool isOpen,
            RoomStatus status,
            string hostNickname,
            int openedAt)
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
            HostNickname = hostNickname?.Trim() ?? string.Empty;
            OpenedAt = openedAt;
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
