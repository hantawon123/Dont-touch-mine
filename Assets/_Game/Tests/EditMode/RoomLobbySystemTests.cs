using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Lobby;
using Game.Core.Ports;
using Game.Core.Rooms;
using NUnit.Framework;
using R3;

namespace Game.Tests.EditMode
{
    public sealed class RoomLobbySystemTests
    {
        [Test]
        public void CreateSettings_ValidatesPublicRoomConfiguration()
        {
            var request = new RoomCreateRequest(
                "  초보방  ",
                false,
                "ignored",
                6,
                "  market-01  ");

            Assert.That(
                request.TryCreateSettings(6, out var settings, out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(RoomSettingsError.None));
            Assert.That(settings.Title, Is.EqualTo("초보방"));
            Assert.That(settings.IsLocked, Is.False);
            Assert.That(settings.MaxPlayers, Is.EqualTo(6));
            Assert.That(settings.MapId, Is.EqualTo("market-01"));
            Assert.That(request.Password, Is.Null);
        }

        [Test]
        public void CreateSettings_LockedRoomRequiresPassword()
        {
            var request = new RoomCreateRequest("잠금방", true, " ", 6, "market-01");

            Assert.That(
                request.TryCreateSettings(6, out _, out var error),
                Is.False);
            Assert.That(error, Is.EqualTo(RoomSettingsError.PasswordRequired));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void CreateSettings_AllowsTwoToSixPlayers(int maxPlayers)
        {
            var request = new RoomCreateRequest(
                "인원 설정방",
                false,
                null,
                maxPlayers,
                "market-01");

            Assert.That(
                request.TryCreateSettings(6, out var settings, out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(RoomSettingsError.None));
            Assert.That(settings.MaxPlayers, Is.EqualTo(maxPlayers));
        }

        [TestCase(1)]
        [TestCase(7)]
        public void CreateSettings_RejectsPlayerCountOutsideRange(int maxPlayers)
        {
            var request = new RoomCreateRequest(
                "잘못된 인원방",
                false,
                null,
                maxPlayers,
                "market-01");

            Assert.That(
                request.TryCreateSettings(6, out _, out var error),
                Is.False);
            Assert.That(error, Is.EqualTo(RoomSettingsError.InvalidPlayerCount));
        }

        [Test]
        public void RoomSummary_ReportsWhetherRoomCanBeJoined()
        {
            var settings = CreateSettings();

            Assert.That(new RoomSummary("room-1", settings, 5, true).CanJoin, Is.True);
            Assert.That(new RoomSummary("room-1", settings, 6, true).CanJoin, Is.False);
            Assert.That(new RoomSummary("room-1", settings, 5, false).CanJoin, Is.False);
            Assert.That(
                new RoomSummary("room-1", settings, 5, true, RoomStatus.Playing).CanJoin,
                Is.False);
        }

        [Test]
        public void RoomSummary_FiltersByTitleIgnoringCaseAndWhitespace()
        {
            var request = new RoomCreateRequest(
                "Test Room",
                false,
                null,
                6,
                "market-01");
            request.TryCreateSettings(6, out var settings, out _);
            var summary = new RoomSummary("room-1", settings, 1, true);

            Assert.That(summary.MatchesTitle(" test "), Is.True);
            Assert.That(summary.MatchesTitle("없는방"), Is.False);
            Assert.That(summary.MatchesTitle("   "), Is.True);
        }

        [Test]
        public void JoinRequest_ValidatesRoomCodeAndLockedRoomPassword()
        {
            Assert.That(
                new RoomJoinRequest(" ", null).TryValidate(false, out var roomIdError),
                Is.False);
            Assert.That(roomIdError, Is.EqualTo(RoomJoinRequestError.RoomIdRequired));

            Assert.That(
                new RoomJoinRequest("room-1", " ").TryValidate(true, out var passwordError),
                Is.False);
            Assert.That(passwordError, Is.EqualTo(RoomJoinRequestError.PasswordRequired));

            Assert.That(
                new RoomJoinRequest(" room-1 ", "secret").TryValidate(true, out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(RoomJoinRequestError.None));
        }

        [Test]
        public void RoomBrowser_FindsRoomByExactCodeIgnoringCaseAndWhitespace()
        {
            using var browser = new RoomBrowserSystem();
            browser.SetRooms(new[]
            {
                new RoomSummary("ROOM-A1", CreateSettings(), 1, true),
                new RoomSummary("ROOM-A10", CreateSettings(), 2, true)
            });

            Assert.That(browser.TryFindByCode(" room-a1 ", out var room), Is.True);
            Assert.That(room.RoomId, Is.EqualTo("ROOM-A1"));
            Assert.That(browser.TryFindByCode("ROOM-A", out _), Is.False);
            Assert.That(browser.TryFindByCode(" ", out _), Is.False);
        }

        [Test]
        public void RoomBrowser_PublishesRoomListAndSessionState()
        {
            using var browser = new RoomBrowserSystem();
            IReadOnlyList<RoomSummary> visibleRooms = null;
            using var subscription = browser.Rooms.Subscribe(rooms => visibleRooms = rooms);

            browser.SetRooms(new[]
            {
                new RoomSummary("NEW-ROOM", CreateSettings(), 3, true)
            });
            browser.PlayerCountChanged(2, 6);

            Assert.That(visibleRooms.Count, Is.EqualTo(1));
            Assert.That(visibleRooms[0].RoomId, Is.EqualTo("NEW-ROOM"));
            Assert.That(visibleRooms[0].CurrentPlayerCount, Is.EqualTo(3));
            Assert.That(browser.PlayerCount.CurrentValue, Is.EqualTo(2));
            Assert.That(browser.MaxPlayers.CurrentValue, Is.EqualTo(6));

            browser.RoomClosed(RoomExitReason.HostClosed);

            Assert.That(browser.PlayerCount.CurrentValue, Is.Zero);
            Assert.That(browser.MaxPlayers.CurrentValue, Is.Zero);
            Assert.That(browser.LastExit.CurrentValue, Is.EqualTo(RoomExitReason.HostClosed));
            Assert.That(
                () => browser.SetRooms(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void RoomUiCommands_ForwardsRequestsAndPublishesResults()
        {
            using var state = new RoomBrowserSystem();
            var browser = new FakeRoomBrowser();
            var commands = new RoomUiCommands(browser, state);
            var busyWhileCallingAdapter = false;
            browser.BeforeRequest = () =>
                busyWhileCallingAdapter |= state.IsBusy.CurrentValue;

            var refreshFailure = commands.RefreshAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(refreshFailure, Is.EqualTo(RoomEntryFailure.None));
            Assert.That(browser.RefreshCount, Is.EqualTo(1));
            Assert.That(busyWhileCallingAdapter, Is.True);
            Assert.That(state.IsBusy.CurrentValue, Is.False);

            browser.NextEntryResult = RoomEntryResult.Opened("ROOM-123");
            var createResult = commands.CreateAsync(
                    new RoomCreateRequest("방", false, null, 6, "market-01"),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(createResult.Ok, Is.True);
            Assert.That(state.RoomCode.CurrentValue, Is.EqualTo("ROOM-123"));

            browser.NextEntryResult = RoomEntryResult.Failed(RoomEntryFailure.WrongPassword);
            var enterResult = commands.EnterAsync(
                    new RoomId("ROOM-123"),
                    "wrong",
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(enterResult.Ok, Is.False);
            Assert.That(state.LastFailure.CurrentValue, Is.EqualTo(RoomEntryFailure.WrongPassword));
            Assert.That(browser.CreateCount, Is.EqualTo(1));
            Assert.That(browser.EnterCount, Is.EqualTo(1));
        }

        [Test]
        public void RoomUiApi_ConnectsCommandsAdapterAndObservableState()
        {
            using var state = new RoomBrowserSystem();
            var browser = new FakeRoomBrowser
            {
                RoomListSink = state,
                SessionSink = state,
                RefreshedRooms = new[]
                {
                    new RoomSummary("ROOM-API", CreateSettings(), 2, true)
                }
            };
            var commands = new RoomUiCommands(browser, state);

            Assert.That(
                commands.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult(),
                Is.EqualTo(RoomEntryFailure.None));
            Assert.That(state.Rooms.CurrentValue.Count, Is.EqualTo(1));
            Assert.That(state.Rooms.CurrentValue[0].RoomId, Is.EqualTo("ROOM-API"));

            browser.NextEntryResult = RoomEntryResult.Opened("ROOM-NEW");
            Assert.That(
                commands.CreateAsync(
                        new RoomCreateRequest("새 방", false, null, 6, "market-01"),
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    .Ok,
                Is.True);
            Assert.That(state.RoomCode.CurrentValue, Is.EqualTo("ROOM-NEW"));
            Assert.That(state.PlayerCount.CurrentValue, Is.EqualTo(1));
            Assert.That(state.MaxPlayers.CurrentValue, Is.EqualTo(6));

            commands.LeaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(state.RoomCode.CurrentValue, Is.Null);
            Assert.That(state.PlayerCount.CurrentValue, Is.Zero);
            Assert.That(state.MaxPlayers.CurrentValue, Is.Zero);
            Assert.That(state.LastExit.CurrentValue, Is.EqualTo(RoomExitReason.Left));
            Assert.That(state.IsBusy.CurrentValue, Is.False);
        }

        [Test]
        public void LobbyAndSummary_RejectDefaultSettings()
        {
            Assert.That(
                () => new RoomSummary("room-1", default, 0, true),
                Throws.ArgumentException);
            Assert.That(
                () => new RoomLobbySystem(default, "host", 0),
                Throws.ArgumentException);
        }

        [Test]
        public void TryStart_AllowsOnlyHostWithAtLeastTwoPlayers()
        {
            var lobby = new RoomLobbySystem(CreateSettings(), "host", 2);
            var eventCount = 0;
            lobby.Started += _ => eventCount++;

            Assert.That(lobby.TryStart("guest"), Is.EqualTo(RoomStartResult.NotHost));
            Assert.That(lobby.TryStart("host"), Is.EqualTo(RoomStartResult.Started));
            Assert.That(lobby.TryStart("host"), Is.EqualTo(RoomStartResult.AlreadyStarted));
            Assert.That(lobby.IsStarted, Is.True);
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void TryStart_RejectsHostUntilTwoPlayersArePresent()
        {
            var lobby = new RoomLobbySystem(CreateSettings(), "host", 1);

            Assert.That(
                lobby.TryStart("host"),
                Is.EqualTo(RoomStartResult.NotEnoughPlayers));

            lobby.UpdatePlayerCount(2);

            Assert.That(lobby.TryStart("host"), Is.EqualTo(RoomStartResult.Started));
        }

        [Test]
        public void TryStart_UsesUpdatedHost()
        {
            var lobby = new RoomLobbySystem(CreateSettings(), "old-host", 6);

            lobby.UpdateHost("new-host");

            Assert.That(lobby.TryStart("old-host"), Is.EqualTo(RoomStartResult.NotHost));
            Assert.That(lobby.TryStart("new-host"), Is.EqualTo(RoomStartResult.Started));
        }

        private static RoomSettings CreateSettings()
        {
            var request = new RoomCreateRequest(
                "테스트방",
                false,
                null,
                6,
                "market-01");
            request.TryCreateSettings(6, out var settings, out _);
            return settings;
        }

        private sealed class FakeRoomBrowser : IRoomBrowser
        {
            public Action BeforeRequest;
            public IRoomListSink RoomListSink;
            public IRoomSessionSink SessionSink;
            public IReadOnlyList<RoomSummary> RefreshedRooms = Array.Empty<RoomSummary>();
            public RoomEntryFailure RefreshResult = RoomEntryFailure.None;
            public RoomEntryResult NextEntryResult = RoomEntryResult.Entered();
            public int RefreshCount;
            public int CreateCount;
            public int EnterCount;

            public UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation)
            {
                RefreshCount++;
                BeforeRequest?.Invoke();
                RoomListSink?.SetRooms(RefreshedRooms);
                return UniTask.FromResult(RefreshResult);
            }

            public UniTask<RoomEntryResult> CreateAsync(
                RoomCreateRequest request,
                CancellationToken cancellation)
            {
                CreateCount++;
                BeforeRequest?.Invoke();
                if (NextEntryResult.Ok)
                {
                    SessionSink?.PlayerCountChanged(1, request.MaxPlayers);
                }

                return UniTask.FromResult(NextEntryResult);
            }

            public UniTask<RoomEntryResult> EnterAsync(
                RoomId room,
                string password,
                CancellationToken cancellation)
            {
                EnterCount++;
                BeforeRequest?.Invoke();
                return UniTask.FromResult(NextEntryResult);
            }

            public UniTask<RoomEntryResult> EnterByCodeAsync(
                string roomCode,
                string password,
                CancellationToken cancellation)
            {
                BeforeRequest?.Invoke();
                return UniTask.FromResult(NextEntryResult);
            }

            public UniTask LeaveAsync(CancellationToken cancellation)
            {
                BeforeRequest?.Invoke();
                SessionSink?.RoomClosed(RoomExitReason.Left);
                return UniTask.CompletedTask;
            }
        }
    }
}
