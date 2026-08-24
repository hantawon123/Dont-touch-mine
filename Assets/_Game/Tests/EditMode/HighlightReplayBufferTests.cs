using Game.Server.Items;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HighlightReplayBufferTests
    {
        [Test]
        public void TryRecord_CopiesPlayerAndWorldObjectState()
        {
            var buffer = new HighlightReplayBuffer(0.1d, 360d);
            var playerPoses = new[] { new Pose(Vector3.one, Quaternion.identity) };
            var worldObjects = new[]
            {
                new WorldObjectState("item", new Pose(Vector3.right, Quaternion.identity))
            };

            Assert.That(buffer.TryRecord(10d, playerPoses, worldObjects), Is.True);
            playerPoses[0] = Pose.identity;
            worldObjects[0] = new WorldObjectState("item", Pose.identity);

            var frame = buffer.Capture(10d, 10d)[0];
            Assert.That(frame.PlayerPoses[0].position, Is.EqualTo(Vector3.one));
            Assert.That(frame.WorldObjects[0].Pose.position, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void TryRecord_UsesSampleIntervalAndTrimsExpiredFrames()
        {
            var buffer = new HighlightReplayBuffer(1d, 2d);

            Assert.That(buffer.TryRecord(10d, EmptyPoses(), EmptyObjects()), Is.True);
            Assert.That(buffer.TryRecord(10.5d, EmptyPoses(), EmptyObjects()), Is.False);
            Assert.That(buffer.TryRecord(11d, EmptyPoses(), EmptyObjects()), Is.True);
            Assert.That(buffer.TryRecord(13d, EmptyPoses(), EmptyObjects()), Is.True);

            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.Capture(0d, 20d)[0].RecordedAt, Is.EqualTo(11d));
        }

        [Test]
        public void Capture_ReturnsOnlyFramesInsideInclusiveRange()
        {
            var buffer = new HighlightReplayBuffer(1d, 10d);
            buffer.TryRecord(1d, EmptyPoses(), EmptyObjects());
            buffer.TryRecord(2d, EmptyPoses(), EmptyObjects());
            buffer.TryRecord(3d, EmptyPoses(), EmptyObjects());

            var captured = buffer.Capture(2d, 3d);

            Assert.That(captured, Has.Length.EqualTo(2));
            Assert.That(captured[0].RecordedAt, Is.EqualTo(2d));
            Assert.That(captured[1].RecordedAt, Is.EqualTo(3d));
        }

        private static Pose[] EmptyPoses()
        {
            return new Pose[0];
        }

        private static WorldObjectState[] EmptyObjects()
        {
            return new WorldObjectState[0];
        }
    }
}
