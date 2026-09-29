using System;
using Game.Core.Lobby;

namespace Game.Core.Rooms
{
    /// <summary>
    /// What the host chose when opening a room.
    /// </summary>
    /// <remarks>
    /// Carries no room code: the code is issued by the layer that opens the
    /// room and comes back in <see cref="RoomEntryResult"/>.
    /// </remarks>
    public readonly struct RoomCreateRequest
    {
        public RoomCreateRequest(
            string title,
            bool isLocked,
            string password,
            int maxPlayers,
            string mapId,
            bool isPrivate = false)
        {
            Title = title;
            IsLocked = isLocked;
            IsPrivate = isPrivate;
            Password = isLocked ? password : null;
            MapId = mapId;
            MaxPlayers = maxPlayers;
        }

        public string Title { get; }
        public string DisplayName => Title;
        public bool IsLocked { get; }
        public bool IsPrivate { get; }
        public string Password { get; }
        public string MapId { get; }
        public int MaxPlayers { get; }

        public bool TryCreateSettings(
            int maxSupportedPlayerCount,
            out RoomSettings settings,
            out RoomSettingsError error)
        {
            if (!RoomSettings.IsValidTitle(Title))
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

            settings = new RoomSettings(
                Title.Trim(),
                IsLocked,
                MaxPlayers,
                MapId?.Trim() ?? string.Empty);
            error = RoomSettingsError.None;
            return true;
        }
    }
}
