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
            view.FriendRequestCancelled += OnFriendRequestCancelled;
            view.FriendListRefreshRequested += OnRefreshRequested;
            view.FriendRemoved += OnFriendRemoved;
            view.FriendBlocked += OnFriendBlocked;

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
            view.FriendRequestCancelled -= OnFriendRequestCancelled;
            view.FriendListRefreshRequested -= OnRefreshRequested;
            view.FriendRemoved -= OnFriendRemoved;
            view.FriendBlocked -= OnFriendBlocked;

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
            RefreshRequestsAsync().Forget();
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

        private void OnFriendRequestCancelled(string playerId)
        {
            CancelSentRequestAsync(playerId).Forget();
        }

        private void OnRefreshRequested()
        {
            RefreshAsync().Forget();
        }

        private void OnFriendRemoved(string playerId)
        {
            RemoveFriendAsync(playerId).Forget();
        }

        private void OnFriendBlocked(string playerId)
        {
            BlockAsync(playerId).Forget();
        }

        private async UniTaskVoid RefreshAsync()
        {
            if (!await Ready())
            {
                return;
            }

            Report("friend list", await friends.RefreshFriendsAsync(lifetime.Token));
            await RefreshRequests();
        }

        private async UniTaskVoid RefreshRequestsAsync()
        {
            if (!await Ready())
            {
                return;
            }

            await RefreshRequests();
        }

        /// <remarks>
        /// Both directions together. They are shown one above the other and every
        /// action on either changes what the other should say, so reading one
        /// without the other leaves half the panel stale.
        /// </remarks>
        private async UniTask RefreshRequests()
        {
            var incoming = await friends.ListIncomingRequestsAsync(lifetime.Token);
            if (incoming.Ok)
            {
                view.SetIncomingRequests(incoming.Value);
            }
            else
            {
                Report("received requests", incoming.Failure);
            }

            var outgoing = await friends.ListOutgoingRequestsAsync(lifetime.Token);
            if (outgoing.Ok)
            {
                view.SetOutgoingRequests(outgoing.Value);
            }
            else
            {
                Report("sent requests", outgoing.Failure);
            }
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
            // log does not fill up while someone types, and never shown - a
            // message that appeared on every second keystroke would be noise.
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

            // The request just sent belongs in the sent list, and if the server
            // settled it into a friendship instead there is nothing to show.
            await RefreshRequests();
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
            await RefreshRequests();
        }

        private async UniTaskVoid CancelSentRequestAsync(string playerId)
        {
            if (!await Ready())
            {
                return;
            }

            Report("cancel", await friends.CancelSentRequestAsync(playerId, lifetime.Token));
            await RefreshRequests();
        }

        /// <remarks>
        /// The friend list is reloaded by the command itself. Requests are not
        /// touched: ending a friendship leaves no request behind, and there was
        /// none to begin with.
        /// </remarks>
        private async UniTaskVoid RemoveFriendAsync(string playerId)
        {
            if (!await Ready())
            {
                return;
            }

            Report("unfriend", await friends.RemoveFriendAsync(playerId, lifetime.Token));
        }

        /// <remarks>
        /// The friend list is reloaded by the command itself, because blocking
        /// ends the friendship. The requests are reloaded here, because it drops
        /// any request between the two as well.
        /// </remarks>
        private async UniTaskVoid BlockAsync(string playerId)
        {
            if (!await Ready())
            {
                return;
            }

            Report("block", await friends.BlockAsync(playerId, lifetime.Token));
            await RefreshRequests();
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

        /// <summary>
        /// Puts a failure on the screen as well as in the log.
        /// </summary>
        /// <remarks>
        /// The log alone was not enough. A refused request took its "요청 중"
        /// mark back and said nothing else, so the player saw a button that
        /// appeared to do nothing and had no way to learn why. The rename field
        /// already explains its refusals; this is the same screen.
        /// <para>
        /// A success clears whatever the last failure said, so a message never
        /// outlives the thing it was about.
        /// </para>
        /// </remarks>
        private void Report(string what, BackendFailure failure)
        {
            if (failure == BackendFailure.None)
            {
                view.SetFriendActionError(string.Empty);
                return;
            }

            if (failure == BackendFailure.Cancelled)
            {
                // The screen is going away. Writing to it would be writing to
                // something nobody is looking at.
                return;
            }

            Debug.LogWarning($"[Friends] {what} failed: {failure}.");
            view.SetFriendActionError(Explain(failure));
        }

        /// <remarks>
        /// Written here rather than taken from the server's message, which is
        /// allowed to change wording and is not part of the contract. The client
        /// guide says to branch on the code, and this is that branch.
        /// </remarks>
        private static string Explain(BackendFailure failure)
        {
            switch (failure)
            {
                // Deliberately not "that person blocked you". The server refuses
                // to distinguish a blocked target from an account that does not
                // exist, and saying which it is would undo that.
                case BackendFailure.TargetNotFound:
                    return "그 사용자를 찾을 수 없습니다";

                case BackendFailure.AlreadyFriends:
                    return "이미 친구입니다";

                case BackendFailure.RequestAlreadySent:
                    return "이미 보낸 요청입니다";

                case BackendFailure.RequestNotFound:
                    return "그 요청이 이미 없습니다";

                case BackendFailure.NotFriends:
                    return "친구가 아닙니다";

                case BackendFailure.SelfRequest:
                    return "자기 자신에게는 보낼 수 없습니다";

                case BackendFailure.AccountNotFound:
                    return "계정을 찾을 수 없습니다";

                case BackendFailure.Offline:
                case BackendFailure.Timeout:
                    return "서버에 연결할 수 없습니다";

                case BackendFailure.NotSignedIn:
                    return "서버에 연결되어 있지 않습니다";

                case BackendFailure.Conflict:
                    return "잠시 후 다시 시도해 주세요";

                default:
                    return "처리하지 못했습니다";
            }
        }
    }
}
