using Game.Bootstrap;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class LoadingOverlayCoordinatorTests
    {
        [Test]
        public void ShouldShowForMatchStart_OnlyAtTheEndOfTheCountdown()
        {
            Assert.That(LoadingOverlayCoordinator.ShouldShowForMatchStart(10d), Is.False);
            Assert.That(LoadingOverlayCoordinator.ShouldShowForMatchStart(1d), Is.False);
            Assert.That(
                LoadingOverlayCoordinator.ShouldShowForMatchStart(
                    LoadingOverlayCoordinator.MatchStartWindowSeconds),
                Is.True);
            Assert.That(
                LoadingOverlayCoordinator.ShouldShowForMatchStart(
                    LoadingOverlayCoordinator.MatchStartWindowSeconds * 0.5d),
                Is.True);
            Assert.That(LoadingOverlayCoordinator.ShouldShowForMatchStart(0d), Is.False);
        }
    }
}
