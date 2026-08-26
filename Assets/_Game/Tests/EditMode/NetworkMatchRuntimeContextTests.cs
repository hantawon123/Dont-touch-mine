using System;
using System.Collections.Generic;
using Game.Core.Match;
using Game.Network.Match;
using Game.Server.Items;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class NetworkMatchRuntimeContextTests
    {
        [Test]
        public void Context_UsesNetworkTimeAndOrdersPosesByPlayerIndex()
        {
            var firstPose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 10f, 0f));
            var secondPose = new Pose(new Vector3(4f, 5f, 6f), Quaternion.Euler(0f, 20f, 0f));
            var source = new FakeNetworkSource(42.5d, new Dictionary<string, Pose>
            {
                ["player-0"] = firstPose,
                ["player-1"] = secondPose,
            });
            var scene = new FakeSceneContext();
            var context = new NetworkMatchRuntimeContext(
                source,
                scene,
                new[]
                {
                    new MatchParticipant("player-1", 1),
                    new MatchParticipant("player-0", 0),
                });

            Assert.That(context.ServerTime, Is.EqualTo(42.5d));
            Assert.That(context.PlayerPositions[0], Is.EqualTo(firstPose.position));
            Assert.That(context.PlayerPositions[1], Is.EqualTo(secondPose.position));
            Assert.That(context.PlayerPoses[0].rotation, Is.EqualTo(firstPose.rotation));
            Assert.That(context.ReplayObjects, Is.SameAs(scene.ReplayObjects));
        }

        [Test]
        public void Context_RejectsMissingOrDuplicatePlayers()
        {
            var source = new FakeNetworkSource(1d, new Dictionary<string, Pose>());
            var scene = new FakeSceneContext();

            Assert.Throws<ArgumentException>(() => new NetworkMatchRuntimeContext(
                source,
                scene,
                new[]
                {
                    new MatchParticipant("player-0", 0),
                    new MatchParticipant("player-1", 0),
                }));

            var context = new NetworkMatchRuntimeContext(
                source,
                scene,
                new[]
                {
                    new MatchParticipant("player-0", 0),
                    new MatchParticipant("player-1", 1),
                });

            Assert.Throws<InvalidOperationException>(() => _ = context.PlayerPositions);
        }

        private sealed class FakeNetworkSource : INetworkMatchRuntimeSource
        {
            private readonly IReadOnlyDictionary<string, Pose> poses;

            public FakeNetworkSource(
                double serverTime,
                IReadOnlyDictionary<string, Pose> poses)
            {
                ServerTime = serverTime;
                this.poses = poses;
            }

            public double ServerTime { get; }

            public bool TryGetPlayerPose(string playerId, out Pose pose) =>
                poses.TryGetValue(playerId, out pose);
        }

        private sealed class FakeSceneContext : IMatchRuntimeContext
        {
            public double ServerTime => 0d;
            public IReadOnlyList<Vector3> PlayerPositions => Array.Empty<Vector3>();
            public IReadOnlyList<Pose> PlayerPoses => Array.Empty<Pose>();
            public IReadOnlyList<WorldObjectState> ReplayObjects { get; } =
                Array.Empty<WorldObjectState>();
        }
    }
}
