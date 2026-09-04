using Game.Core.Lobby;
using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The rules the room list is read by: what order the rooms come in, and
    /// which of them a player can enter.
    /// </summary>
    /// <remarks>
    /// Both are close to impossible to check by hand. Seeing the order would
    /// take opening several rooms seconds apart, and seeing a full or playing
    /// room takes six players and a started match.
    /// </remarks>
    public sealed class RoomBrowserListingTests
    {
        [Test]
        public void Rooms_NewestFirst()
        {
            using var system = new RoomBrowserSystem();

            system.SetRooms(new[]
            {
                Room("오래된 방", openedAt: 100),
                Room("새 방", openedAt: 300),
                Room("중간 방", openedAt: 200),
            });

            Assert.That(
                Titles(system),
                Is.EqualTo(new[] { "새 방", "중간 방", "오래된 방" }));
        }

        /// <summary>
        /// Ordinal order would run the other way round: Unicode puts digits
        /// before Latin and Latin far before Hangul.
        /// </summary>
        [Test]
        public void Rooms_OpenedTogether_AreOrderedKoreanThenLatinThenDigits()
        {
            using var system = new RoomBrowserSystem();

            system.SetRooms(new[]
            {
                Room("2번 방", openedAt: 100),
                Room("apple room", openedAt: 100),
                Room("가나다 방", openedAt: 100),
                Room("Banana room", openedAt: 100),
                Room("ㅋㅋㅋ 방", openedAt: 100),
            });

            Assert.That(
                Titles(system),
                Is.EqualTo(new[]
                {
                    "가나다 방",
                    "ㅋㅋㅋ 방",
                    "apple room",
                    "Banana room",
                    "2번 방",
                }));
        }

        /// <summary>
        /// Matchmaking hands the rooms over in an order that moves between
        /// refreshes. Rows that swap places under the pointer are worse than
        /// rows in an arbitrary but steady order.
        /// </summary>
        [Test]
        public void Rooms_SharingNameAndTime_KeepTheSameOrderAcrossRefreshes()
        {
            using var system = new RoomBrowserSystem();
            var first = Room("같은 이름", openedAt: 100, roomId: "room-a");
            var second = Room("같은 이름", openedAt: 100, roomId: "room-b");

            system.SetRooms(new[] { first, second });
            var before = Ids(system);

            system.SetRooms(new[] { second, first });

            Assert.That(Ids(system), Is.EqualTo(before));
        }

        /// <summary>
        /// A room whose listing does not say when it opened sorts last. One of
        /// unknown age is not a new one.
        /// </summary>
        [Test]
        public void Rooms_WithoutAnOpeningTime_SortLast()
        {
            using var system = new RoomBrowserSystem();

            system.SetRooms(new[]
            {
                Room("시각 없는 방"),
                Room("시각 있는 방", openedAt: 1),
            });

            Assert.That(
                Titles(system),
                Is.EqualTo(new[] { "시각 있는 방", "시각 없는 방" }));
        }

        [Test]
        public void WaitingRoomWithSpaceLeft_CanBeEntered()
        {
            Assert.That(Room("대기중", players: 5).CanJoin, Is.True);
        }

        [Test]
        public void PlayingRoom_CannotBeEntered()
        {
            Assert.That(
                Room("게임중", players: 3, status: RoomStatus.Playing).CanJoin,
                Is.False);
        }

        [Test]
        public void FullRoom_CannotBeEntered_EvenWhileWaiting()
        {
            var room = Room("정원 초과", players: 6);

            Assert.That(room.Status, Is.EqualTo(RoomStatus.Waiting));
            Assert.That(room.IsFull, Is.True);
            Assert.That(room.CanJoin, Is.False);
        }

        [Test]
        public void ClosedRoom_CannotBeEntered()
        {
            Assert.That(Room("닫힌 방", isOpen: false).CanJoin, Is.False);
        }

        private static RoomSummary Room(
            string title,
            int players = 1,
            RoomStatus status = RoomStatus.Waiting,
            bool isOpen = true,
            int openedAt = 0,
            string roomId = null)
        {
            return new RoomSummary(
                new RoomId(roomId ?? title),
                title,
                "playground",
                players,
                RoomSettings.MaxPlayerCount,
                isLocked: false,
                isOpen: isOpen,
                status,
                hostNickname: "방장",
                openedAt: openedAt);
        }

        private static string[] Titles(RoomBrowserSystem system)
        {
            var rooms = system.Rooms.CurrentValue;
            var titles = new string[rooms.Count];
            for (var index = 0; index < rooms.Count; index++)
            {
                titles[index] = rooms[index].DisplayName;
            }

            return titles;
        }

        private static string[] Ids(RoomBrowserSystem system)
        {
            var rooms = system.Rooms.CurrentValue;
            var ids = new string[rooms.Count];
            for (var index = 0; index < rooms.Count; index++)
            {
                ids[index] = rooms[index].RoomId;
            }

            return ids;
        }
    }
}
