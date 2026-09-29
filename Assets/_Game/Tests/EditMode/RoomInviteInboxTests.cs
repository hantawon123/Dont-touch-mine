using System.Linq;
using Game.Core.Home;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// How invitations queue up on the home screen: three at a time, the newest
    /// pushing out the oldest, one card per friend, and each leaving on its own
    /// after a while.
    /// </summary>
    public sealed class RoomInviteInboxTests
    {
        [Test]
        public void AnInvite_IsShownAsItArrives()
        {
            var inbox = new RoomInviteInbox();

            var invite = inbox.Receive("p1", "친구", "ROOM01");

            Assert.That(inbox.Visible.Select(v => v.Id), Is.EqualTo(new[] { invite.Id }));
            Assert.That(invite.FromNickname, Is.EqualTo("친구"));
            Assert.That(invite.RoomCode, Is.EqualTo("ROOM01"));
        }

        [Test]
        public void AFourthInvite_PushesOutTheOldest()
        {
            var inbox = new RoomInviteInbox();
            inbox.Receive("p1", "하나", "R1");
            inbox.Receive("p2", "둘", "R2");
            inbox.Receive("p3", "셋", "R3");

            inbox.Receive("p4", "넷", "R4");

            Assert.That(inbox.PendingCount, Is.EqualTo(RoomInviteInbox.VisibleLimit));
            Assert.That(
                inbox.Visible.Select(v => v.FromNickname), Is.EqualTo(new[] { "둘", "셋", "넷" }));
        }

        [Test]
        public void TheSameFriendAgain_RefreshesTheCardInPlace()
        {
            var inbox = new RoomInviteInbox();
            var first = inbox.Receive("p1", "하나", "R1");
            inbox.Receive("p2", "둘", "R2");

            // The server keeps one invitation per friend, so this is the same
            // invitation asked again rather than a second one.
            var again = inbox.Receive("p1", "하나", "R9");

            Assert.That(inbox.PendingCount, Is.EqualTo(2), "it stacked a second card");
            Assert.That(again.Id, Is.EqualTo(first.Id), "the id moved under a press already sent");
            Assert.That(inbox.Visible[0].Id, Is.EqualTo(first.Id), "the column reshuffled");
            Assert.That(inbox.Visible[0].RoomCode, Is.EqualTo("R9"), "it kept the old room");
        }

        [Test]
        public void AskingAgain_RestartsTheClock()
        {
            var inbox = new RoomInviteInbox();
            inbox.Receive("p1", "하나", "R1");
            inbox.Advance(RoomInviteInbox.Seconds - 1f);

            inbox.Receive("p1", "하나", "R1");
            inbox.Advance(RoomInviteInbox.Seconds - 1f);

            Assert.That(inbox.Visible.Count, Is.EqualTo(1), "it expired on the old clock");
        }

        [Test]
        public void AnInvite_GoesWhenItsTimeIsUp()
        {
            var inbox = new RoomInviteInbox();
            inbox.Receive("p1", "하나", "R1");

            inbox.Advance(RoomInviteInbox.Seconds - 1f);
            Assert.That(inbox.Visible.Count, Is.EqualTo(1), "it went early");

            inbox.Advance(2f);

            Assert.That(inbox.Visible, Is.Empty);
        }

        [Test]
        public void OnlyTheInvitesThatRanOut_AreDropped()
        {
            var inbox = new RoomInviteInbox();
            inbox.Receive("p1", "하나", "R1");
            inbox.Advance(RoomInviteInbox.Seconds - 1f);
            inbox.Receive("p2", "둘", "R2");

            inbox.Advance(2f);

            Assert.That(inbox.Visible.Select(v => v.FromNickname), Is.EqualTo(new[] { "둘" }));
        }

        [Test]
        public void AnsweringOne_LeavesTheRest()
        {
            var inbox = new RoomInviteInbox();
            inbox.Receive("p1", "하나", "R1");
            var second = inbox.Receive("p2", "둘", "R2");
            inbox.Receive("p3", "셋", "R3");

            Assert.That(inbox.Take(second.Id, out var taken), Is.True);

            Assert.That(taken.RoomCode, Is.EqualTo("R2"));
            Assert.That(
                inbox.Visible.Select(v => v.FromNickname), Is.EqualTo(new[] { "하나", "셋" }));
        }

        [Test]
        public void TakingTwice_SaysTheSecondPressFoundNothing()
        {
            var inbox = new RoomInviteInbox();
            var invite = inbox.Receive("p1", "친구", "R1");

            Assert.That(inbox.Take(invite.Id, out _), Is.True);
            Assert.That(inbox.Take(invite.Id, out _), Is.False);
            Assert.That(inbox.Visible, Is.Empty);
        }

        [Test]
        public void ANamelessSender_StillReadsAsSomeone()
        {
            var inbox = new RoomInviteInbox();

            var invite = inbox.Receive("p1", "", "R1");

            Assert.That(invite.FromNickname, Is.Not.Empty);
        }

        [Test]
        public void Changed_FiresOnArrivalAndOnAnswer_NotOnAQuietTick()
        {
            var inbox = new RoomInviteInbox();
            var fired = 0;
            inbox.Changed += () => fired++;

            var invite = inbox.Receive("p1", "친구", "R1");
            inbox.Advance(1f);
            inbox.Take(invite.Id, out _);
            inbox.Advance(1f);
            inbox.Clear();

            Assert.That(fired, Is.EqualTo(2));
        }
    }
}
