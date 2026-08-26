using System;
using System.Collections.Generic;
using Game.Core.Match;
using Game.Core.Players;
using Game.Network.Match;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class NetworkContractTests
    {
        [Test]
        public void Participant_UsesOnlyStablePlayerIndex()
        {
            var participant = new MatchParticipant("player", 3);

            Assert.That(participant.PlayerIndex, Is.EqualTo(3));
            Assert.That(typeof(MatchParticipant).GetProperty("Seat"), Is.Null);
        }

        [Test]
        public void Participant_ConvertsRoomSeatOrderToContiguousPlayerIndices()
        {
            var participants = MatchParticipant.FromRoomParticipants(new[]
            {
                new Game.Core.Rooms.RoomParticipant("late-seat", 5, false),
                new Game.Core.Rooms.RoomParticipant("host", 0, true),
                new Game.Core.Rooms.RoomParticipant("middle-seat", 3, false),
            });

            Assert.That(
                Array.ConvertAll(participants, participant => participant.PlayerId),
                Is.EqualTo(new[] { "host", "middle-seat", "late-seat" }));
            Assert.That(
                Array.ConvertAll(participants, participant => participant.PlayerIndex),
                Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void InputIntent_NormalizesMovementYawAndButtons()
        {
            var input = new PlayerInputIntent(
                3f,
                4f,
                -90f,
                PlayerInputButtons.Jump | PlayerInputButtons.Sprint);

            Assert.That(input.MoveX, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(input.MoveY, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(input.LookYawDegrees, Is.EqualTo(270f));
            Assert.That(input.IsPressed(PlayerInputButtons.Jump), Is.True);
            Assert.That(input.IsPressed(PlayerInputButtons.Prone), Is.False);
        }

        [Test]
        public void InputIntent_RejectsInvalidNetworkValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayerInputIntent(float.NaN, 0f, 0f, PlayerInputButtons.None));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayerInputIntent(0f, 0f, 0f, (PlayerInputButtons)128));
        }

        [Test]
        public void MatchStateSnapshot_RejectsInvalidReplicatedState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchStateSnapshot((MatchPhase)99, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchStateSnapshot(MatchPhase.Hiding, double.NaN));
        }

        [Test]
        public void MatchStarter_ForwardsReplicatedPhaseSnapshot()
        {
            var gameObject = new GameObject("MatchStarterTest");

            try
            {
                var starter = gameObject.AddComponent<MatchStarter>();
                var expected = new MatchStateSnapshot(MatchPhase.Searching, 120d);
                var received = new MatchStateSnapshot();
                var wasReceived = false;

                starter.MatchStateReceived += snapshot =>
                {
                    received = snapshot;
                    wasReceived = true;
                };

                starter.PublishSnapshot(expected);

                Assert.That(wasReceived, Is.True);
                Assert.That(received.Phase, Is.EqualTo(MatchPhase.Searching));
                Assert.That(received.PhaseEndsAt, Is.EqualTo(120d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ItemAssignmentRpc_TargetsOnePlayerAndForwardsOnlyItemId()
        {
            var rpc = typeof(MatchSessionState).GetMethod(
                "RPC_AssignItem",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            var gameObject = new GameObject("MatchStarterTest");

            try
            {
                Assert.That(rpc, Is.Not.Null);
                Assert.That(
                    rpc.GetParameters()[0].IsDefined(
                        typeof(Fusion.RpcTargetAttribute), false),
                    Is.True);

                var starter = gameObject.AddComponent<MatchStarter>();
                string received = null;
                starter.ItemAssignmentReceived += itemId => received = itemId;

                starter.PublishItemAssignment("Soda_01");

                Assert.That(received, Is.EqualTo("Soda_01"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ActionRequestRpcs_DeriveRequesterFromRpcInfo()
        {
            var names = new[]
            {
                "RPC_RequestHold",
                "RPC_RequestRelease",
                "RPC_RequestThrow",
                "RPC_RequestHit",
                "RPC_RequestShredder",
            };

            foreach (var name in names)
            {
                var rpc = typeof(MatchSessionState).GetMethod(name);

                Assert.That(rpc, Is.Not.Null, name);
                var attribute = (Fusion.RpcAttribute)Attribute.GetCustomAttribute(
                    rpc,
                    typeof(Fusion.RpcAttribute));
                Assert.That(attribute, Is.Not.Null, name);
                Assert.That(attribute.Sources, Is.EqualTo(Fusion.RpcSources.All), name);
                Assert.That(
                    attribute.Targets,
                    Is.EqualTo(Fusion.RpcTargets.StateAuthority),
                    name);
                Assert.That(
                    Array.Exists(
                        rpc.GetParameters(),
                        parameter => parameter.ParameterType == typeof(Fusion.RpcInfo)),
                    Is.True,
                    name);
                Assert.That(
                    Array.Exists(
                        rpc.GetParameters(),
                        parameter => parameter.Name == "playerIndex"),
                    Is.False,
                    name);
            }
        }

        [Test]
        public void MatchEventRpcs_BroadcastOnlyAuthorityConfirmedData()
        {
            var names = new[]
            {
                "RPC_NotifyItemDestroyed",
                "RPC_NotifyPlayerStunned",
                "RPC_NotifyObjectThrown",
                "RPC_NotifyFinalWarning",
            };

            foreach (var name in names)
            {
                var rpc = typeof(MatchSessionState).GetMethod(name);

                Assert.That(rpc, Is.Not.Null, name);
                var attribute = (Fusion.RpcAttribute)Attribute.GetCustomAttribute(
                    rpc,
                    typeof(Fusion.RpcAttribute));
                Assert.That(attribute, Is.Not.Null, name);
                Assert.That(
                    attribute.Sources,
                    Is.EqualTo(Fusion.RpcSources.StateAuthority),
                    name);
                Assert.That(
                    attribute.Targets,
                    Is.EqualTo(Fusion.RpcTargets.All),
                    name);
            }

            var destroyedRpc = typeof(MatchSessionState).GetMethod(
                "RPC_NotifyItemDestroyed");
            Assert.That(
                Array.Exists(
                    destroyedRpc.GetParameters(),
                    parameter => parameter.Name.IndexOf(
                        "owner",
                        StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "Destroyed item notifications must not reveal its owner.");
        }

        [Test]
        public void MatchStarter_ForwardsReplicatedActivityAndResult()
        {
            var gameObject = new GameObject("MatchStarterTest");

            try
            {
                var starter = gameObject.AddComponent<MatchStarter>();
                IReadOnlyList<bool> receivedActivity = null;
                MatchResult? receivedResult = null;
                starter.ParticipantActivityReceived += value =>
                    receivedActivity = value;
                starter.MatchResultReceived += value => receivedResult = value;

                starter.PublishParticipantActivity(new[] { true, false });
                starter.PublishMatchResult(new MatchResult(
                    MatchEndReason.LastPlayerStanding,
                    300d,
                    new[] { 0 }));

                Assert.That(receivedActivity, Is.EqualTo(new[] { true, false }));
                Assert.That(receivedResult.HasValue, Is.True);
                Assert.That(
                    receivedResult.Value.EndReason,
                    Is.EqualTo(MatchEndReason.LastPlayerStanding));
                Assert.That(receivedResult.Value.EndedAt, Is.EqualTo(300d));
                Assert.That(receivedResult.Value.WinnerPlayerIndices, Is.EqualTo(new[] { 0 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ObjectStateSnapshot_PreservesPhysicsAndVisibilityState()
        {
            var pose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f));
            var velocity = new Vector3(4f, 5f, 6f);
            var snapshot = new MatchObjectStateSnapshot(
                "Soda_01",
                2,
                pose,
                velocity,
                true,
                7);

            Assert.That(snapshot.ObjectId, Is.EqualTo("Soda_01"));
            Assert.That(snapshot.HolderPlayerIndex, Is.EqualTo(2));
            Assert.That(snapshot.Pose, Is.EqualTo(pose));
            Assert.That(snapshot.InitialVelocity, Is.EqualTo(velocity));
            Assert.That(snapshot.IsDestroyed, Is.True);
            Assert.That(snapshot.Version, Is.EqualTo(7));
        }
    }
}
