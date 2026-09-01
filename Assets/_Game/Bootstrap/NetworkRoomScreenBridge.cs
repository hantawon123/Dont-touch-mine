using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Rooms;
using Game.Core.Lobby;
using Game.Core.Rooms;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries the browser screen's create and enter requests to the session,
    /// and joins the Photon lobby so there is a room list to show at all.
    /// </summary>
    /// <remarks>
    /// The screen raises events and never touches the session, so this is where
    /// the two are tied together. Without it the requests reached nothing: the
    /// screen listed rooms it had invented and entered them locally.
    /// <para>
    /// The refresh is what connects. <c>RoomBrowser.RefreshAsync</c> joins the
    /// lobby when this peer is not in it yet, and Photon pushes the list from
    /// then on, so nothing here polls.
    /// </para>
    /// </remarks>
    public sealed class NetworkRoomScreenBridge : IStartable, IDisposable
    {
        private readonly RoomScreenPresenter screen;
        private readonly IRoomBrowserView browserView;
        private readonly RoomUiCommands commands;
        private readonly NetworkRunnerService network;
        private bool isRefreshing;

        public NetworkRoomScreenBridge(
            RoomScreenPresenter screen,
            IRoomBrowserView browserView,
            RoomUiCommands commands,
            NetworkRunnerService network)
        {
            this.screen = screen ?? throw new ArgumentNullException(nameof(screen));
            this.browserView = browserView ?? throw new ArgumentNullException(nameof(browserView));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public void Start()
        {
            screen.RoomCreateRequested += OnCreateRequested;
            screen.RoomJoinRequested += OnJoinRequested;
            browserView.RefreshRequested += OnRefreshRequested;
            browserView.CreateRoomRequested += OnCreateFormOpened;
            Refresh().Forget();
        }

        /// <summary>
        /// Lets go of the screen's requests. The project-owned matchmaking
        /// connection is intentionally left alive for the room session to reuse.
        /// </summary>
        /// <remarks>
        /// The session service outlives this scene. Detaching the handlers is
        /// sufficient; it stops this view reacting without tearing down the
        /// Photon connection during scene unload.
        /// </remarks>
        public void Dispose()
        {
            screen.RoomCreateRequested -= OnCreateRequested;
            screen.RoomJoinRequested -= OnJoinRequested;
            browserView.RefreshRequested -= OnRefreshRequested;
            browserView.CreateRoomRequested -= OnCreateFormOpened;
        }

        private void OnRefreshRequested() => Refresh().Forget();

        private void OnCreateFormOpened() => network.PrepareLobbyScene();

        private void OnCreateRequested(RoomCreateRequest request)
        {
            Create(request).Forget();
        }

        private void OnJoinRequested(RoomId room, string password)
        {
            Enter(room, password).Forget();
        }

        private async UniTaskVoid Refresh()
        {
            if (isRefreshing)
            {
                return;
            }

            isRefreshing = true;
            try
            {
                await commands.RefreshAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // The screen closed while connecting. Nothing to report.
            }
            catch (Exception failure)
            {
                Debug.LogError($"[Rooms] Could not reach the room list: {failure.Message}");
            }
            finally
            {
                isRefreshing = false;
            }
        }

        /// <summary>
        /// The outcome is not returned anywhere: it is recorded on
        /// <see cref="RoomBrowserSystem"/>, which the screen already watches.
        /// </summary>
        private async UniTaskVoid Create(RoomCreateRequest request)
        {
            try
            {
                await commands.CreateAsync(request, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception failure)
            {
                Debug.LogError($"[Rooms] Could not open the room: {failure.Message}");
            }
        }

        private async UniTaskVoid Enter(RoomId room, string password)
        {
            try
            {
                await commands.EnterAsync(room, password, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception failure)
            {
                Debug.LogError($"[Rooms] Could not enter the room: {failure.Message}");
            }
        }
    }
}
