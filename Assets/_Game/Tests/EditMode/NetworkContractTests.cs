using System;
using Game.Core.Match;
using Game.Core.Players;
using Game.Network.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class NetworkContractTests
    {
        [Test]
        public void Participant_UsesSeatAsStablePlayerIndex()
        {
            var participant = new MatchParticipant("player", 3);

            Assert.That(participant.PlayerIndex, Is.EqualTo(3));
            Assert.That(participant.Seat, Is.EqualTo(participant.PlayerIndex));
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
