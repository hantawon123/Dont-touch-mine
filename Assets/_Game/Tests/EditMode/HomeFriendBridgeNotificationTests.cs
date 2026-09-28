using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Players;
using Game.Core.Ports;
using Game.Core.Settings;
using NUnit.Framework;
using R3;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the friend panel re-reads when the server pushes a change, checked
    /// against a gateway that counts every read.
    /// </summary>
    /// <remarks>
    /// The bridge is the production class. The counts below are taken after the
    /// bridge's own first load, so each test states only what a push added.
    /// </remarks>
    public sealed class HomeFriendBridgeNotificationTests
    {
        [Test]
        public async Task VisiblePanel_RefreshesPresenceInBothDirectionsWithoutReadingRequests()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.View.SetFriendListVisible(true);
            wiring.Gateway.FriendPresence = FriendPresence.Online;

            wiring.Advance(2.9f);
            Assert.That(wiring.Gateway.FriendReads, Is.Zero);
            wiring.Advance(0.1f);
            await wiring.Settle();
            Assert.That(wiring.Friends.OnlineFriends.Count, Is.EqualTo(1));

            wiring.Gateway.FriendPresence = FriendPresence.Offline;
            wiring.Advance(3f);
            await wiring.Settle();
            Assert.That(wiring.Friends.OnlineFriends.Count, Is.Zero);
            Assert.That(wiring.Friends.OfflineFriends.Count, Is.EqualTo(1));
            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(2));
            Assert.That(wiring.Gateway.IncomingReads, Is.Zero);
            Assert.That(wiring.Gateway.OutgoingReads, Is.Zero);
        }

        [Test]
        public async Task HiddenPanel_StopsPollingAndResetsTheInterval()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.Advance(30f);
            Assert.That(wiring.Gateway.FriendReads, Is.Zero);
            wiring.View.SetFriendListVisible(true);
            wiring.Advance(2f);
            wiring.View.SetFriendListVisible(false);
            wiring.Advance(30f);
            wiring.View.SetFriendListVisible(true);
            wiring.Advance(1f);
            Assert.That(wiring.Gateway.FriendReads, Is.Zero);
            wiring.Advance(2f);
            await wiring.Settle();
            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(1));
        }

        [Test]
        public async Task AReceivedRequest_ReReadsBothRequestListsAndNotFriends()
        {
            using var wiring = await Wiring.StartAsync();

            wiring.Push(ServerNotificationKind.FriendRequestReceived);
            await wiring.Settle();

            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.OutgoingReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(0));
        }

        [Test]
        public async Task ARemovedRequest_ReReadsBothRequestLists()
        {
            using var wiring = await Wiring.StartAsync();

            // Declined or withdrawn — the server does not say which, and either
            // way both directions may have changed.
            wiring.Push(ServerNotificationKind.FriendRequestRemoved);
            await wiring.Settle();

            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.OutgoingReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(0));
        }

        [Test]
        public async Task AnAcceptedRequest_ReReadsFriendsAndRequests()
        {
            using var wiring = await Wiring.StartAsync();

            // A new friend, and one fewer sent request.
            wiring.Push(ServerNotificationKind.FriendRequestAccepted);
            await wiring.Settle();

            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.OutgoingReads, Is.EqualTo(1));
        }

        [Test]
        public async Task ARemovedFriend_ReReadsFriendsOnly()
        {
            using var wiring = await Wiring.StartAsync();

            wiring.Push(ServerNotificationKind.FriendRemoved);
            await wiring.Settle();

            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(0));
        }

        [Test]
        public async Task ARoomInvite_ShowsACardAndReadsNothing()
        {
            using var wiring = await Wiring.StartAsync();

            // The push carries the whole invite, so there is nothing to re-read.
            wiring.Push(ServerNotificationKind.RoomInviteReceived, roomCode: "7K2M9P");
            await wiring.Settle();

            Assert.That(wiring.View.Invites.Count, Is.EqualTo(1));
            Assert.That(wiring.View.Invites[0].FromNickname, Is.EqualTo("상대"));
            Assert.That(wiring.View.Invites[0].RoomCode, Is.EqualTo("7K2M9P"));
            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(0));
            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(0));
        }

        [Test]
        public async Task WithTheInviteNoticeOff_NoCardIsShownAndTheInviteIsLeftAlone()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.SilenceInvites();

            wiring.Push(ServerNotificationKind.RoomInviteReceived, roomCode: "7K2M9P");
            await wiring.Settle();

            Assert.That(wiring.View.Invites, Is.Empty);

            // Silencing a notice is not declining it: the friend is not told.
            Assert.That(wiring.Invites.DeclinedPlayers, Is.Empty);
        }

        [Test]
        public async Task TurningTheNoticeOff_LeavesCardsThatAreAlreadyUp()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.Push(ServerNotificationKind.RoomInviteReceived, roomCode: "7K2M9P");
            await wiring.Settle();

            wiring.SilenceInvites();
            await wiring.Settle();

            // A friend is waiting on an answer to this one.
            Assert.That(wiring.View.Invites.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ARoomInviteWithoutARoom_ShowsNothing()
        {
            using var wiring = await Wiring.StartAsync();

            wiring.Push(ServerNotificationKind.RoomInviteReceived);
            await wiring.Settle();

            Assert.That(wiring.View.Invites, Is.Empty);
        }

        [Test]
        public async Task AcceptingAnInvite_EntersThatRoomAndClearsItOnTheServer()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.Push(ServerNotificationKind.RoomInviteReceived, roomCode: "7K2M9P");
            await wiring.Settle();

            wiring.View.Accept(wiring.View.Invites[0].Id);
            await wiring.Settle();

            Assert.That(wiring.Host.JoinedRoom, Is.EqualTo("7K2M9P"));
            Assert.That(wiring.Invites.DeclinedPlayers, Is.EqualTo(new[] { "other" }));
            Assert.That(wiring.View.Invites, Is.Empty, "the card is gone");
        }

        [Test]
        public async Task DecliningAnInvite_OnlyClearsItOnTheServer()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.Push(ServerNotificationKind.RoomInviteReceived, roomCode: "7K2M9P");
            await wiring.Settle();

            wiring.View.Decline(wiring.View.Invites[0].Id);
            await wiring.Settle();

            Assert.That(wiring.Host.JoinedRoom, Is.Null);
            Assert.That(wiring.Invites.DeclinedPlayers, Is.EqualTo(new[] { "other" }));
            Assert.That(wiring.View.Invites, Is.Empty);
        }

        [Test]
        public async Task AnsweringACardTwice_ActsOnce()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.Push(ServerNotificationKind.RoomInviteReceived, roomCode: "7K2M9P");
            await wiring.Settle();
            var id = wiring.View.Invites[0].Id;

            wiring.View.Accept(id);
            wiring.View.Accept(id);
            await wiring.Settle();

            Assert.That(wiring.Host.Joins, Is.EqualTo(1));
            Assert.That(wiring.Invites.DeclinedPlayers.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task TheLinkComingBack_ReReadsTheRequests()
        {
            using var wiring = await Wiring.StartAsync();

            // Whatever was pushed while the link was down is gone. The requests
            // are re-read here because this panel is the only thing holding them;
            // the friend list is caught up project-wide by NotificationLink.
            wiring.Link.State.Value = NotificationLinkState.Connected;
            await wiring.Settle();

            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(1));
            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(0));
        }

        [Test]
        public async Task PushesWhileARead_IsInFlight_QueueOneMoreReadNotSeveral()
        {
            using var wiring = await Wiring.StartAsync();
            wiring.Gateway.HoldIncoming = true;

            wiring.Push(ServerNotificationKind.FriendRequestReceived);
            await wiring.Settle();
            wiring.Push(ServerNotificationKind.FriendRequestReceived);
            wiring.Push(ServerNotificationKind.FriendRequestRemoved);
            await wiring.Settle();

            // One read is stuck on the wire. The two pushes behind it did not
            // start reads of their own.
            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(1));

            wiring.Gateway.ReleaseIncoming();
            await wiring.Settle();

            // Exactly one follow-up. Not dropped — the held read was issued before
            // those pushes and may have missed them — and not one per push.
            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(2));
        }

        [Test]
        public async Task AfterDispose_PushesReadNothing()
        {
            var wiring = await Wiring.StartAsync();
            wiring.Dispose();

            wiring.Push(ServerNotificationKind.FriendRequestReceived);
            wiring.Link.State.Value = NotificationLinkState.Connected;
            await wiring.Settle();

            Assert.That(wiring.Gateway.IncomingReads, Is.EqualTo(0));
            Assert.That(wiring.Gateway.FriendReads, Is.EqualTo(0));
        }

        private sealed class Wiring : IDisposable
        {
            private readonly HomeFriendBridge bridge;

            private Wiring(BackendSignIn signIn)
            {
                var commands = new FriendUiCommands(Gateway, Friends, new FriendSearchSystem());
                bridge = new HomeFriendBridge(
                    View, commands, signIn, Link, Host, Invites, NotificationSettings);
                bridge.Start();
            }

            public CountingGateway Gateway { get; } = new CountingGateway();

            public FriendListSystem Friends { get; } = new FriendListSystem();

            public void Advance(float seconds) => bridge.Advance(seconds);

            public SilentView View { get; } = new SilentView();

            public RecordingHost Host { get; } = new RecordingHost();

            public RecordingInvites Invites { get; } = new RecordingInvites();

            public NotificationSettingsSystem NotificationSettings { get; } =
                new NotificationSettingsSystem(new InMemoryNotificationSettingsStore());

            /// <summary>Turns the game-invite notice off, as the settings screen would.</summary>
            public void SilenceInvites() =>
                NotificationSettings.Apply(
                    NotificationSettings.Current.With(
                        NotificationOption.GameInvite, InterfaceCatalog.Off));

            public FakeNotificationStream Link { get; } = new FakeNotificationStream();

            public static async UniTask<Wiring> StartAsync()
            {
                var signIn = new BackendSignIn(new SignedInAccounts(), new PlayerProfile("나"));
                await signIn.StartAsync(CancellationToken.None);

                var wiring = new Wiring(signIn);

                // Let the bridge's own first load finish, then count from zero so
                // each test speaks only of what its push added.
                await wiring.Settle();
                wiring.Gateway.ResetCounts();
                return wiring;
            }

            public void Push(ServerNotificationKind kind, string roomCode = null)
            {
                Link.Pushed.OnNext(new ServerNotification(
                    kind, "other", "상대", roomCode, new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc)));
            }

            /// <remarks>
            /// The fakes answer without ever yielding, so a few turns carry the
            /// bridge's fire-and-forget through to the end.
            /// </remarks>
            public async UniTask Settle()
            {
                await UniTask.Yield();
                await UniTask.Yield();
                await UniTask.Yield();
            }

            public void Dispose() => bridge.Dispose();
        }

        /// <summary>The server's push channel, driven by hand.</summary>
        private sealed class FakeNotificationStream : INotificationStream
        {
            public readonly Subject<ServerNotification> Pushed = new Subject<ServerNotification>();

            public readonly ReactiveProperty<NotificationLinkState> State =
                new ReactiveProperty<NotificationLinkState>(NotificationLinkState.Disconnected);

            Observable<ServerNotification> INotificationStream.Notifications => Pushed;

            ReadOnlyReactiveProperty<NotificationLinkState> INotificationStream.State => State;
        }

        /// <summary>Counts every read, and can hold the incoming-request read open.</summary>
        private sealed class CountingGateway : IFriendGateway
        {
            private UniTaskCompletionSource<BackendResult<IReadOnlyList<FriendRequestSummary>>> held;

            public int FriendReads { get; private set; }

            public FriendPresence FriendPresence { get; set; } = FriendPresence.Offline;

            public int IncomingReads { get; private set; }

            public int OutgoingReads { get; private set; }

            /// <summary>While true, the next incoming read does not answer until released.</summary>
            public bool HoldIncoming { get; set; }

            public void ResetCounts()
            {
                FriendReads = 0;
                IncomingReads = 0;
                OutgoingReads = 0;
            }

            public void ReleaseIncoming()
            {
                HoldIncoming = false;
                held?.TrySetResult(NoRequests());
                held = null;
            }

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> ListFriendsAsync(
                CancellationToken cancellation)
            {
                FriendReads++;
                return UniTask.FromResult(
                    BackendResult<IReadOnlyList<FriendSummary>>.Success(
                        new[] { new FriendSummary("other", "상대", FriendPresence) }));
            }

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>> ListIncomingRequestsAsync(
                CancellationToken cancellation)
            {
                IncomingReads++;
                if (!HoldIncoming)
                {
                    return UniTask.FromResult(NoRequests());
                }

                held = new UniTaskCompletionSource<BackendResult<IReadOnlyList<FriendRequestSummary>>>();
                return held.Task;
            }

            public UniTask<BackendResult<IReadOnlyList<FriendRequestSummary>>> ListOutgoingRequestsAsync(
                CancellationToken cancellation)
            {
                OutgoingReads++;
                return UniTask.FromResult(NoRequests());
            }

            public UniTask<BackendResult<IReadOnlyList<FriendSummary>>> SearchAsync(
                string nickname, CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<FriendSummary>>.Success(Array.Empty<FriendSummary>()));

            public UniTask<BackendResult<FriendRequestOutcome>> SendRequestAsync(
                string playerId, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult<FriendRequestOutcome>.Success(FriendRequestOutcome.Sent));

            public UniTask<BackendResult> AcceptRequestAsync(string playerId, CancellationToken cancellation) => Ok();

            public UniTask<BackendResult> DeclineRequestAsync(string playerId, CancellationToken cancellation) => Ok();

            public UniTask<BackendResult> RemoveFriendAsync(string playerId, CancellationToken cancellation) => Ok();

            private static UniTask<BackendResult> Ok() => UniTask.FromResult(BackendResult.Success());

            private static BackendResult<IReadOnlyList<FriendRequestSummary>> NoRequests() =>
                BackendResult<IReadOnlyList<FriendRequestSummary>>.Success(Array.Empty<FriendRequestSummary>());
        }

        /// <summary>An account gateway that signs in and does nothing else.</summary>
        private sealed class SignedInAccounts : IAccountGateway
        {
            public UniTask<BackendResult<AccountSnapshot>> SignInAsync(CancellationToken cancellation) => Account();

            public UniTask<BackendResult<AccountSnapshot>> RefreshAsync(CancellationToken cancellation) => Account();

            public UniTask<BackendResult<AccountSnapshot>> RenameAsync(
                string nickname, CancellationToken cancellation) => Account();

            public UniTask<BackendResult<AccountSnapshot>> SetSearchableAsync(
                bool searchable, CancellationToken cancellation) => Account();

            public UniTask<BackendResult<AccountSnapshot>> SetAppearanceAsync(
                AvatarAppearance appearance, CancellationToken cancellation) => Account();

            public UniTask<BackendResult<AccountSnapshot>> ClearAppearanceAsync(
                CancellationToken cancellation) => Account();

            public UniTask<BackendResult> DeleteAccountAsync(CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            private static UniTask<BackendResult<AccountSnapshot>> Account() =>
                UniTask.FromResult(
                    BackendResult<AccountSnapshot>.Success(new AccountSnapshot("me", "나", true, true)));
        }

        /// <summary>Remembers the one thing an invite can ask of the host.</summary>
        private sealed class RecordingHost : IHomeApplicationHost
        {
            public string JoinedRoom { get; private set; }

            public int Joins { get; private set; }

            public void JoinRoom(string roomCode)
            {
                JoinedRoom = roomCode;
                Joins++;
            }

            public void Quit() { }
            public void OpenHome() { }
            public void OpenRoomBrowser() { }
            public void OpenCharacterCloset() { }
            public void OpenSettings() { }
            public void OpenTutorial() { }
            public void CreateRoom(string title, bool isPublic, int maxPlayers) { }
            public void OpenLobby() { }
        }

        /// <summary>Counts the invites the bridge clears on the server.</summary>
        private sealed class RecordingInvites : IInviteGateway
        {
            public List<string> DeclinedPlayers { get; } = new List<string>();

            public UniTask<BackendResult> SendAsync(
                string playerId, string roomCode, CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());

            public UniTask<BackendResult<IReadOnlyList<RoomInvitation>>> ListAsync(
                CancellationToken cancellation) =>
                UniTask.FromResult(
                    BackendResult<IReadOnlyList<RoomInvitation>>.Success(Array.Empty<RoomInvitation>()));

            public UniTask<BackendResult> DeclineAsync(string playerId, CancellationToken cancellation)
            {
                DeclinedPlayers.Add(playerId);
                return UniTask.FromResult(BackendResult.Success());
            }
        }

        /// <summary>
        /// A home screen that shows nothing and raises nothing, except that it
        /// keeps the invite cards it is handed and can press their buttons.
        /// </summary>
        private sealed class SilentView : IHomeMenuView
        {
            public List<RoomInvite> Invites { get; } = new List<RoomInvite>();

            public void Accept(string id) => RoomInviteAccepted?.Invoke(id);

            public void Decline(string id) => RoomInviteDeclined?.Invoke(id);

            public event Action<string> RoomInviteAccepted;
            public event Action<string> RoomInviteDeclined;

            public void SetRoomInvites(IReadOnlyList<RoomInvite> invites)
            {
                Invites.Clear();
                Invites.AddRange(invites);
            }

            public event Action<HomeMenuAction> ActionClicked { add { } remove { } }
            public event Action FriendListDismissed { add { } remove { } }
            public event Action ProfileSettingsDismissed { add { } remove { } }
            public event Action<string> NicknameChangeRequested { add { } remove { } }
            public event Action<string> NicknameEdited { add { } remove { } }
            public event Action<bool> NicknameSearchAllowedChanged { add { } remove { } }
            public event Action FriendSearchOpened { add { } remove { } }
            public event Action FriendSearchClosed { add { } remove { } }
            public event Action<string> FriendSearchRequested { add { } remove { } }
            public event Action<string> FriendRequestClicked { add { } remove { } }
            public event Action<string> FriendRequestAccepted { add { } remove { } }
            public event Action<string> FriendRequestDeclined { add { } remove { } }
            public event Action ServerSettingsDismissed { add { } remove { } }
            public event Action<string> RegionSelected { add { } remove { } }
            public event Action<string, bool, int> RoomCreationRequested { add { } remove { } }
            public event Action CreateRoomDismissed { add { } remove { } }
            public event Action<string> FriendRequestCancelled { add { } remove { } }
            public event Action FriendListRefreshRequested { add { } remove { } }
            public event Action<string> FriendRemoved { add { } remove { } }

            public void SetNickname(string nickname) { }
            public void SetProfileSettingsVisible(bool visible) { }

            public void SetSuspendedNoticeVisible(bool visible) =>
                SuspendedNoticeVisible = visible;

            public void ShowUpdateNotice(string downloadUrl) { }

            public bool SuspendedNoticeVisible { get; private set; }
            public void SetNicknameAppliedFeedbackVisible(bool visible) { }
            public void SetNicknameError(string message) { }
            public void SetNicknameSearchAllowed(bool allowed) { }
            public void SetNicknameSearchAllowedError(string message) { }
            public void ShowConnectionError(string message) { }

            public void ShowChrome(UiLocale locale) { }
            public bool FriendListVisible { get; private set; }
            public void SetFriendListVisible(bool visible) { FriendListVisible = visible; }
            public void SetFriends(IReadOnlyList<FriendSummary> onlineFriends, IReadOnlyList<FriendSummary> offlineFriends) { }
            public void SetFriendSearchVisible(bool visible) { }
            public void SetFriendSearchResults(IReadOnlyList<FriendSearchHit> results) { }
            public void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests) { }
            public void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests) { }
            public void SetFriendActionError(string message) { }
            public void SetServerSettingsVisible(bool visible) { }
            public void SetSelectedRegion(string code) { }
            public void SetCreateRoomVisible(bool visible) { }
            public void SetNicknameSettled(bool settled) { }

            public void SetProfileAppearance(Game.Core.Players.AvatarAppearance appearance) { }
        }
    }
}
