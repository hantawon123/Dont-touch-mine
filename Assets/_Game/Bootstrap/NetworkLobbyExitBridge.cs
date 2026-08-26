using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Lobby;
using Game.Core.Lobby;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries the lobby's leave request to the session, so leaving the screen
    /// also leaves the Photon room.
    /// </summary>
    /// <remarks>
    /// Without this the player walked back to the browser while their character
    /// stayed spawned, their seat stayed taken and everyone still in the room
    /// kept seeing them on the list.
    /// </remarks>
    public sealed class NetworkLobbyExitBridge : IStartable, IDisposable
    {
        private readonly LobbyExitPresenter exit;
        private readonly RoomUiCommands commands;

        public NetworkLobbyExitBridge(LobbyExitPresenter exit, RoomUiCommands commands)
        {
            this.exit = exit ?? throw new ArgumentNullException(nameof(exit));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public void Start()
        {
            exit.LeaveRequested += OnLeaveRequested;
        }

        public void Dispose()
        {
            exit.LeaveRequested -= OnLeaveRequested;
        }

        private void OnLeaveRequested()
        {
            Leave().Forget();
        }

        /// <remarks>
        /// Deliberately uncancellable. The screen that asked is being unloaded
        /// in the same frame, so a token tied to it would cancel the departure
        /// halfway and leave the room believing this player is still in it. The
        /// runner outlives the scene, so the call finishes on its own.
        /// </remarks>
        private async UniTaskVoid Leave()
        {
            try
            {
                await commands.LeaveAsync(CancellationToken.None);
            }
            catch (Exception failure)
            {
                Debug.LogError($"[Rooms] Could not leave the room: {failure.Message}");
            }
        }
    }
}
