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

    public sealed class LobbyHostSession : ILobbyHostSession, IDisposable
    {
        private readonly ReactiveProperty<bool> isLocalHost;
        private readonly ReactiveProperty<PlaySettingsDraft> settings;

        public LobbyHostSession(
            string localPlayerId,
            bool isLocalHost,
            PlaySettingsDraft initialSettings)
        {
            if (string.IsNullOrWhiteSpace(localPlayerId))
            {
                throw new ArgumentException("Local player id is required.", nameof(localPlayerId));
            }

            LocalPlayerId = localPlayerId.Trim();
            this.isLocalHost = new ReactiveProperty<bool>(isLocalHost);
            settings = new ReactiveProperty<PlaySettingsDraft>(initialSettings);
        }

        public string LocalPlayerId { get; }
        public ReadOnlyReactiveProperty<bool> IsLocalHost => isLocalHost;
        public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;

        public event Action StartRequested;
        public event Action<string> KickRequested;
        public event Action<string> HostTransferRequested;
        public event Action<PlaySettingsDraft> SettingsApplyRequested;

        public void SetLocalHost(bool value) => isLocalHost.Value = value;

        public void ReplaceSettings(PlaySettingsDraft next) => settings.Value = next;

        public void RequestStart()
        {
            if (!isLocalHost.CurrentValue)
            {
                return;
            }

            StartRequested?.Invoke();
        }

        public void RequestKick(string playerId)
        {
            if (!isLocalHost.CurrentValue || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (string.Equals(playerId.Trim(), LocalPlayerId, StringComparison.Ordinal))
            {
                return;
            }

            KickRequested?.Invoke(playerId.Trim());
        }

        public void RequestHostTransfer(string playerId)
        {
            if (!isLocalHost.CurrentValue || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (string.Equals(playerId.Trim(), LocalPlayerId, StringComparison.Ordinal))
            {
                return;
            }

            HostTransferRequested?.Invoke(playerId.Trim());
        }

        public void RequestApplySettings(PlaySettingsDraft draft)
        {
            if (!isLocalHost.CurrentValue)
            {
                return;
            }

            SettingsApplyRequested?.Invoke(draft);
            settings.Value = draft;
        }

        public void Dispose()
        {
            isLocalHost.Dispose();
            settings.Dispose();
        }
    }
}
