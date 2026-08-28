using System;
using R3;

namespace Game.Core.Lobby
{
    public readonly struct PlaySettingsDraft
    {
        public const int MinDestructionLimit = 1;
        public const int MaxDestructionLimit = 10;
        public const int DefaultDestructionLimit = 5;

        public PlaySettingsDraft(
            string title,
            string roomCode,
            bool passwordEnabled,
            string password,
            int maxPlayers,
            int destructionLimit,
            string mapId)
        {
            Title = title?.Trim() ?? string.Empty;
            RoomCode = roomCode?.Trim() ?? string.Empty;
            PasswordEnabled = passwordEnabled;
            Password = passwordEnabled ? password?.Trim() ?? string.Empty : string.Empty;
            MaxPlayers = maxPlayers;
            DestructionLimit = destructionLimit;
            MapId = mapId?.Trim() ?? string.Empty;
        }

        public string Title { get; }
        public string RoomCode { get; }
        public bool PasswordEnabled { get; }
        public string Password { get; }
        public int MaxPlayers { get; }
        public int DestructionLimit { get; }
        public string MapId { get; }
    }

    public interface ILobbyHostSession
    {
        string LocalPlayerId { get; }
        ReadOnlyReactiveProperty<bool> IsLocalHost { get; }
        ReadOnlyReactiveProperty<PlaySettingsDraft> Settings { get; }

        event Action StartRequested;
        event Action<string> KickRequested;
        event Action<string> HostTransferRequested;
        event Action<PlaySettingsDraft> SettingsApplyRequested;

        void SetLocalHost(bool isLocalHost);
        void ReplaceSettings(PlaySettingsDraft settings);
        void RequestStart();
        void RequestKick(string playerId);
        void RequestHostTransfer(string playerId);
        void RequestApplySettings(PlaySettingsDraft settings);
    }

}
