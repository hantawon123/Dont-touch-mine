using System;
using Game.Core.Home;
using Game.Core.Match;
using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// The backend account travels from the room roster into the match line-up
    /// unchanged, and its absence is spelled the way each layer's readers expect.
    /// </summary>
    /// <remarks>
    /// Two spellings on purpose. In the room it is an empty string, like the
    /// nickname, so presentation never checks for null. In the match it is null,
    /// because that value goes to the backend as-is and the backend reads a
    /// missing account as null. Pinning both keeps a publisher from having to
    /// translate, and from getting it wrong.
    /// </remarks>
    public sealed class ParticipantUserIdTests
    {
        private const string Account = "0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0";

        [Test]
        public void RoomParticipant_KeepsTheAccountAndTrimsIt()
        {
            var participant = new RoomParticipant("P3", 2, false, "이름", "  " + Account + " ");

            Assert.That(participant.UserId, Is.EqualTo(Account));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void RoomParticipant_WithoutAnAccount_ReadsEmptyNotNull(string presented)
        {
            var participant = new RoomParticipant("P3", 2, false, "이름", presented);

            // Like the nickname: consumers treat it as "empty or a real id".
            Assert.That(participant.UserId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void RoomParticipant_DefaultsToNoAccount_ForCallersThatOnlyKnowSeats()
        {
            var participant = new RoomParticipant("P3", 2, false);

            Assert.That(participant.UserId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void MatchParticipant_KeepsTheAccount()
        {
            var participant = new MatchParticipant("P3", 0, Account);

            Assert.That(participant.UserId, Is.EqualTo(Account));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void MatchParticipant_WithoutAnAccount_ReadsNull(string presented)
        {
            var participant = new MatchParticipant("P3", 0, presented);

            // What the host puts on the wire. The backend reads a missing
            // account as null, so nothing between here and there has to convert.
            Assert.That(participant.UserId, Is.Null);
        }

        [Test]
        public void MatchParticipant_StillIdentifiesBySeatIdNotAccount()
        {
            // The account is for following a person across rooms. Seats,
            // authority and match arrays keep keying on the room-scoped id.
            var participant = new MatchParticipant("P3", 4, Account);

            Assert.That(participant.PlayerId, Is.EqualTo("P3"));
            Assert.That(participant.PlayerIndex, Is.EqualTo(4));
        }

        [Test]
        public void FromRoomParticipants_CarriesEachAccountToItsOwnIndex()
        {
            // Seats out of order, so a wrong join would be visible: the account
            // must follow its player through the re-indexing, not its position.
            var participants = MatchParticipant.FromRoomParticipants(new[]
            {
                new RoomParticipant("late", 5, false, "늦은", "late-account"),
                new RoomParticipant("host", 0, true, "방장", "host-account"),
                new RoomParticipant("guest", 3, false, "손님"),
            });

            Assert.That(
                Array.ConvertAll(participants, p => p.PlayerId),
                Is.EqualTo(new[] { "host", "guest", "late" }));
            Assert.That(
                Array.ConvertAll(participants, p => p.UserId),
                Is.EqualTo(new[] { "host-account", null, "late-account" }));
        }

        [Test]
        public void Profile_AdoptsTheAccountOnceAndRaisesChanged()
        {
            var profile = new PlayerProfile("나");
            var changes = 0;
            profile.Changed += _ => changes++;

            profile.AdoptUserId(Account);
            profile.AdoptUserId(Account);

            Assert.That(profile.UserId, Is.EqualTo(Account));
            Assert.That(changes, Is.EqualTo(1), "the same account twice is not a change");
        }

        [Test]
        public void Profile_StartsWithoutAnAccountAndCanForgetIt()
        {
            var profile = new PlayerProfile("나");
            Assert.That(profile.UserId, Is.EqualTo(string.Empty));

            profile.AdoptUserId(Account);
            profile.AdoptUserId(null);

            // Sign-in failed, or the account was deleted. Empty, never null,
            // so it can go straight into a connection token.
            Assert.That(profile.UserId, Is.EqualTo(string.Empty));
        }
    }
}
