using System;
using Game.Core.Lobby;
using R3;
using VContainer.Unity;

namespace Game.Client.Lobby
{
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

        /// <remarks>
        /// The button only asks. Taking everyone into the map is the authority's
        /// job and already happens once the line-up is confirmed, so loading a
        /// scene here would move whoever clicked on ahead of the others and race
        /// the networked load on the authority's own screen.
        /// </remarks>
        public void Start()
        {
            subscription = hostSession.IsLocalHost.Subscribe(hudView.SetHostControlsVisible);
            hudView.StartClicked += hostSession.RequestStart;
        }

        public void Dispose()
        {
            hudView.StartClicked -= hostSession.RequestStart;
            subscription?.Dispose();
        }
    }
}
