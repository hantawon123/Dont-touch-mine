using Game.Bootstrap;
using Game.Core.Match;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class MatchSettingsOverlayTests
    {
        [Test]
        public void ShouldHandleEscape_OpensOrClosesFromGameplay()
        {
            Assert.That(
                MatchSettingsOverlay.ShouldHandleEscape(false, false, false, false),
                Is.True);
        }

        [Test]
        public void ShouldHandleEscape_IgnoresChatCaptureAndModals()
        {
            Assert.That(
                MatchSettingsOverlay.ShouldHandleEscape(true, false, false, false),
                Is.False);
            Assert.That(
                MatchSettingsOverlay.ShouldHandleEscape(false, true, false, false),
                Is.False);
            Assert.That(
                MatchSettingsOverlay.ShouldHandleEscape(false, false, true, false),
                Is.False);
            Assert.That(
                MatchSettingsOverlay.ShouldHandleEscape(false, false, false, true),
                Is.False);
        }

        [TestCase(MatchPhase.Highlight)]
        [TestCase(MatchPhase.Result)]
        public void ShouldHandleEscape_IgnoresHighlightAndResult(MatchPhase phase)
        {
            Assert.That(MatchSettingsOverlay.BlocksEscapeDuringPresentation(phase), Is.True);
            Assert.That(
                MatchSettingsOverlay.ShouldHandleEscape(false, false, false, false, true),
                Is.False);
        }

        [TestCase(MatchPhase.Hiding)]
        [TestCase(MatchPhase.Searching)]
        public void ShouldHandleEscape_AllowsGameplayPhases(MatchPhase phase)
        {
            Assert.That(MatchSettingsOverlay.BlocksEscapeDuringPresentation(phase), Is.False);
        }
    }
}
