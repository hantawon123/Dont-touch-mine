using System;
using R3;

namespace Game.Core.Lobby
{
    public enum MatchRuleSettingsError
    {
        None,
        InvalidHidingDuration,
        InvalidSearchingDuration,
        InvalidSprintMultiplier,
        InvalidStunHitCount
    }

    public readonly struct MatchRuleSettings
    {
        public const int MinHidingDurationSeconds = 10;
        public const int MaxHidingDurationSeconds = 120;
        public const int DefaultHidingDurationSeconds = 30;
        public const int MinSearchingDurationMinutes = 1;
        public const int MaxSearchingDurationMinutes = 15;
        public const int DefaultSearchingDurationMinutes = 5;
        public const float DefaultSprintMultiplier = 1f;
        public const int MinStunHitCount = 1;
        public const int MaxStunHitCount = 10;
        public const int DefaultStunHitCount = 3;

        private MatchRuleSettings(
            int hidingDurationSeconds,
            int searchingDurationMinutes,
            float sprintMultiplier,
            int stunHitCount,
            string categoryId)
        {
            HidingDurationSeconds = hidingDurationSeconds;
            SearchingDurationMinutes = searchingDurationMinutes;
            SprintMultiplier = sprintMultiplier;
            StunHitCount = stunHitCount;
            CategoryId = categoryId?.Trim() ?? string.Empty;
        }

        public static MatchRuleSettings Default => new(
            DefaultHidingDurationSeconds,
            DefaultSearchingDurationMinutes,
            DefaultSprintMultiplier,
            DefaultStunHitCount,
            string.Empty);

        public int HidingDurationSeconds { get; }
        public int SearchingDurationMinutes { get; }
        public int SearchingDurationSeconds => SearchingDurationMinutes * 60;
        public float SprintMultiplier { get; }
        public int StunHitCount { get; }
        public string CategoryId { get; }
        public bool UsesRandomCategory => string.IsNullOrEmpty(CategoryId);

        public static bool TryCreate(
            int hidingDurationSeconds,
            int searchingDurationMinutes,
            float sprintMultiplier,
            int stunHitCount,
            string categoryId,
            out MatchRuleSettings settings,
            out MatchRuleSettingsError error)
        {
            if (hidingDurationSeconds < MinHidingDurationSeconds ||
                hidingDurationSeconds > MaxHidingDurationSeconds)
            {
                return Fail(MatchRuleSettingsError.InvalidHidingDuration, out settings, out error);
            }

            if (searchingDurationMinutes < MinSearchingDurationMinutes ||
                searchingDurationMinutes > MaxSearchingDurationMinutes)
            {
                return Fail(MatchRuleSettingsError.InvalidSearchingDuration, out settings, out error);
            }

            if (sprintMultiplier != 0.5f && sprintMultiplier != 1f &&
                sprintMultiplier != 1.5f && sprintMultiplier != 2f && sprintMultiplier != 3f)
            {
                return Fail(MatchRuleSettingsError.InvalidSprintMultiplier, out settings, out error);
            }

            if (stunHitCount < MinStunHitCount || stunHitCount > MaxStunHitCount)
            {
                return Fail(MatchRuleSettingsError.InvalidStunHitCount, out settings, out error);
            }

            settings = new MatchRuleSettings(
                hidingDurationSeconds,
                searchingDurationMinutes,
                sprintMultiplier,
                stunHitCount,
                categoryId);
            error = MatchRuleSettingsError.None;
            return true;
        }

        private static bool Fail(
            MatchRuleSettingsError value,
            out MatchRuleSettings settings,
            out MatchRuleSettingsError error)
        {
            settings = default;
            error = value;
            return false;
        }
    }

    public readonly struct PlaySettingsDraft
    {
        public const int MinDestructionLimit = 1;
        public const int MaxDestructionLimit = 10;
        public const int DefaultDestructionLimit = 5;
        public const int UnlimitedDestructionLimit = 0;
        public const int UnlimitedDestructionUses = int.MaxValue;

        public PlaySettingsDraft(
            string title,
            string roomCode,
            bool passwordEnabled,
            string password,
            int maxPlayers,
            int destructionLimit,
            string mapId)
            : this(
                title,
                roomCode,
                passwordEnabled,
                password,
                maxPlayers,
                destructionLimit,
                mapId,
                MatchRuleSettings.Default)
        {
        }

        public PlaySettingsDraft(
            string title,
            string roomCode,
            bool passwordEnabled,
            string password,
            int maxPlayers,
            int destructionLimit,
            string mapId,
            MatchRuleSettings matchRules)
        {
            Title = title?.Trim() ?? string.Empty;
            RoomCode = roomCode?.Trim() ?? string.Empty;
            PasswordEnabled = passwordEnabled;
            Password = passwordEnabled ? password?.Trim() ?? string.Empty : string.Empty;
            MaxPlayers = maxPlayers;
            DestructionLimit = destructionLimit;
            MapId = mapId?.Trim() ?? string.Empty;
            MatchRules = matchRules;
        }

        public string Title { get; }
        public string RoomCode { get; }
        public bool PasswordEnabled { get; }
        public string Password { get; }
        public int MaxPlayers { get; }
        public int DestructionLimit { get; }
        public string MapId { get; }
        public MatchRuleSettings MatchRules { get; }
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
