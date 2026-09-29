using System;
using Game.Core.Home;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The order the 친구 요청 tab draws requests in.
    /// </summary>
    /// <remarks>
    /// The server answers in whatever order its query returned, which is not
    /// the order the design asks for and is not guaranteed to be the same twice.
    /// A list that reshuffles between refreshes is a list a player cannot click
    /// reliably: the row they were reaching for moves out from under them.
    /// </remarks>
    public sealed class FriendRequestOrderTests
    {
        private static readonly DateTime Noon =
            new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void TheRequestThatArrivedLast_SitsAtTheTop()
        {
            var arranged = FriendRequestOrder.Arrange(new[]
            {
                new FriendRequestSummary("p1", "가나다", Noon.AddHours(-1)),
                new FriendRequestSummary("p2", "하하하", Noon)
            });

            Assert.That(arranged.Count, Is.EqualTo(2));
            Assert.That(
                arranged[0].Nickname,
                Is.EqualTo("하하하"),
                "이름순이 아니라 늦게 온 요청이 위로 와야 한다.");
        }

        [Test]
        public void RequestsThatArrivedTogether_FallBackToNameOrder()
        {
            var arranged = FriendRequestOrder.Arrange(new[]
            {
                new FriendRequestSummary("p1", "77칠칠", Noon),
                new FriendRequestSummary("p2", "banana", Noon),
                new FriendRequestSummary("p3", "가나다", Noon)
            });

            Assert.That(
                new[] { arranged[0].Nickname, arranged[1].Nickname, arranged[2].Nickname },
                Is.EqualTo(new[] { "가나다", "banana", "77칠칠" }));
        }

        [Test]
        public void TheSamePersonListedTwice_IsOneRow()
        {
            // Not a server bug to report: a request answered a moment ago can
            // still be in a list read just before that. Two rows for one person
            // means the second one does nothing when pressed.
            var arranged = FriendRequestOrder.Arrange(new[]
            {
                new FriendRequestSummary("p1", "두번보낸친구", Noon),
                new FriendRequestSummary("p1", "두번보낸친구", Noon.AddMinutes(-5))
            });

            Assert.That(arranged.Count, Is.EqualTo(1));
        }

        [Test]
        public void NothingToArrange_IsAnEmptyList()
        {
            Assert.That(FriendRequestOrder.Arrange(null), Is.Empty);
            Assert.That(
                FriendRequestOrder.Arrange(Array.Empty<FriendRequestSummary>()), Is.Empty);
        }

        [Test]
        public void ArrangingDoesNotDisturbWhatItWasGiven()
        {
            // The caller owns the list the gateway handed it, and a sort in
            // place would reorder a collection somebody else is still reading.
            var given = new[]
            {
                new FriendRequestSummary("p1", "가나다", Noon.AddHours(-1)),
                new FriendRequestSummary("p2", "하하하", Noon)
            };

            FriendRequestOrder.Arrange(given);

            Assert.That(given[0].Nickname, Is.EqualTo("가나다"));
        }
    }
}
