using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Home;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries the home screen's friend requests to the backend, and the
    /// backend's answers back to the screen.
    /// </summary>
    /// <remarks>
    /// The screen raises events and never touches the backend, so this is where
    /// the two are tied together — the same arrangement
    /// <see cref="NetworkRoomScreenBridge"/> uses for the room browser. Without
    /// it the panel showed a list it had invented: four friends and seven
    /// strangers who existed only in <c>HomeLifetimeScope</c>.
    /// </remarks>
    public sealed class HomeFriendBridge : IStartable, IDisposable
    {
        private readonly IHomeMenuView view;
        private readonly FriendUiCommands friends;
        private readonly BackendSignIn signIn;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        public HomeFriendBridge(
            IHomeMenuView view,
            FriendUiCommands friends,
            BackendSignIn signIn)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        }

        public void Start()
        {
            view.ActionClicked += OnActionClicked;
            view.FriendSearchOpened += OnFriendSearchOpened;
            view.FriendSearchRequested += OnFriendSearchRequested;
            view.FriendRequestClicked += OnFriendRequestClicked;
            view.FriendRequestAccepted += OnFriendRequestAccepted;
            view.FriendRequestDeclined += OnFriendRequestDeclined;

            // Loaded before the player opens anything, so the panel is filled
            // the first time rather than after it appears empty.
            RefreshAsync().Forget();
        }

        public void Dispose()
        {
            view.ActionClicked -= OnActionClicked;
            view.FriendSearchOpened -= OnFriendSearchOpened;
            view.FriendSearchRequested -= OnFriendSearchRequested;
            view.FriendRequestClicked -= OnFriendRequestClicked;
            view.FriendRequestAccepted -= OnFriendRequestAccepted;
            view.FriendRequestDeclined -= OnFriendRequestDeclined;

            // Everything in flight is abandoned rather than allowed to write to
            // a screen that is being torn down.
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private void OnActionClicked(HomeMenuAction action)
        {
            if (action == HomeMenuAction.Friends)
            {
                // Opening the panel is the closest thing this screen has to a
                // refresh button, and presence is only ever as fresh as the last
                // read.
                RefreshAsync().Forget();
            }
        }

        private void OnFriendSearchOpened()
        {
            RefreshIncomingRequestsAsync().Forget();
        }

        private void OnFriendSearchRequested(string query)
        {
            SearchAsync(query).Forget();
        }

        private void OnFriendRequestClicked(string playerId)
        {
            SendRequestAsync(playerId).Forget();
        }

        private void OnFriendRequestAccepted(string playerId)
        {
            AnswerRequestAsync(playerId, accepted: true).Forget();
        }

        private void OnFriendRequestDeclined(string playerId)
        {
            AnswerRequestAsync(playerId, accepted: false).Forget();
        }

        private async UniTaskVoid RefreshAsync()
        {
            if (!await Ready())
            {
                return;
            }

            Report("friend list", await friends.RefreshFriendsAsync(lifetime.Token));
            await RefreshIncomingRequests();
        }

        private async UniTaskVoid RefreshIncomingRequestsAsync()
        {
            if (!await Ready())
            {
                return;
            }

            await RefreshIncomingRequests();
        }

        private async UniTask RefreshIncomingRequests()
        {
            var answer = await friends.ListIncomingRequestsAsync(lifetime.Token);
            if (!answer.Ok)
            {
                Report("friend requests", answer.Failure);
                return;
            }

            view.SetIncomingRequests(answer.Value);
        }

        private async UniTaskVoid SearchAsync(string query)
        {
            if (!await Ready())
            {
                return;
            }

            var failure = await friends.SearchAsync(query, friends.FriendIds(), lifetime.Token);

            // A query the server will not accept is the player typing, not a
            // fault: one character is below its minimum. Reported quietly so the
            // log does not fill up while someone types.
            if (failure != BackendFailure.InvalidRequest)
            {
                Report("search", failure);
            }
        }

        private async UniTaskVoid SendRequestAsync(string playerId)
        {
            if (!await Ready())
            {
                return;
            }

            Report("friend request", await friends.SendRequestAsync(playerId, lifetime.Token));
        }

        private async UniTaskVoid AnswerRequestAsync(string playerId, bool accepted)
        {
            if (!await Ready())
            {
                return;
            }

            var failure = accepted
                ? await friends.AcceptRequestAsync(playerId, lifetime.Token)
                : await friends.DeclineRequestAsync(playerId, lifetime.Token);

            Report(accepted ? "accept" : "decline", failure);

            // Re-read either way. On success the row is gone; on failure the
            // list this screen is showing is out of date, which is what most of
            // these failures mean.
            await RefreshIncomingRequests();
        }

        /// <summary>
        /// Whether there is an account and this screen is still alive.
        /// </summary>
        private async UniTask<bool> Ready()
        {
            if (lifetime.IsCancellationRequested)
            {
                return false;
            }

            return await signIn.Ready && !lifetime.IsCancellationRequested;
        }

        private static void Report(string what, BackendFailure failure)
        {
            if (failure == BackendFailure.None || failure == BackendFailure.Cancelled)
            {
                return;
            }

            Debug.LogWarning($"[Friends] {what} failed: {failure}.");
        }
    }
}
