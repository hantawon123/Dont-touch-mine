using Game.Network.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class InteractionAuthorityRulesTests
    {
        private readonly InteractionAuthorityRules rules =
            new InteractionAuthorityRules();

        [Test]
        public void InteractionDistance_AcceptsBoundaryAndRejectsInvalidPositions()
        {
            Assert.That(
                rules.IsWithinInteractionDistance(Vector3.zero, Vector3.right * 2f),
                Is.True);
            Assert.That(
                rules.IsWithinInteractionDistance(
                    Vector3.zero,
                    Vector3.right * 2.01f),
                Is.False);
            Assert.That(
                rules.IsWithinInteractionDistance(
                    Vector3.zero,
                    new Vector3(float.NaN, 0f, 0f)),
                Is.False);
        }

        [Test]
        public void Release_RejectsDistantPositionAndInvalidRotation()
        {
            var playerPose = Pose.identity;

            Assert.That(
                rules.IsValidRelease(
                    playerPose,
                    new Pose(Vector3.right * 2f, Quaternion.identity)),
                Is.True);
            Assert.That(
                rules.IsValidRelease(
                    playerPose,
                    new Pose(Vector3.right * 2.01f, Quaternion.identity)),
                Is.False);
            Assert.That(
                rules.IsValidRelease(
                    playerPose,
                    new Pose(Vector3.zero, new Quaternion(0f, 0f, 0f, 0f))),
                Is.False);
        }

        [Test]
        public void Throw_AcceptsSpeedBoundaryAndRejectsInvalidVelocity()
        {
            var releasePose = new Pose(Vector3.right, Quaternion.identity);

            Assert.That(
                rules.IsValidThrow(
                    Pose.identity,
                    releasePose,
                    Vector3.forward * 8f),
                Is.True);
            Assert.That(
                rules.IsValidThrow(
                    Pose.identity,
                    releasePose,
                    Vector3.forward * 8.01f),
                Is.False);
            Assert.That(
                rules.IsValidThrow(
                    Pose.identity,
                    releasePose,
                    new Vector3(float.PositiveInfinity, 0f, 0f)),
                Is.False);
        }
    }
}
