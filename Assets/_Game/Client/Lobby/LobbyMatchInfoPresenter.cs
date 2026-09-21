using System;
using Game.Core.Lobby;
using Game.Core.Maps;
using R3;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Keeps the lobby's always-on category/map card in sync with room settings.
    /// </summary>
    public sealed class LobbyMatchInfoPresenter : IStartable, IDisposable
    {
        private readonly ILobbyHostSession hostSession;
        private readonly LobbyHudView hud;
        private IDisposable settingsSubscription;

        public LobbyMatchInfoPresenter(ILobbyHostSession hostSession, LobbyHudView hud)
        {
            this.hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
        }

        public void Start()
        {
            settingsSubscription = hostSession.Settings.Subscribe(Apply);
        }

        public void Dispose()
        {
            settingsSubscription?.Dispose();
        }

        private void Apply(PlaySettingsDraft draft)
        {
            hud.SetMatchInfo(
                CategoryLabel(draft),
                MapLabel(draft),
                MapPreviewSprites.For(draft.MapId),
                MapCatalog.IsRandom(draft.MapId));
        }

        private static string CategoryLabel(PlaySettingsDraft draft)
        {
            var index = PlaySettingsCategoryCatalog.IndexOf(draft.MatchRules.CategoryId);
            return PlaySettingsCategoryCatalog.GetOption(
                index < 0 ? PlaySettingsCategoryCatalog.DefaultIndex : index).Label;
        }

        private static string MapLabel(PlaySettingsDraft draft)
        {
            var index = PlaySettingsMapCatalog.IndexOf(draft.MapId);
            if (index >= 0)
            {
                return PlaySettingsMapCatalog.GetOption(index).Label;
            }

            var mapId = draft.MapId?.Trim() ?? string.Empty;
            return mapId.Length == 0
                ? PlaySettingsMapCatalog.RandomLabel
                : mapId;
        }
    }
}
