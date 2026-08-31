using Game.Client.Players;
using Game.Bootstrap;
using Game.Server.Match;
using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ReplayVisualTests
    {
        [Test]
        public void ReplayCopy_HasNoPhysics_AndNeverMovesOriginal()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.AddComponent<Rigidbody>();
            var visual = new ReplayVisual(source.transform, null);
            try
            {
                visual.SetPlaying(true);
                visual.Target.position = Vector3.one * 20f;
                Assert.That(source.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(visual.Target.GetComponentInChildren<Collider>(), Is.Null);
                Assert.That(visual.Target.GetComponentInChildren<Rigidbody>(), Is.Null);
                Assert.That(source.GetComponent<Renderer>().forceRenderingOff, Is.True);
                visual.SetPlaying(false);
                Assert.That(source.GetComponent<Renderer>().forceRenderingOff, Is.False);
            }
            finally { visual.Dispose(); Object.DestroyImmediate(source); }
        }

        [Test]
        public void DeletedOriginal_RemainsReplayable_ThenDisappearsAtRecordedBoundary()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var visual = new ReplayVisual(source.transform, null);
            Object.DestroyImmediate(source);
            try
            {
                var player = new HighlightReplayPlayer(new Transform[0],
                    new[] { new SceneWorldObjectReference("item", visual.Target) });
                var frames = new[]
                {
                    new HighlightReplayFrame(0, new Pose[0], new[] { new WorldObjectState("item", new Pose(Vector3.one, Quaternion.identity)) }),
                    new HighlightReplayFrame(1, new Pose[0], new WorldObjectState[0])
                };
                player.Start(new[] { new HighlightReplayClip(new HighlightSegment(0, 1), frames) });
                Assert.That(visual.Target.gameObject.activeSelf, Is.True);
                player.Advance(1);
                Assert.That(visual.Target.gameObject.activeSelf, Is.False);
                player.Start(new[] { new HighlightReplayClip(new HighlightSegment(0, 1), frames) });
                Assert.That(visual.Target.gameObject.activeSelf, Is.True);
            }
            finally { visual.Dispose(); }
        }
    }
}
