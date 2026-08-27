using System;
using Game.Server.Players;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MatchPlayerRosterTests
    {
        [Test]
        public void Constructor_PreservesParticipantOrderAndResolvesPlayerId()
        {
            var roster = new MatchPlayerRoster(new[]
            {
                " player-0 ",
                "player-1",
                "player-2",
                "player-3",
                "player-4",
                "player-5"
            });

            Assert.That(roster.Players.Count, Is.EqualTo(6));
            Assert.That(roster.GetPlayer(0).PlayerId, Is.EqualTo("player-0"));
            Assert.That(roster.GetPlayer(5).PlayerIndex, Is.EqualTo(5));
            Assert.That(roster.TryGetPlayerIndex(" player-4 ", out var index), Is.True);
            Assert.That(index, Is.EqualTo(4));
            Assert.That(roster.TryGetPlayerIndex("missing", out index), Is.False);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void Constructor_AcceptsMinimumPlayerCount()
        {
            var roster = new MatchPlayerRoster(new[] { "player-0" });

            Assert.That(roster.Players.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryDeactivate_PreservesPlayerIndexAndUpdatesActiveCountOnce()
        {
            var roster = new MatchPlayerRoster(new[] { "player-0", "player-1" });

            Assert.That(roster.ActivePlayerCount, Is.EqualTo(2));
            Assert.That(roster.TryDeactivate(1), Is.True);
            Assert.That(roster.TryDeactivate(1), Is.False);
            Assert.That(roster.ActivePlayerCount, Is.EqualTo(1));
            Assert.That(roster.GetPlayer(1).PlayerIndex, Is.EqualTo(1));
            Assert.That(roster.GetPlayer(1).PlayerId, Is.EqualTo("player-1"));
            Assert.That(roster.GetPlayer(1).IsActive, Is.False);
            Assert.That(roster.IsActive(0), Is.True);
        }

        [Test]
        public void Constructor_RejectsInvalidParticipantList()
        {
            Assert.That(
                () => new MatchPlayerRoster(null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new MatchPlayerRoster(Array.Empty<string>()),
                Throws.ArgumentException);
            Assert.That(
                () => new MatchPlayerRoster(new[]
                {
                    "player-0",
                    "player-1",
                    "player-2",
                    "player-3",
                    "player-4",
                    "player-5",
                    "player-6"
                }),
                Throws.ArgumentException);
            Assert.That(
                () => new MatchPlayerRoster(new[]
                {
                    "player-0",
                    "player-1",
                    "player-2",
                    "player-3",
                    "player-4",
                    "player-4"
                }),
                Throws.ArgumentException);
            Assert.That(
                () => new MatchPlayerRoster(new[]
                {
                    "player-0",
                    "player-1",
                    "player-2",
                    "player-3",
                    "player-4",
                    " "
                }),
                Throws.ArgumentException);
        }
    }
}
