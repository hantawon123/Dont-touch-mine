using Game.Network.Players;
using NUnit.Framework;

// Only the one type is imported: opening the whole Fusion namespace pulls in
// Fusion.Assert, which collides with NUnit's.
using PlayerRef = Fusion.PlayerRef;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Pins the seat mapping. Seats index spawn points and the match rules read
    /// players by seat number, so the two things that must never drift are that
    /// one seat holds exactly one player, and that a seat given back is the next
    /// one handed out.
    /// </summary>
    /// <remarks>
    /// Only the spawning peer keeps a registry, so none of this is replicated and
    /// all of it is verifiable without Fusion running.
    /// </remarks>
    public sealed class PlayerRegistryTests
    {
        /// <summary>A distinct real player, named the way Fusion names them.</summary>
        private static PlayerRef Player(int index)
        {
            return PlayerRef.FromIndex(index);
        }

        [Test]
        public void Seats_AreHandedOutInJoinOrderAndRoundTrip()
        {
            var registry = new PlayerRegistry();
            var host = Player(0);
            var guest = Player(1);

            Assert.That(registry.Add(host), Is.EqualTo(0));
            Assert.That(registry.Add(guest), Is.EqualTo(1));
            Assert.That(registry.Count, Is.EqualTo(2));

            Assert.That(registry.TryGetSeat(guest, out var seat), Is.True);
            Assert.That(seat, Is.EqualTo(1));

            Assert.That(registry.TryGetPlayer(seat, out var seated), Is.True);
            Assert.That(seated, Is.EqualTo(guest));
        }

        /// <summary>
        /// Fusion raises a join callback per peer and the spawner may ask twice,
        /// so a repeat must be answered with the seat already held rather than a
        /// second one.
        /// </summary>
        [Test]
        public void AddingTheSamePlayerTwice_KeepsOneSeat()
        {
            var registry = new PlayerRegistry();
            var player = Player(3);

            var first = registry.Add(player);
            var again = registry.Add(player);

            Assert.That(again, Is.EqualTo(first));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void SixPlayers_FillSeatsZeroToFiveWithoutSharing()
        {
            var registry = new PlayerRegistry();
            var seats = new int[6];

            for (var i = 0; i < seats.Length; i++)
            {
                seats[i] = registry.Add(Player(i));
            }

            Assert.That(seats, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(seats, Is.Unique);
            Assert.That(registry.Count, Is.EqualTo(6));
        }

        /// <summary>
        /// The lowest free seat is taken so the numbers stay dense. A gap left by
        /// someone leaving must be filled before the count grows, otherwise a
        /// spawn point past the end of the map's list gets asked for.
        /// </summary>
        [Test]
        public void LeavingFreesTheSeat_AndTheNextJoinerTakesIt()
        {
            var registry = new PlayerRegistry();
            registry.Add(Player(0));
            var middle = Player(1);
            registry.Add(middle);
            registry.Add(Player(2));

            Assert.That(registry.Remove(middle), Is.True);
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.TryGetSeat(middle, out _), Is.False);

            Assert.That(registry.Add(Player(9)), Is.EqualTo(1));
        }

        /// <summary>
        /// Churn must not let seat numbers creep upward. Six is the room limit,
        /// so after any sequence of joins and leaves every seat still indexes a
        /// spawn point that exists.
        /// </summary>
        [Test]
        public void SeatsStayWithinThePeakHeadcount_AcrossChurn()
        {
            var registry = new PlayerRegistry();

            for (var i = 0; i < 6; i++)
            {
                registry.Add(Player(i));
            }

            Assert.That(registry.Remove(Player(5)), Is.True);
            Assert.That(registry.Remove(Player(4)), Is.True);
            Assert.That(registry.Remove(Player(0)), Is.True);

            var rejoined = new[]
            {
                registry.Add(Player(10)),
                registry.Add(Player(11)),
                registry.Add(Player(12)),
            };

            Assert.That(rejoined, Is.Unique);
            Assert.That(rejoined, Is.All.InRange(0, 5));
            Assert.That(registry.Count, Is.EqualTo(6));
        }

        [Test]
        public void Restore_KeepsSnapshotSeatAndRejectsCollisions()
        {
            var registry = new PlayerRegistry();
            var migratedHost = Player(2);

            Assert.That(registry.Restore(migratedHost, 4), Is.True);
            Assert.That(registry.TryGetSeat(migratedHost, out var seat), Is.True);
            Assert.That(seat, Is.EqualTo(4));
            Assert.That(registry.TryGetPlayer(4, out var seated), Is.True);
            Assert.That(seated, Is.EqualTo(migratedHost));

            Assert.That(registry.Restore(migratedHost, 3), Is.False);
            Assert.That(registry.Restore(Player(3), 4), Is.False);
            Assert.That(registry.Restore(Player(4), 6), Is.False);
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownSeatOrPlayer_IsReportedNotGuessed()
        {
            var registry = new PlayerRegistry();
            var host = Player(0);
            registry.Add(host);
            registry.Add(Player(1));

            Assert.That(registry.Remove(Player(7)), Is.False);

            Assert.That(registry.TryGetPlayer(-1, out var below), Is.False);
            Assert.That(below, Is.EqualTo(PlayerRef.None));

            Assert.That(registry.TryGetPlayer(99, out var above), Is.False);
            Assert.That(above, Is.EqualTo(PlayerRef.None));

            registry.Remove(host);
            Assert.That(registry.TryGetPlayer(0, out var freed), Is.False);
            Assert.That(freed, Is.EqualTo(PlayerRef.None));
        }

        /// <summary>
        /// The id is derived rather than stored so every peer spells it the same
        /// way without it being sent. Asserting the shape rather than a literal
        /// keeps this from pinning Fusion's encoding.
        /// </summary>
        [Test]
        public void IdOf_IsDerivedFromThePlayerRefAndPrefixed()
        {
            var player = Player(2);

            Assert.That(player.IsRealPlayer, Is.True);
            Assert.That(PlayerRegistry.IdOf(player), Is.EqualTo("P" + player.PlayerId));
            Assert.That(PlayerRegistry.IdOf(player), Is.EqualTo(PlayerRegistry.IdOf(player)));
            Assert.That(PlayerRegistry.IdOf(PlayerRef.None), Is.Null);
        }

        [Test]
        public void Clear_EmptiesEverything()
        {
            var registry = new PlayerRegistry();
            registry.Add(Player(0));
            registry.Add(Player(1));

            registry.Clear();

            Assert.That(registry.Count, Is.EqualTo(0));
            Assert.That(registry.TryGetPlayer(0, out _), Is.False);
            Assert.That(registry.Add(Player(4)), Is.EqualTo(0));
        }
    }
}
