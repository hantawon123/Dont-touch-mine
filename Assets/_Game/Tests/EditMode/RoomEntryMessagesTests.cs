using Game.Client.Rooms;
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
