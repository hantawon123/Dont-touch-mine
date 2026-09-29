using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// When a roll of the wheel counts as choosing the wheel for an action.
    /// </summary>
    public sealed class ScrollCaptureFilterTests
    {
        private ScrollCaptureFilter filter;

        [SetUp]
        public void SetUp()
        {
            filter = new ScrollCaptureFilter();
        }

        [Test]
        public void AWheelAtRest_MeansNothing()
        {
            Assert.That(filter.Feed(0f), Is.Null);
            Assert.That(filter.Feed(0.001f), Is.Null, "Noise, not a notch.");
        }

        [Test]
        public void OneNotch_IsNotEnough()
        {
            Assert.That(
                filter.Feed(120f),
                Is.Null,
                "The page itself scrolls, so one nudge is too easy to give by accident.");
        }

        [Test]
        public void TwoNotchesUp_ChooseTheWheelUpwards()
        {
            filter.Feed(120f);

            Assert.That(filter.Feed(120f), Is.EqualTo(ControlCatalog.ScrollUp));
        }

        [Test]
        public void TwoNotchesDown_ChooseTheWheelDownwards()
        {
            filter.Feed(-1f);

            Assert.That(filter.Feed(-1f), Is.EqualTo(ControlCatalog.ScrollDown));
        }

        /// <summary>
        /// The size of a notch differs by platform — a hundred and twenty on
        /// Windows, one elsewhere — so only its sign is read.
        /// </summary>
        [Test]
        public void HowBigANotchIs_DoesNotMatter()
        {
            filter.Feed(0.5f);

            Assert.That(filter.Feed(9999f), Is.EqualTo(ControlCatalog.ScrollUp));
        }

        [Test]
        public void ChangingDirection_StartsTheCountAgain()
        {
            filter.Feed(1f);

            Assert.That(filter.Feed(-1f), Is.Null, "The first notch went the other way.");
            Assert.That(filter.Feed(-1f), Is.EqualTo(ControlCatalog.ScrollDown));
        }

        [Test]
        public void FramesAtRestBetweenNotches_DoNotBreakTheRoll()
        {
            filter.Feed(1f);
            filter.Feed(0f);
            filter.Feed(0f);

            Assert.That(
                filter.Feed(1f),
                Is.EqualTo(ControlCatalog.ScrollUp),
                "A wheel reports nothing between notches, which is most frames.");
        }

        [Test]
        public void AfterChoosing_TheNextRollStartsOver()
        {
            filter.Feed(1f);
            Assert.That(filter.Feed(1f), Is.EqualTo(ControlCatalog.ScrollUp));

            Assert.That(filter.Feed(1f), Is.Null, "Counting begins again.");
            Assert.That(filter.Feed(1f), Is.EqualTo(ControlCatalog.ScrollUp));
        }

        [Test]
        public void Reset_ForgetsAPartlyRolledWheel()
        {
            filter.Feed(1f);

            filter.Reset();

            Assert.That(filter.Feed(1f), Is.Null);
        }
    }
}
