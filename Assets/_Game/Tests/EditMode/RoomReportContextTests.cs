using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Ports;
using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// The key a report carries so the server can hold it to one per person
    /// per match (S15P21D205-1018): the room code and how many matches this
    /// peer has seen start since coming in.
    /// </summary>
    public sealed class RoomReportContextTests
    {
        [Test]
        public void OutsideARoom_ThereIsNoKey()
        {
            using var room = new RoomBrowserSystem();
            using var context = new RoomReportContext(room);

            Assert.That(context.CurrentKey, Is.Null);
        }

        [Test]
        public void InARoom_BeforeAnyMatch_TheKeyEndsInZero()
        {
            using var room = new RoomBrowserSystem();
            using var context = new RoomReportContext(room);
            Enter(room, "7K2M9P");

            Assert.That(context.CurrentKey, Is.EqualTo("7K2M9P#0"));
        }

        /// <summary>
        /// The line-up is published when a match starts, published again on a
        /// host migration, and published empty when the room goes back to
        /// waiting. Only the first counts as a match; a migration or the
        /// return to the lobby must not open another report slot.
        /// </summary>
        [Test]
        public void EachMatchThatStarts_MovesTheKeyOnce()
        {
            using var room = new RoomBrowserSystem();
            using var context = new RoomReportContext(room);
            Enter(room, "7K2M9P");

            room.MatchStarted(LineUp());
            Assert.That(context.CurrentKey, Is.EqualTo("7K2M9P#1"));

            room.MatchStarted(LineUp());
            Assert.That(context.CurrentKey, Is.EqualTo("7K2M9P#1"), "Re-publishing the same match is not a new one.");

            room.MatchStarted(new MatchParticipant[0]);
            Assert.That(context.CurrentKey, Is.EqualTo("7K2M9P#1"), "Back to waiting is not a new one either.");

            room.MatchStarted(LineUp());
            Assert.That(context.CurrentKey, Is.EqualTo("7K2M9P#2"));
        }

        [Test]
        public void LeavingTheRoom_StartsTheCountOver()
        {
            using var room = new RoomBrowserSystem();
            using var context = new RoomReportContext(room);
            Enter(room, "7K2M9P");
            room.MatchStarted(LineUp());
            room.MatchStarted(new MatchParticipant[0]);

            room.RoomClosed(RoomExitReason.HostClosed);
            Assert.That(context.CurrentKey, Is.Null, "No room, no key.");

            Enter(room, "Q8R2T1");
            Assert.That(context.CurrentKey, Is.EqualTo("Q8R2T1#0"), "A new room counts from zero.");
        }

        private static MatchParticipant[] LineUp() =>
            new[] { new MatchParticipant("P1", 0, "user-1"), new MatchParticipant("P2", 1, "user-2") };

        /// <summary>Goes through the commands, the way the screen does, so the room records the entry itself.</summary>
        private static void Enter(RoomBrowserSystem room, string code)
        {
            var commands = new RoomUiCommands(new OpeningBrowser(code), room);
            commands.EnterByCodeAsync(code, null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(room.IsInRoom.CurrentValue, Is.True, "precondition: the room was entered");
        }

        private sealed class OpeningBrowser : IRoomBrowser
        {
            private readonly string code;

            public OpeningBrowser(string code)
            {
                this.code = code;
            }

            public UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryFailure.None);

            public UniTask<RoomEntryResult> CreateAsync(RoomCreateRequest request, CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryResult.Opened(code));

            public UniTask<RoomEntryResult> EnterAsync(RoomId room, string password, CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryResult.Opened(code));

            public UniTask<RoomEntryResult> EnterByCodeAsync(string roomCode, string password, CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryResult.Opened(code));

            public UniTask LeaveAsync(CancellationToken cancellation) => UniTask.CompletedTask;
        }
    }
}
