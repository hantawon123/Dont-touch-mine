using Game.Bootstrap;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class DedicatedServerStartupTests
    {
        [Test]
        public void WarmRoomSurvivesHours_ThenAllowsAFullClaimWindow()
        {
            double emptySince = 0;
            double? claimStartedAt = null;
            foreach (var now in new[] { 121d, 600d, 7200d })
                Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 0, now,
                    ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(claimStartedAt, Is.Null);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 1, 7201,
                ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 1, 7321,
                ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 1, 7322,
                ref emptySince, ref claimStartedAt), Is.True);
        }

        [Test]
        public void FailedClaimCannotBeKeptAliveByDisconnectingAndReconnecting()
        {
            double emptySince = 0;
            double? claimStartedAt = null;
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 1, 10,
                ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 0, 100,
                ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 1, 129,
                ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(true, 1, 131,
                ref emptySince, ref claimStartedAt), Is.True);
        }

        [Test]
        public void ClaimedRoomStaysAliveWhileOccupied_AndRetiresAfterEmptyTimeout()
        {
            double emptySince = 0;
            double? claimStartedAt = 10;
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(false, 4, 7200,
                ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(false, 0, 7320,
                ref emptySince, ref claimStartedAt), Is.False);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(false, 0, 7321,
                ref emptySince, ref claimStartedAt), Is.True);
            Assert.That(DedicatedServerStartup.ShouldCloseIdleRoom(false, 1, 7321,
                ref emptySince, ref claimStartedAt), Is.False);
        }
    }
}
