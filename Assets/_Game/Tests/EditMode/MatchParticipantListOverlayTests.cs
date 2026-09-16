using Game.Bootstrap;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class MatchParticipantListOverlayTests
    {
        [Test]
        public void ShouldHandleToggle_OpensFromGameplay()
        {
            Assert.That(
                MatchParticipantListOverlay.ShouldHandleToggle(false, false, false),
                Is.True);
        }

        [Test]
        public void ShouldHandleToggle_IgnoresChatSettingsAndPresentation()
        {
            Assert.That(
                MatchParticipantListOverlay.ShouldHandleToggle(true, false, false),
                Is.False);
            Assert.That(
                MatchParticipantListOverlay.ShouldHandleToggle(false, true, false),
                Is.False);
            Assert.That(
                MatchParticipantListOverlay.ShouldHandleToggle(false, false, true),
                Is.False);
            Assert.That(
                MatchParticipantListOverlay.ShouldHandleToggle(false, false, false, true),
                Is.False);
        }
    }
}
