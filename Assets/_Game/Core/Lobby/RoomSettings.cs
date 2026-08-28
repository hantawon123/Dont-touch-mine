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

    public enum RoomStatus
    {
        Waiting,
        Playing
    }

    public readonly struct RoomSettings
    {
        public const int MinMatchPlayerCount = 1;
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
