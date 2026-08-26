using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Pins the nickname policy. A nickname is display-only and arrives late, so
    /// the rule is that it is either a real name or empty — never null, and never
    /// whitespace that would render as a blank row.
    /// </summary>
    public sealed class RoomParticipantTests
    {
        [Test]
        public void Nickname_IsEmptyWhenNotSupplied()
        {
            var participant = new RoomParticipant("P1", 0, true);

            Assert.That(participant.Nickname, Is.EqualTo(string.Empty));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Nickname_IsEmptyWhenBlank(string supplied)
        {
            var participant = new RoomParticipant("P1", 0, false, supplied);

            Assert.That(participant.Nickname, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Nickname_IsTrimmed()
        {
            var participant = new RoomParticipant("P1", 0, false, "  심재훈  ");

            Assert.That(participant.Nickname, Is.EqualTo("심재훈"));
        }

        /// <summary>
        /// The identifier and the name are separate on purpose: two people may
        /// choose the same name, so nothing may identify anyone by it.
        /// </summary>
        [Test]
        public void PlayerId_IsIndependentOfNickname()
        {
            var first = new RoomParticipant("P1", 0, false, "같은이름");
            var second = new RoomParticipant("P2", 1, false, "같은이름");

            Assert.That(first.PlayerId, Is.Not.EqualTo(second.PlayerId));
            Assert.That(first.Nickname, Is.EqualTo(second.Nickname));
        }

        [Test]
        public void SeatAndHostFlag_KeepTheirMeaning()
        {
            var participant = new RoomParticipant("P3", 4, true, "방장");

            Assert.That(participant.Seat, Is.EqualTo(4));
            Assert.That(participant.IsHost, Is.True);
        }
    }
}
