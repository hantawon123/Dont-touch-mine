using System;
using Game.Core.Lobby;
using R3;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    public sealed class LobbyHostChromePresenter : IStartable, IDisposable
    {
        public const string PlaygroundSceneName = "Playground";

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
            hudView.StartClicked += hostSession.RequestStart;
            hostSession.StartRequested += HandleStartRequested;
        }

        public void Dispose()
        {
            hudView.StartClicked -= hostSession.RequestStart;
            hostSession.StartRequested -= HandleStartRequested;
            subscription?.Dispose();
        }

        private static void HandleStartRequested()
        {
            SceneManager.LoadScene(PlaygroundSceneName);
        }
    }
}
