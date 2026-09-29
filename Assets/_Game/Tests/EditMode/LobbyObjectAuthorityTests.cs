using Game.Network.Match;
using Game.Client.Interactions;
using Game.Server.Items;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LobbyObjectAuthorityTests
    {
        private static Pose At(float x) => new(new Vector3(x, 0f, 0f), Quaternion.identity);
        private static LobbyObjectAuthority Create() => new(new[]
        {
            new WorldObjectState("box", At(0f)),
            new WorldObjectState("can", At(1f))
        });

        [Test]
        public void LobbyEnvironment_HasUniqueCarryablesWithinReplicationCapacity()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/LobbyBasementEnvironment.prefab");
            Assert.That(prefab, Is.Not.Null);
            var items = prefab.GetComponentsInChildren<CarryableItem>(true);
            Assert.That(items.Length, Is.InRange(1, MatchSessionState.MaxReplicatedObjects));
            Assert.That(items.Select(item => item.ObjectId).Distinct().Count(), Is.EqualTo(items.Length));
        }

        [Test]
        public void Hold_RejectsUnknownDistantAndAlreadyHeldObjects()
        {
            var lobby = Create();
            Assert.That(lobby.TryHold("host", "unknown", At(0f)), Is.False);
            Assert.That(lobby.TryHold("host", "box", At(3f)), Is.False);
            Assert.That(lobby.TryHold("host", "box", At(0f)), Is.True);
            Assert.That(lobby.TryHold("host", "can", At(0f)), Is.False);
            Assert.That(lobby.TryHold("client", "box", At(0f)), Is.False);
        }

        [TestCase(false, 0f)]
        [TestCase(true, 8f)]
        public void ReleaseAndThrow_AllowAnotherPlayerToPickUp(bool throwing, float speed)
        {
            var lobby = Create();
            lobby.TryHold("host", "box", At(0f));
            Assert.That(lobby.TryRelease("host", At(0f), At(1f), Vector3.forward * speed, throwing), Is.True);
            Assert.That(lobby.TryGetHeld("host", out _), Is.False);
            Assert.That(lobby.TryHold("client", "box", At(1f)), Is.True);
        }

        [Test]
        public void InvalidRelease_PreservesOwnershipAndDisconnectFreesIt()
        {
            var lobby = Create();
            lobby.TryHold("host", "box", At(0f));
            Assert.That(lobby.TryRelease("host", At(0f), At(3.01f), Vector3.zero, false), Is.False);
            Assert.That(lobby.TryRelease("host", At(0f), At(1f), Vector3.forward * 9f, true), Is.False);
            Assert.That(lobby.TryGetHeld("host", out var held), Is.True);
            Assert.That(held, Is.EqualTo("box"));
            Assert.That(lobby.Forget("host"), Is.True);
            Assert.That(lobby.TryHold("replacement", "box", At(0f)), Is.True);
        }
    }
}
