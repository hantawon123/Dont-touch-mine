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

}
