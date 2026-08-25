using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;
using NUnit.Framework;

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
            var browser = new RoomBrowserSystem();
            browser.ReplaceRooms(new[]
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
        public void RoomBrowser_RefreshRequestReplacesVisibleRoomList()
        {
            var browser = new RoomBrowserSystem();
            var requestCount = 0;
            var changedCount = 0;
            IReadOnlyList<RoomSummary> visibleRooms = null;
            browser.ReplaceRooms(new[]
            {
                new RoomSummary("OLD-ROOM", CreateSettings(), 1, true)
            });
            browser.RefreshRequested += () =>
            {
                requestCount++;
                browser.ReplaceRooms(new[]
                {
                    new RoomSummary("NEW-ROOM", CreateSettings(), 3, true)
                });
            };
            browser.RoomsChanged += rooms =>
            {
                changedCount++;
                visibleRooms = rooms;
            };

            browser.RequestRefresh();

            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(visibleRooms, Is.SameAs(browser.Rooms));
            Assert.That(visibleRooms.Count, Is.EqualTo(1));
            Assert.That(visibleRooms[0].RoomId, Is.EqualTo("NEW-ROOM"));
            Assert.That(visibleRooms[0].CurrentPlayerCount, Is.EqualTo(3));
            Assert.That(
                () => browser.ReplaceRooms(null),
                Throws.TypeOf<ArgumentNullException>());
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
    }
}
