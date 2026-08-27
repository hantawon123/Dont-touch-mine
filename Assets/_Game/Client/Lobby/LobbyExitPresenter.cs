using System;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Rooms;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Takes the player out of the lobby and back to the room browser, whether
    /// they asked to go or the room stopped existing under them.
    /// </summary>
    /// <remarks>
    /// Asks <see cref="AppFlowSystem"/> first, so a scene only loads for a move
    /// the app actually allows, the same way the browser handles going back.
    /// <para>
    /// Leaving the screen is not leaving the room. The session has to be told,
    /// and telling it is asynchronous, so that half leaves through
    /// <see cref="LeaveRequested"/> for a layer that can await it.
    /// </para>
    /// </remarks>
    public sealed class LobbyExitPresenter : IStartable, IDisposable
    {
        private readonly LobbyHudView view;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private readonly RoomBrowserSystem roomBrowser;
        private IDisposable exitSubscription;

        public LobbyExitPresenter(
            LobbyHudView view,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow,
            RoomBrowserSystem roomBrowser)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
            this.roomBrowser = roomBrowser
                ?? throw new ArgumentNullException(nameof(roomBrowser));
        }

        /// <summary>The player asked to leave the room, not just this screen.</summary>
        public event Action LeaveRequested;

        public void Start()
        {
            view.LeaveClicked += OnLeaveClicked;
            exitSubscription = roomBrowser.LastExit.Subscribe(OnRoomEnded);
        }

        public void Dispose()
        {
            view.LeaveClicked -= OnLeaveClicked;
            exitSubscription?.Dispose();
        }

        private void OnLeaveClicked()
        {
            // Announced before the screen changes, because this screen is about
            // to go away and the session still has to hear it.
            LeaveRequested?.Invoke();
            ReturnToBrowser();
        }

        /// <summary>
        /// The room ended without this player asking.
        /// </summary>
        /// <remarks>
        /// <see cref="RoomExitReason.Left"/> is this player's own doing and is
        /// already handled where it was asked for. The others arrive unasked,
        /// and a lobby for a room that no longer exists would otherwise sit
        /// there quietly emptying out.
        /// </remarks>
        private void OnRoomEnded(RoomExitReason? reason)
        {
            if (reason == null || reason == RoomExitReason.Left)
            {
                return;
            }

            ReturnToBrowser();
        }

        /// <summary>
        /// A refusal is reported rather than swallowed. Returning in silence
        /// makes a dead Leave button and a hung screen look identical from the
        /// outside, which is exactly what made this hard to place once already.
        /// </summary>
        private void ReturnToBrowser()
        {
            if (appFlow.CurrentState != AppFlowState.RoomBrowser &&
                !appFlow.TryTransitionTo(AppFlowState.RoomBrowser))
            {
                Debug.LogError(
                    "[Lobby] Cannot leave for the room browser from " +
                    $"{appFlow.CurrentState}.");
                return;
            }

            applicationHost.OpenRoomBrowser();
        }
    }
}
