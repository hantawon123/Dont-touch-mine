using System;
using Game.Client.Home;
using Game.Core.Flow;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Takes the player out of the lobby and back to the room browser.
    /// </summary>
    /// <remarks>
    /// Asks <see cref="AppFlowSystem"/> first, so a scene only loads for a move
    /// the app actually allows, the same way the browser handles going back.
    /// </remarks>
    public sealed class LobbyExitPresenter : IStartable, IDisposable
    {
        private readonly LobbyHudView view;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;

        public LobbyExitPresenter(
            LobbyHudView view,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
        }

        public void Start()
        {
            view.LeaveClicked += OnLeaveRequested;
        }

        public void Dispose()
        {
            view.LeaveClicked -= OnLeaveRequested;
        }

        private void OnLeaveRequested()
        {
            if (appFlow.CurrentState != AppFlowState.RoomBrowser &&
                !appFlow.TryTransitionTo(AppFlowState.RoomBrowser))
            {
                return;
            }

            applicationHost.OpenRoomBrowser();
        }
    }
}
