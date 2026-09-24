using Game.Network.Match;
using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class NpcCarryAuthorityTests
    {
        private const string Npc = "npc:1";
        private const string OtherNpc = "npc:2";
        private const string Box = "box-1";
        private const string Vase = "vase-1";

        private static readonly Pose BotAtOrigin = new(Vector3.zero, Quaternion.identity);

        private static NpcCarryAuthority Create() => new(new[]
        {
            new WorldObjectState(Box, new Pose(new Vector3(0f, 0f, 1.5f), Quaternion.identity)),
            new WorldObjectState(Vase, new Pose(new Vector3(0f, 0f, 6f), Quaternion.identity)),
        });

        [Test]
        public void TryHold_WithinTwoMetres_Succeeds()
        {
            var authority = Create();

            Assert.That(authority.TryHold(Npc, Box, BotAtOrigin, out var reason), Is.True, reason);
            Assert.That(authority.TryGetHeld(Npc, out var held), Is.True);
            Assert.That(held, Is.EqualTo(Box));
            Assert.That(authority.HeldCount, Is.EqualTo(1));
        }

        [Test]
        public void TryHold_OutsideInteractionDistance_IsRejected()
        {
            var authority = Create();

            Assert.That(authority.TryHold(Npc, Vase, BotAtOrigin, out var reason), Is.False);
            Assert.That(reason, Does.Contain("interaction distance"));
            Assert.That(authority.HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void TryHold_HumanOrSeatBotId_IsRejected()
        {
            var authority = Create();

            Assert.That(authority.TryHold("player-7", Box, BotAtOrigin, out var reason), Is.False);
            Assert.That(reason, Does.Contain("NPC id"));
            Assert.That(authority.TryHold("bot:1", Box, BotAtOrigin, out _), Is.False);
        }

        [Test]
        public void TryHold_SecondObjectWhileHandOccupied_IsRejected()
        {
            var authority = Create();
            authority.TryHold(Npc, Box, BotAtOrigin, out _);

            Assert.That(authority.TryHold(Npc, Box, BotAtOrigin, out var reason), Is.False);
            Assert.That(reason, Does.Contain("occupied"));
        }

        [Test]
        public void TryHold_ObjectHeldByAnotherNpc_IsRejected()
        {
            var authority = Create();
            authority.TryHold(OtherNpc, Box, BotAtOrigin, out _);

            Assert.That(authority.TryHold(Npc, Box, BotAtOrigin, out var reason), Is.False);
            Assert.That(reason, Does.Contain("already held"));
        }

        [Test]
        public void TryRelease_WithinReleaseDistance_FreesHandAndMovesObject()
        {
            var authority = Create();
            authority.TryHold(Npc, Box, BotAtOrigin, out _);
            var dropAt = new Pose(new Vector3(0f, 0f, 2.5f), Quaternion.identity);

            Assert.That(authority.TryRelease(Npc, BotAtOrigin, dropAt, out var reason), Is.True, reason);
            Assert.That(authority.HeldCount, Is.EqualTo(0));
            Assert.That(authority.TryGetPose(Box, out var pose), Is.True);
            Assert.That(pose.position, Is.EqualTo(dropAt.position));
            // 놓은 뒤에는 같은 물건을 다시 집을 수 있어야 한다(점유 해제).
            Assert.That(authority.TryHold(Npc, Box, new Pose(dropAt.position, Quaternion.identity), out _), Is.True);
        }

        [Test]
        public void TryRelease_TooFarOrEmptyHand_IsRejected()
        {
            var authority = Create();

            Assert.That(authority.TryRelease(Npc, BotAtOrigin, BotAtOrigin, out var emptyReason), Is.False);
            Assert.That(emptyReason, Does.Contain("empty"));

            authority.TryHold(Npc, Box, BotAtOrigin, out _);
            var farAway = new Pose(new Vector3(0f, 0f, 10f), Quaternion.identity);
            Assert.That(authority.TryRelease(Npc, BotAtOrigin, farAway, out var farReason), Is.False);
            Assert.That(farReason, Does.Contain("release distance"));
            Assert.That(authority.HeldCount, Is.EqualTo(1), "a rejected release must not drop the object");
        }

        [Test]
        public void Reset_ClearsHandsAndRestoresInitialPoses()
        {
            var authority = Create();
            authority.TryHold(Npc, Box, BotAtOrigin, out _);
            authority.TryRelease(Npc, BotAtOrigin, new Pose(new Vector3(0f, 0f, 2.5f), Quaternion.identity), out _);
            authority.TryHold(Npc, Box, new Pose(new Vector3(0f, 0f, 2.5f), Quaternion.identity), out _);

            authority.Reset();

            Assert.That(authority.HeldCount, Is.EqualTo(0));
            Assert.That(authority.TryGetPose(Box, out var pose), Is.True);
            Assert.That(pose.position, Is.EqualTo(new Vector3(0f, 0f, 1.5f)));
        }
    }
}
