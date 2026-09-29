using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Lobby;
using Game.Core.Backend;
using Game.Core.Home;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Reloads the shared friend store when the lobby opens, and keeps
    /// presence current while the 2-key roster is on screen.
    /// </summary>
    /// <remarks>
    /// Presence is not pushed. Home already polls while its friend panel is
    /// open; the lobby does the same for the player modal so a friend walking
    /// into a room or a match moves between the three sections without the
    /// host having to close and reopen it.
    /// </remarks>
    public sealed class LobbyFriendRefresh : IStartable, ITickable, IDisposable
    {
        public const float PresenceRefreshInterval = 3f;

        private readonly FriendUiCommands friends;
        private readonly ILobbyShortcutOverlay shortcuts;
        private readonly CancellationTokenSource lifetime = new();
        private float presenceElapsed;
        private bool refreshingPresence;
        private bool wasPlayerListOpen;

        public LobbyFriendRefresh(FriendUiCommands friends, ILobbyShortcutOverlay shortcuts)
        {
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
            this.shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));
        }

        public void Start()
        {
            friends.RefreshFriendsAsync(lifetime.Token).Forget();
        }

        public void Tick() => Advance(Time.unscaledDeltaTime);

        public void Advance(float unscaledDeltaTime)
        {
            if (lifetime.IsCancellationRequested)
            {
                return;
            }

            if (!PlayerListOpen)
            {
                presenceElapsed = 0f;
                wasPlayerListOpen = false;
                return;
            }

            if (!wasPlayerListOpen)
            {
                wasPlayerListOpen = true;
                presenceElapsed = 0f;
                RefreshPresenceAsync().Forget();
                return;
            }

            if (refreshingPresence)
            {
                return;
            }

            presenceElapsed += unscaledDeltaTime;
            if (presenceElapsed < PresenceRefreshInterval)
            {
                return;
            }

            presenceElapsed = 0f;
            RefreshPresenceAsync().Forget();
        }

        public void Dispose()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private bool PlayerListOpen =>
            shortcuts.IsOpen && shortcuts.OpenKind == LobbyShortcutKind.Players;

        private async UniTaskVoid RefreshPresenceAsync()
        {
            refreshingPresence = true;
            try
            {
                var failure = await friends.RefreshFriendsAsync(lifetime.Token);
                if (failure != BackendFailure.None && failure != BackendFailure.Cancelled)
                {
                    Debug.LogWarning($"[Friends] Lobby presence refresh failed: {failure}.");
                }
            }
            finally
            {
                refreshingPresence = false;
            }
        }
    }
}
