using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the realtime link to the backend alive for the life of the
    /// application, and catches the friend list up whenever the link comes back.
    /// </summary>
    /// <remarks>
    /// Starts after sign-in, because the link's first word is who this client
    /// is. Then it runs until the application ends: scenes come and go around
    /// it, which is why it lives in the project scope rather than the home
    /// scope — a player who is in a match still wants their friend list to be
    /// right when they return.
    /// <para>
    /// The catch-up here is the friend list only, because that is the one list
    /// with a store that outlives a screen. Requests and invites are re-read by
    /// the screens that show them, each watching the same
    /// <see cref="NotificationLinkState.Connected"/> edge.
    /// </para>
    /// </remarks>
    public sealed class NotificationLink : IAsyncStartable, IDisposable
    {
        private readonly WebSocketNotificationStream stream;
        private readonly BackendSignIn signIn;
        private readonly FriendUiCommands friends;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private IDisposable catchUp;

        public NotificationLink(
            WebSocketNotificationStream stream,
            BackendSignIn signIn,
            FriendUiCommands friends)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (!await signIn.Ready)
            {
                // No account, so nothing to say HELLO as. The game runs without
                // the channel the way it runs without the friend panel.
                Debug.Log("[Notifications] No account; the realtime link stays down.");
                return;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, lifetime.Token);

            // Forced, because a refresh may already be in flight from a screen
            // with its own token, and a coalesced call would return that one
            // instead of reading again after the gap.
            catchUp = stream.State
                .Where(state => state == NotificationLinkState.Connected)
                .Subscribe(_ => friends.RefreshFriendsAsync(lifetime.Token, force: true).Forget());

            await stream.RunAsync(linked.Token);
        }

        public void Dispose()
        {
            lifetime.Cancel();
            catchUp?.Dispose();
            stream.Dispose();
            lifetime.Dispose();
        }
    }
}
