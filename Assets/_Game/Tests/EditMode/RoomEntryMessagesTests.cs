using Game.Client.Common;
using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// What a refused entry tells the player to do next.
    /// </summary>
    public sealed class RoomEntryMessagesTests
    {
        /// <summary>
        /// The same failure, and two different things to do about it: refreshing
        /// the list finds nothing for a code that was mistyped, and re-reading
        /// the code does nothing for a room that closed a moment ago.
        /// </summary>
        [Test]
        public void MissingRoom_IsExplainedByHowThePlayerTriedToEnter()
        {
            var fromList = RoomEntryMessages.Describe(
                RoomEntryFailure.NotFound, RoomEntrySource.RoomList);
            var fromCode = RoomEntryMessages.Describe(
                RoomEntryFailure.NotFound, RoomEntrySource.RoomCode);

            Assert.That(fromList, Does.Contain("새로고침"));
            Assert.That(fromCode, Does.Contain("코드"));
            Assert.That(fromCode, Does.Not.Contain("새로고침"));
        }

        /// <summary>
        /// A code names one room, so there is no list of others to send the
        /// player back to.
        /// </summary>
        [Test]
        public void FullRoom_OnlySuggestsAnotherRoomWhenThereIsAListOfThem()
        {
            Assert.That(
                RoomEntryMessages.Describe(
                    RoomEntryFailure.Full, RoomEntrySource.RoomList),
                Does.Contain("다른 방"));

            Assert.That(
                RoomEntryMessages.Describe(
                    RoomEntryFailure.Full, RoomEntrySource.RoomCode),
                Does.Not.Contain("다른 방"));
        }

        [TestCase(RoomEntryFailure.ConnectionFailed)]
        [TestCase(RoomEntryFailure.Unknown)]
        [TestCase(RoomEntryFailure.CodeUnavailable)]
        public void FailuresThePlayerCannotActOn_ShareOneWording(
            RoomEntryFailure failure)
        {
            Assert.That(
                RoomEntryMessages.Describe(failure, RoomEntrySource.RoomList),
                Is.EqualTo(RoomEntryMessages.Generic));
        }

        [TestCase(RoomEntryFailure.NotFound)]
        [TestCase(RoomEntryFailure.Full)]
        [TestCase(RoomEntryFailure.Closed)]
        public void MakingARoom_IsNeverToldToLookAtTheList(RoomEntryFailure failure)
        {
            var said = RoomEntryMessages.Describe(failure, RoomEntrySource.RoomCreate);

            // These three are about a room somebody else made. Reaching them
            // from the create form means the room this player just asked for
            // did not survive being made, and there is no list to refresh, no
            // code to check and no other room to pick.
            Assert.That(said, Does.Not.Contain("목록"));
            Assert.That(said, Does.Not.Contain("코드"));
            Assert.That(said, Does.Not.Contain("다른 방"));
            Assert.That(said, Does.Not.Contain("골라"));
        }

        [Test]
        public void SettingsTheServerRefuses_AreOnlyMentionedToTheOneWhoTypedThem()
        {
            // The form checks the name and the player count before it sends, so
            // this only happens when the two disagree.
            Assert.That(
                RoomEntryMessages.Describe(
                    RoomEntryFailure.InvalidRequest, RoomEntrySource.RoomCreate),
                Is.EqualTo("방 설정을 확인해 주세요."));

            // Nobody entering a room chose its settings, so there is nothing
            // for them to go and fix.
            Assert.That(
                RoomEntryMessages.Describe(
                    RoomEntryFailure.InvalidRequest, RoomEntrySource.RoomList),
                Is.EqualTo(RoomEntryMessages.Generic));
        }

        [Test]
        public void EveryFailure_IsGivenSomethingToSayFromEveryDirection()
        {
            foreach (RoomEntrySource source in
                System.Enum.GetValues(typeof(RoomEntrySource)))
            {
                foreach (RoomEntryFailure failure in
                    System.Enum.GetValues(typeof(RoomEntryFailure)))
                {
                    Assert.That(
                        RoomEntryMessages.Describe(failure, source),
                        Is.Not.Null.And.Not.Empty,
                        $"{failure} has no wording from {source}.");
                }
            }
        }

        [Test]
        public void EveryFailure_IsGivenSomethingToSay()
        {
            foreach (RoomEntryFailure failure in
                System.Enum.GetValues(typeof(RoomEntryFailure)))
            {
                Assert.That(
                    RoomEntryMessages.Describe(failure, RoomEntrySource.RoomCode),
                    Is.Not.Null.And.Not.Empty,
                    $"{failure} has no wording.");
            }
        }
    }
}
