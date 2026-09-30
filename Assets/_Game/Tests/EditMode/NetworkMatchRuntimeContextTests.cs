using System;
using System.Collections.Generic;
using Game.Core.Emotes;
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
        public void ReplayState_PostureValidationMatchesEveryByteValue()
        {
            for (var value = 0; value <= byte.MaxValue; value++)
            {
                var posture = (PlayerPosture)value;
                if (Enum.IsDefined(typeof(PlayerPosture), posture))
                    Assert.That(new NetworkPlayerReplayState(posture, true, 0).Posture, Is.EqualTo(posture));
                else
                    Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkPlayerReplayState(posture, true, 0));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkPlayerReplayState(PlayerPosture.Standing, true, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkPlayerReplayState(PlayerPosture.Standing, true, 0, -1));
        }

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

        /// <summary>
        /// 감정 표현은 번호가 바뀐 순간 시작해 카탈로그 길이만큼 이어진다. 그래야 하이라이트가
        /// 실제 플레이에서 보던 표현을 그대로 재생한다 (2026-09-22).
        /// </summary>
        [Test]
        public void Context_CapturesEmoteWhileItPlays()
        {
            var source = new FakeNetworkSource(10d, new Dictionary<string, Pose>
            {
                ["player-0"] = Pose.identity,
                ["player-1"] = Pose.identity,
            });
            source.SetReplayState("player-0",
                new NetworkPlayerReplayState(PlayerPosture.Standing, true, 0));
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
            _ = context.PlayerReplayActions;

            source.ServerTime = 10.1d;
            source.SetReplayState("player-0",
                new NetworkPlayerReplayState(
                    PlayerPosture.Standing, true, 0, 1, (int)EmoteId.Wave));

            Assert.That(context.PlayerReplayActions[0].TryGetEmote(out var emoteId), Is.True);
            Assert.That(emoteId, Is.EqualTo((int)EmoteId.Wave));
            Assert.That(context.PlayerReplayActions[1].TryGetEmote(out _), Is.False);

            // 인사는 2초가 조금 넘는 1회성 표현이라 그 뒤에는 남지 않는다.
            source.ServerTime = 13d;
            Assert.That(context.PlayerReplayActions[0].TryGetEmote(out _), Is.False);

            // 주먹질은 재생 중인 표현을 끊는다.
            source.ServerTime = 14d;
            source.SetReplayState("player-0",
                new NetworkPlayerReplayState(
                    PlayerPosture.Standing, true, 0, 2, (int)EmoteId.HipHop));
            Assert.That(context.PlayerReplayActions[0].TryGetEmote(out _), Is.True);

            source.ServerTime = 14.1d;
            source.SetReplayState("player-0",
                new NetworkPlayerReplayState(
                    PlayerPosture.Standing, true, 1, 2, (int)EmoteId.HipHop));
            Assert.That(context.PlayerReplayActions[0].TryGetEmote(out _), Is.False);
        }

        [Test]
        public void Context_CapturesOncePerServerTickAndRefreshesAfterTimeCorrection()
        {
            var poses = new Dictionary<string, Pose>
            {
                ["player-0"] = Pose.identity,
                ["player-1"] = Pose.identity,
            };
            var source = new FakeNetworkSource(10d, poses);
            var context = new NetworkMatchRuntimeContext(source, new FakeSceneContext(),
                new[] { new MatchParticipant("player-0", 0), new MatchParticipant("player-1", 1) });

            _ = context.PlayerReplayActions;
            _ = context.PlayerPoses;
            _ = context.PlayerPositions;
            Assert.That(source.PoseReads, Is.EqualTo(2));
            Assert.That(source.ReplayReads, Is.EqualTo(2));

            var moved = new Pose(Vector3.right, Quaternion.identity);
            poses["player-0"] = moved;
            Assert.That(context.PlayerPoses[0], Is.EqualTo(Pose.identity),
                "All consumers in one server tick must see the same snapshot.");
            source.ServerTime = 11d;
            Assert.That(context.PlayerPoses[0], Is.EqualTo(moved));
            Assert.That(source.PoseReads, Is.EqualTo(4));
            poses["player-0"] = Pose.identity;
            source.ServerTime = 9d;
            Assert.That(context.PlayerPositions[0], Is.EqualTo(Vector3.zero));
            Assert.That(source.PoseReads, Is.EqualTo(6));
        }

        [Test]
        public void Context_RetriesFailedInitialCaptureAtSameTime()
        {
            var poses = new Dictionary<string, Pose> { ["player-0"] = Pose.identity };
            var source = new FakeNetworkSource(10d, poses);
            var context = new NetworkMatchRuntimeContext(source, new FakeSceneContext(),
                new[] { new MatchParticipant("player-0", 0), new MatchParticipant("player-1", 1) });
            Assert.Throws<InvalidOperationException>(() => _ = context.PlayerPoses);
            poses["player-1"] = new Pose(Vector3.right, Quaternion.identity);
            Assert.That(context.PlayerPositions[1], Is.EqualTo(Vector3.right));
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

            public int PoseReads { get; private set; }
            public int ReplayReads { get; private set; }

            public bool TryGetPlayerPose(string playerId, out Pose pose)
            {
                PoseReads++;
                return poses.TryGetValue(playerId, out pose);
            }

            public bool TryGetLocalStamina(out float current, out float max, out bool exhausted)
            {
                current = 0f;
                max = 0f;
                exhausted = false;
                return false;
            }

            public void Remove(string playerId)
            {
                poses.Remove(playerId);
            }

            public void SetReplayState(string playerId, NetworkPlayerReplayState state) =>
                replayStates[playerId] = state;

            public bool TryGetPlayerReplayState(
                string playerId,
                out NetworkPlayerReplayState state)
            {
                ReplayReads++;
                return replayStates.TryGetValue(playerId, out state);
            }
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
