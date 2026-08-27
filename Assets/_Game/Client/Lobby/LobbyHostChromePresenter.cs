using System;
using Game.Core.Lobby;
using R3;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Shows the host-only parts of the always-on HUD to the host and nobody
    /// else. Asking to start is not here: that button lives in the Esc menu,
    /// where a released cursor can reach it.
    /// </summary>
    public sealed class LobbyHostChromePresenter : IStartable, IDisposable
    {
        private readonly ILobbyHostSession hostSession;
        private readonly LobbyHudView hudView;
        private IDisposable subscription;

        public LobbyHostChromePresenter(ILobbyHostSession hostSession, LobbyHudView hudView)
        {
            this.hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
            this.hudView = hudView ?? throw new ArgumentNullException(nameof(hudView));
        }

        public void Start()
        {
            subscription = hostSession.IsLocalHost.Subscribe(hudView.SetHostControlsVisible);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
