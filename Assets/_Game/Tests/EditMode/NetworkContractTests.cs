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
    }
}
