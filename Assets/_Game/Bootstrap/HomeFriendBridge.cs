using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using Game.Core.Settings;
using R3;
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
    /// <para>
    /// It also listens to the backend. When the server pushes that a request
    /// arrived, was answered or went away, the affected lists are read again, so
    /// the request badge moves without the player reopening the panel. The push
    /// is only the cue; what is shown still comes from the same REST reads the
    /// buttons trigger, so a missed push costs nothing the next read does not
    /// repair.
    /// </para>
    /// </remarks>
    public sealed class HomeFriendBridge : IStartable, ITickable, IDisposable
    {
        private readonly IHomeMenuView view;
        private readonly FriendUiCommands friends;
        private readonly BackendSignIn signIn;
        private readonly INotificationStream notifications;
        private readonly IHomeApplicationHost host;
        private readonly IInviteGateway invites;
        private readonly NotificationSettingsSystem notificationSettings;
        private readonly UiLocale locale;

        /// <summary>
        /// The room invitations waiting on this screen. Owned here because the
        /// push that brings them arrives here, and the comment below the switch
        /// always said the toast would.
        /// </summary>
        private readonly RoomInviteInbox inbox = new RoomInviteInbox();
        private bool refreshing;
        private const float PresenceRefreshInterval = 3f;
        private float presenceElapsed;
        private bool refreshingPresence;
        private bool refreshingRequests;
        private bool requestsChanged;
        private IDisposable pushed;
        private IDisposable relinked;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        public HomeFriendBridge(
            IHomeMenuView view,
            FriendUiCommands friends,
            BackendSignIn signIn,
            INotificationStream notifications,
            IHomeApplicationHost host,
            IInviteGateway invites,
            NotificationSettingsSystem notificationSettings,
            UiLocale locale = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
            this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.invites = invites ?? throw new ArgumentNullException(nameof(invites));
            this.notificationSettings = notificationSettings
                ?? throw new ArgumentNullException(nameof(notificationSettings));
            this.locale = locale;
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
            view.RoomInviteAccepted += OnRoomInviteAccepted;
            view.RoomInviteDeclined += OnRoomInviteDeclined;
            inbox.Changed += OnInvitesChanged;

            pushed = notifications.Notifications.Subscribe(OnPushed);

            // The requests are re-read here on every reconnection, because this
            // panel is the only thing that holds them. The friend list has a store
            // of its own and is caught up by NotificationLink, project-wide.
            relinked = notifications.State
                .Where(state => state == NotificationLinkState.Connected)
                .Subscribe(_ => RefreshRequestsAfterPushAsync().Forget());

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
            view.RoomInviteAccepted -= OnRoomInviteAccepted;
            view.RoomInviteDeclined -= OnRoomInviteDeclined;
            inbox.Changed -= OnInvitesChanged;

            pushed?.Dispose();
            relinked?.Dispose();

            // Everything in flight is abandoned rather than allowed to write to
            // a screen that is being torn down.
            lifetime.Cancel();
            lifetime.Dispose();
        }

        /// <summary>
        /// Something changed on the server. Read back whichever lists it touched.
        /// </summary>
        /// <remarks>
        /// A push is treated like one of this screen's own mutations: if a read
        /// is already in flight, one more is queued behind it rather than
        /// started beside it or dropped. Dropped would miss a change the
        /// in-flight read was issued too early to see; beside it would race two
        /// answers onto the same panel.
        /// <para>
        /// Failures are logged, not shown. The player did nothing to cause this
        /// read, and an error line appearing on its own would read as the panel
        /// breaking rather than as the network hiccup it is.
        /// </para>
        /// </remarks>
        private void OnPushed(ServerNotification notification)
        {
            switch (notification.Kind)
            {
                case ServerNotificationKind.FriendRequestReceived:
                case ServerNotificationKind.FriendRequestRemoved:
                    RefreshRequestsAfterPushAsync().Forget();
                    break;

                case ServerNotificationKind.FriendRequestAccepted:
                    // A new friend, and one fewer sent request.
                    RefreshFriendsAfterPushAsync(andRequests: true).Forget();
                    break;

                case ServerNotificationKind.FriendRemoved:
                    RefreshFriendsAfterPushAsync(andRequests: false).Forget();
                    break;

                case ServerNotificationKind.RoomInviteReceived:
                    // Nothing to re-read: the push carries the whole invite. A
                    // push without a room is malformed and shows nothing rather
                    // than a card that could not be accepted.
                    if (notification.RoomCode != null && WantsInviteCards)
                    {
                        inbox.Receive(
                            notification.FromPlayerId, notification.FromNickname, notification.RoomCode);
                    }

                    break;
            }
        }

        /// <summary>
        /// Whether this player still wants to be told about invitations.
        /// </summary>
        /// <remarks>
        /// Read at the moment one arrives rather than subscribed to, so turning
        /// the setting off does not sweep away cards that are already up: those
        /// are questions a friend asked and is waiting on, and the setting says
        /// what to do about the next one.
        /// <para>
        /// The invitation itself is left on the server. Declining it here would
        /// tell the friend they were turned down, which is not what silencing a
        /// notice means.
        /// </para>
        /// </remarks>
        private bool WantsInviteCards =>
            notificationSettings.Current.IsOn(NotificationOption.GameInvite);

        /// <summary>
        /// Runs the invite cards' clock down.
        /// </summary>
        /// <remarks>
        /// Unscaled, because a card is about something happening on a friend's
        /// screen rather than in this one's simulation.
        /// </remarks>
        public void Tick() => Advance(Time.unscaledDeltaTime);

        public void Advance(float unscaledDeltaTime)
        {
            if (lifetime.IsCancellationRequested) return;

            inbox.Advance(unscaledDeltaTime);
            if (!view.FriendListVisible)
            {
                presenceElapsed = 0f;
                return;
            }

            // Presence changes are not pushed by the backend. Poll only while
            // the panel is visible, without overlapping slow requests.
            if (refreshingPresence || refreshing) return;
            presenceElapsed += unscaledDeltaTime;
            if (presenceElapsed < PresenceRefreshInterval) return;

            presenceElapsed = 0f;
            RefreshPresenceAsync().Forget();
        }

        private async UniTaskVoid RefreshPresenceAsync()
        {
            refreshingPresence = true;
            try
            {
                if (!await Ready()) return;
                var failure = await friends.RefreshFriendsAsync(lifetime.Token);
                if (failure != BackendFailure.None && failure != BackendFailure.Cancelled)
                {
                    Debug.LogWarning($"[Friends] Presence refresh failed: {failure}.");
                }
            }
            finally { refreshingPresence = false; }
        }

        private void OnInvitesChanged()
        {
            view.SetRoomInvites(inbox.Visible);
        }

        /// <remarks>
        /// The card goes first, then the room. Whether the room lets this player
        /// in is the host's to say, on the same notice room creation uses; the
        /// question the card asked has been answered either way.
        /// </remarks>
        private void OnRoomInviteAccepted(string inviteId)
        {
            if (!inbox.Take(inviteId, out var invite))
            {
                return;
            }

            ClearOnServerAsync(invite).Forget();
            host.JoinRoom(invite.RoomCode);
        }

        private void OnRoomInviteDeclined(string inviteId)
        {
            if (inbox.Take(inviteId, out var invite))
            {
                ClearOnServerAsync(invite).Forget();
            }
        }

        /// <remarks>
        /// The server keeps an invite until it is declined, whichever way the
        /// player answered; this is what stops it being listed again. Failures
        /// are logged only: the card is already gone, and a stale entry on the
        /// server is nothing the player could act on.
        /// </remarks>
        private async UniTaskVoid ClearOnServerAsync(RoomInvite invite)
        {
            var result = await invites.DeclineAsync(invite.FromPlayerId, lifetime.Token);
            if (!result.Ok && result.Failure != BackendFailure.Cancelled)
            {
                Debug.LogWarning(
                    $"[Friends] Clearing the invite from {invite.FromPlayerId} failed: {result.Failure}.");
            }
        }

        private async UniTaskVoid RefreshRequestsAfterPushAsync()
        {
            if (!await Ready())
            {
                return;
            }

            await RefreshRequests(afterMutation: true);
        }

        private async UniTaskVoid RefreshFriendsAfterPushAsync(bool andRequests)
        {
            if (!await Ready())
            {
                return;
            }

            // Forced so that a refresh a button started a moment ago, with its
            // own token, does not stand in for the read this push asks for.
            var failure = await friends.RefreshFriendsAsync(lifetime.Token, force: true);
            if (failure != BackendFailure.None && failure != BackendFailure.Cancelled)
            {
                Debug.LogWarning($"[Friends] friend list did not reload after a push: {failure}.");
            }

            if (andRequests)
            {
                await RefreshRequests(afterMutation: true);
            }
        }

        private void OnActionClicked(HomeMenuAction action)
        {
            if (action == HomeMenuAction.Friends)
            {
                presenceElapsed = 0f;
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

        private async UniTaskVoid RefreshAsync()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                if (!await Ready()) return;
                Report("friend list", await friends.RefreshFriendsAsync(lifetime.Token));
                await RefreshRequests();
            }
            finally { refreshing = false; }
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
        private async UniTask RefreshRequests(bool afterMutation = false)
        {
            if (refreshingRequests)
            {
                requestsChanged |= afterMutation;
                return;
            }
            refreshingRequests = true;
            try
            {
                do
                {
                    requestsChanged = false;
                    await ReadRequests();
                } while (requestsChanged && !lifetime.IsCancellationRequested);
            }
            finally { refreshingRequests = false; }
        }

        private async UniTask ReadRequests()
        {
            var incoming = await friends.ListIncomingRequestsAsync(lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            if (incoming.Ok)
            {
                view.SetIncomingRequests(incoming.Value);
            }
            else
            {
                Report("received requests", incoming.Failure);
            }

            var outgoing = await friends.ListOutgoingRequestsAsync(lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
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
            await RefreshRequests(afterMutation: true);
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
            await RefreshRequests(afterMutation: true);
        }

        private async UniTaskVoid CancelSentRequestAsync(string playerId)
        {
            if (!await Ready())
            {
                return;
            }

            Report("cancel", await friends.CancelSentRequestAsync(playerId, lifetime.Token));
            await RefreshRequests(afterMutation: true);
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

            var failure = await friends.RemoveFriendAsync(playerId, lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            Report("unfriend", failure);
            if (failure != BackendFailure.None && failure != BackendFailure.Cancelled)
            {
                view.ShowConnectionError(Explain(failure));
            }
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
            if (lifetime.IsCancellationRequested) return;
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
        private string Copy(string key) =>
            locale != null
                ? locale.Get(key)
                : UiTextCatalog.Shipped.Get(key, "ko");

        private string Explain(BackendFailure failure)
        {
            switch (failure)
            {
                case BackendFailure.TargetNotFound:
                    return Copy(UiText.Home.TargetNotFound);

                case BackendFailure.AlreadyFriends:
                    return Copy(UiText.Home.AlreadyFriends);

                case BackendFailure.RequestAlreadySent:
                    return Copy(UiText.Home.RequestAlreadySent);

                case BackendFailure.RequestNotFound:
                    return Copy(UiText.Home.RequestNotFound);

                case BackendFailure.NotFriends:
                    return Copy(UiText.Home.NotFriends);

                case BackendFailure.TargetInGame:
                    return Copy(UiText.Home.TargetInGame);

                case BackendFailure.SelfRequest:
                    return Copy(UiText.Home.SelfRequest);

                case BackendFailure.AccountNotFound:
                    return Copy(UiText.Home.AccountNotFound);

                case BackendFailure.Offline:
                case BackendFailure.Timeout:
                    return Copy(UiText.Settings.FeedbackOffline);

                case BackendFailure.NotSignedIn:
                    return Copy(UiText.Settings.FeedbackNotSignedIn);

                case BackendFailure.Conflict:
                    return Copy(UiText.Home.Conflict);

                default:
                    return Copy(UiText.Home.FriendFailed);
            }
        }
    }
}
