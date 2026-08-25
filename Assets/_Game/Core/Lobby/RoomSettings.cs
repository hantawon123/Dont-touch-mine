using System;

namespace Game.Core.Lobby
{
    public enum RoomSettingsError
    {
        None,
        TitleRequired,
        PasswordRequired,
        InvalidPlayerCount,
        MapRequired
    }

    public enum RoomJoinRequestError
    {
        None,
        RoomIdRequired,
        PasswordRequired
    }

    public enum RoomStatus
    {
        Waiting,
        Playing
    }

    public readonly struct RoomJoinRequest
    {
        public RoomJoinRequest(string roomId, string password)
        {
            RoomId = roomId?.Trim();
            Password = password;
        }

        public string RoomId { get; }
        public string Password { get; }

        public bool TryValidate(
            bool passwordRequired,
            out RoomJoinRequestError error)
        {
            if (string.IsNullOrWhiteSpace(RoomId))
            {
                error = RoomJoinRequestError.RoomIdRequired;
                return false;
            }

            if (passwordRequired && string.IsNullOrWhiteSpace(Password))
            {
                error = RoomJoinRequestError.PasswordRequired;
                return false;
            }

            error = RoomJoinRequestError.None;
            return true;
        }
    }

    public readonly struct RoomCreateRequest
    {
        public RoomCreateRequest(
            string title,
            bool isLocked,
            string password,
            int maxPlayers,
            string mapId)
        {
            Title = title;
            IsLocked = isLocked;
            Password = isLocked ? password : null;
            MaxPlayers = maxPlayers;
            MapId = mapId;
        }

        public string Title { get; }
        public bool IsLocked { get; }
        public string Password { get; }
        public int MaxPlayers { get; }
        public string MapId { get; }

        public bool TryCreateSettings(
            int maxSupportedPlayerCount,
            out RoomSettings settings,
            out RoomSettingsError error)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                settings = default;
                error = RoomSettingsError.TitleRequired;
                return false;
            }

            if (IsLocked && string.IsNullOrWhiteSpace(Password))
            {
                settings = default;
                error = RoomSettingsError.PasswordRequired;
                return false;
            }

            if (maxSupportedPlayerCount < RoomSettings.MinPlayerCount ||
                MaxPlayers < RoomSettings.MinPlayerCount ||
                MaxPlayers > Math.Min(RoomSettings.MaxPlayerCount, maxSupportedPlayerCount))
            {
                settings = default;
                error = RoomSettingsError.InvalidPlayerCount;
                return false;
            }

            if (string.IsNullOrWhiteSpace(MapId))
            {
                settings = default;
                error = RoomSettingsError.MapRequired;
                return false;
            }

            settings = new RoomSettings(
                Title.Trim(),
                IsLocked,
                MaxPlayers,
                MapId.Trim());
            error = RoomSettingsError.None;
            return true;
        }
    }

    public readonly struct RoomSettings
    {
        public const int MinPlayerCount = 2;
        public const int MaxPlayerCount = 6;

        internal RoomSettings(string title, bool isLocked, int maxPlayers, string mapId)
        {
            Title = title;
            IsLocked = isLocked;
            MaxPlayers = maxPlayers;
            MapId = mapId;
        }

        public string Title { get; }
        public bool IsLocked { get; }
        public int MaxPlayers { get; }
        public string MapId { get; }

        internal bool IsValid =>
            !string.IsNullOrWhiteSpace(Title) &&
            MaxPlayers >= MinPlayerCount &&
            MaxPlayers <= MaxPlayerCount &&
            !string.IsNullOrWhiteSpace(MapId);
    }

    public readonly struct RoomSummary
    {
        public RoomSummary(
            string roomId,
            RoomSettings settings,
            int currentPlayerCount,
            bool isOpen,
            RoomStatus status = RoomStatus.Waiting)
        {
            if (!settings.IsValid)
            {
                throw new ArgumentException("Room settings are invalid.", nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Room id is required.", nameof(roomId));
            }

            if (currentPlayerCount < 0 || currentPlayerCount > settings.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(currentPlayerCount));
            }

            RoomId = roomId.Trim();
            Settings = settings;
            CurrentPlayerCount = currentPlayerCount;
            IsOpen = isOpen;
            Status = status;
        }

        public string RoomId { get; }
        public RoomSettings Settings { get; }
        public int CurrentPlayerCount { get; }
        public bool IsOpen { get; }
        public RoomStatus Status { get; }
        public bool CanJoin =>
            Status == RoomStatus.Waiting &&
            IsOpen &&
            CurrentPlayerCount < Settings.MaxPlayers;

        public bool MatchesTitle(string searchText) =>
            string.IsNullOrWhiteSpace(searchText) ||
            Settings.Title.IndexOf(
                searchText.Trim(),
                StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
