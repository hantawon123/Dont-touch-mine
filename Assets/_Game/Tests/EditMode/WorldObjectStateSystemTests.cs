using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class WorldObjectStateSystemTests
    {
        [Test]
        public void UpdatedPose_IsKeptInSnapshot()
        {
            var system = new WorldObjectStateSystem(new[]
            {
                new WorldObjectState("shelf", new Pose(Vector3.zero, Quaternion.identity))
            });
            var movedPose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));

            Assert.That(system.TrySetPose("shelf", movedPose), Is.True);

            var snapshot = system.CaptureSnapshot();
            Assert.That(snapshot, Has.Length.EqualTo(1));
            Assert.That(snapshot[0].Pose.position, Is.EqualTo(movedPose.position));
            Assert.That(snapshot[0].Pose.rotation, Is.EqualTo(movedPose.rotation));
        }

        [Test]
        public void DestroyedObject_CannotMoveAgain()
        {
            var system = new WorldObjectStateSystem(new[]
            {
                new WorldObjectState("box", new Pose(Vector3.zero, Quaternion.identity))
            });

            Assert.That(system.TryDestroy("box"), Is.True);
            Assert.That(system.TrySetPose("box", new Pose(Vector3.one, Quaternion.identity)), Is.False);
            Assert.That(system.TryGetState("box", out var state), Is.True);
            Assert.That(state.IsDestroyed, Is.True);
        }
    }
}
