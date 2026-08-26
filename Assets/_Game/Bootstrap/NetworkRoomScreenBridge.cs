using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Rooms;
using Game.Core.Lobby;
using Game.Core.Rooms;
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
        private readonly RoomUiCommands commands;
        private readonly CancellationTokenSource cancellation =
            new CancellationTokenSource();

        public NetworkRoomScreenBridge(RoomScreenPresenter screen, RoomUiCommands commands)
        {
            this.screen = screen ?? throw new ArgumentNullException(nameof(screen));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public void Start()
        {
            screen.RoomCreateRequested += OnCreateRequested;
            screen.RoomJoinRequested += OnJoinRequested;
            Refresh().Forget();
        }

        public void Dispose()
        {
            screen.RoomCreateRequested -= OnCreateRequested;
            screen.RoomJoinRequested -= OnJoinRequested;

            cancellation.Cancel();
            cancellation.Dispose();
        }

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
            try
            {
                await commands.RefreshAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // The screen closed while connecting. Nothing to report.
            }
            catch (Exception failure)
            {
                Debug.LogError($"[Rooms] Could not reach the room list: {failure.Message}");
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
                await commands.CreateAsync(request, cancellation.Token);
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
                await commands.EnterAsync(room, password, cancellation.Token);
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
