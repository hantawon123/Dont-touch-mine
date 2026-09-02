using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Players;
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

        [Test]
        public void Context_KeepsLastPoseAfterSpawnedPlayerLeaves()
        {
            var lastPose = new Pose(new Vector3(3f, 0f, 7f), Quaternion.identity);
            var source = new FakeNetworkSource(10d, new Dictionary<string, Pose>
            {
                ["player-0"] = Pose.identity,
                ["player-1"] = lastPose,
            });
            var context = new NetworkMatchRuntimeContext(
                source,
                new FakeSceneContext(),
                new[]
                {
                    new MatchParticipant("player-0", 0),
                    new MatchParticipant("player-1", 1),
                });

            Assert.That(context.PlayerPoses[1], Is.EqualTo(lastPose));

            source.Remove("player-1");

            Assert.That(context.PlayerPoses[1], Is.EqualTo(lastPose));
            Assert.That(context.PlayerPositions[1], Is.EqualTo(lastPose.position));
        }

        [Test]
        public void Context_CapturesPostureAirborneAndAttackPulse()
        {
            var source = new FakeNetworkSource(10d, new Dictionary<string, Pose>
            {
                ["player-0"] = Pose.identity,
                ["player-1"] = Pose.identity,
            });
            source.SetReplayState("player-0",
                new NetworkPlayerReplayState(PlayerPosture.Crouching, false, 0));
            source.SetReplayState("player-1",
                new NetworkPlayerReplayState(PlayerPosture.Standing, true, 0));
            var context = new NetworkMatchRuntimeContext(
                source,
                new FakeSceneContext(),
                new[]
                {
                    new MatchParticipant("player-0", 0),
                    new MatchParticipant("player-1", 1),
                });

            Assert.That(context.PlayerReplayActions[0], Is.EqualTo(
                HighlightPlayerAction.Crouching | HighlightPlayerAction.Airborne));

            source.ServerTime = 10.1d;
            source.SetReplayState("player-1",
                new NetworkPlayerReplayState(PlayerPosture.Standing, true, 1));

            Assert.That(context.PlayerReplayActions[1] & HighlightPlayerAction.Punching,
                Is.Not.EqualTo(HighlightPlayerAction.None));
        }

        private sealed class FakeNetworkSource :
            INetworkMatchRuntimeSource,
            INetworkPlayerReplayStateSource
        {
            public bool IsRuntimeReady => true;
            public MatchRuleSettings MatchRules => MatchRuleSettings.Default;
            private readonly IDictionary<string, Pose> poses;
            private readonly Dictionary<string, NetworkPlayerReplayState> replayStates = new();

            public FakeNetworkSource(
                double serverTime,
                IDictionary<string, Pose> poses)
            {
                ServerTime = serverTime;
                this.poses = poses;
            }

            public double ServerTime { get; set; }

            public bool TryGetPlayerPose(string playerId, out Pose pose) =>
                poses.TryGetValue(playerId, out pose);

            public void Remove(string playerId)
            {
                poses.Remove(playerId);
            }

            public void SetReplayState(string playerId, NetworkPlayerReplayState state) =>
                replayStates[playerId] = state;

            public bool TryGetPlayerReplayState(
                string playerId,
                out NetworkPlayerReplayState state) =>
                replayStates.TryGetValue(playerId, out state);
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
