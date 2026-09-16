using System.Collections.Generic;
using System.Reflection;
using Game.Bootstrap;
using Game.Client.Interactions;
using Game.Core.Lobby;
using Game.Network.Match;
using Game.Network.Session;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class HeldItemRecoveryTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(-1)]
        [TestCase(1)]
        public void UnchangedAuthorityVersion_RepairsWrongLocalHolder(int authoritativeHolder)
        {
            using var room = new RoomBrowserSystem();
            var network = new NetworkRunnerService(null, null, null, null, null, null);
            var itemObject = new GameObject("RecoveryItem", typeof(Rigidbody), typeof(BoxCollider));
            var player0 = new GameObject("WrongHolder");
            var player1 = new GameObject("CorrectHolder");
            try
            {
                var item = itemObject.AddComponent<CarryableItem>();
                typeof(CarryableItem).GetMethod("Awake", Private).Invoke(item, null);
                var wrong = player0.AddComponent<PlayerInteractor>();
                var correct = player1.AddComponent<PlayerInteractor>();
                typeof(PlayerInteractor).GetField("holdPoint", Private).SetValue(wrong, player0.transform);
                typeof(PlayerInteractor).GetField("holdPoint", Private).SetValue(correct, player1.transform);
                wrong.ApplyConfirmedPickup(item);

                var bridge = new NetworkInteractionSceneBridge(network, room, false, itemObject.scene);
                Field<Dictionary<string, CarryableItem>>(bridge, "items").Add(item.ObjectId, item);
                Field<Dictionary<int, PlayerInteractor>>(bridge, "interactors").Add(0, wrong);
                Field<Dictionary<int, PlayerInteractor>>(bridge, "interactors").Add(1, correct);
                Field<Dictionary<string, int>>(bridge, "appliedVersions").Add(item.ObjectId, 5);
                var pose = new Pose(new Vector3(2f, 1f, 3f), Quaternion.identity);
                typeof(NetworkInteractionSceneBridge).GetField("objectStates", Private).SetValue(bridge,
                    new[] { new MatchObjectStateSnapshot(item.ObjectId, authoritativeHolder, pose, default, false, 5) });
                typeof(NetworkInteractionSceneBridge).GetMethod("ApplyObjectStates", Private).Invoke(bridge, null);

                Assert.That(wrong.CarriedItem, Is.Null);
                Assert.That(item.IsCarried, Is.EqualTo(authoritativeHolder == 1));
                if (authoritativeHolder == 1) Assert.That(correct.CarriedItem, Is.SameAs(item));
                // OnNetworkPose updates the physics pose; interpolated Transform sync needs a player loop.
                else Assert.That(item.GetComponent<Rigidbody>().position, Is.EqualTo(pose.position));
            }
            finally
            {
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(player0);
                Object.DestroyImmediate(player1);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DestroyedSceneItem_HudRefreshClearsStaleReferences(bool hudVisible)
        {
            var itemObject = new GameObject("OutgoingSceneItem", typeof(Rigidbody));
            var playerObject = new GameObject("PersistentPlayer");
            try
            {
                var interactor = playerObject.AddComponent<PlayerInteractor>();
                var item = itemObject.AddComponent<CarryableItem>();
                typeof(PlayerInteractor).GetField("aimedTarget", Private).SetValue(interactor, item);
                typeof(PlayerInteractor).GetField("highlightedItem", Private).SetValue(interactor, item);
                typeof(PlayerInteractor).GetProperty("CarriedItem").SetValue(interactor, item);
                Object.DestroyImmediate(itemObject);

                Assert.DoesNotThrow(() => interactor.SetHudVisible(hudVisible));
                Assert.That(Field<Component>(interactor, "aimedTarget"), Is.Null);
                Assert.That(Field<CarryableItem>(interactor, "highlightedItem"), Is.Null);
                Assert.That(interactor.CarriedItem, Is.Null);
                Assert.That(interactor.IsCarrying, Is.False);
            }
            finally
            {
                if (itemObject != null) Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void DestroyedSceneItem_DisableClearsAimWithoutAccessingHighlight()
        {
            var itemObject = new GameObject("OutgoingSceneItem", typeof(Rigidbody));
            var playerObject = new GameObject("PersistentPlayer");
            try
            {
                var interactor = playerObject.AddComponent<PlayerInteractor>();
                var item = itemObject.AddComponent<CarryableItem>();
                typeof(PlayerInteractor).GetField("aimedTarget", Private).SetValue(interactor, item);
                typeof(PlayerInteractor).GetField("highlightedItem", Private).SetValue(interactor, item);
                Object.DestroyImmediate(itemObject);

                Assert.DoesNotThrow(() => typeof(PlayerInteractor)
                    .GetMethod("OnDisable", Private).Invoke(interactor, null));
                Assert.That(Field<Component>(interactor, "aimedTarget"), Is.Null);
                Assert.That(Field<CarryableItem>(interactor, "highlightedItem"), Is.Null);
            }
            finally
            {
                if (itemObject != null) Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void HighlightHandoff_FreezesLivePropsAndIgnoresLateStateWithoutHidingVisuals()
        {
            using var room = new RoomBrowserSystem();
            var network = new NetworkRunnerService(null, null, null, null, null, null);
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var lobbyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var body = itemObject.AddComponent<Rigidbody>();
                var item = itemObject.AddComponent<CarryableItem>();
                typeof(CarryableItem).GetMethod("Awake", Private).Invoke(item, null);
                var lobbyBody = lobbyObject.AddComponent<Rigidbody>();
                var bridge = new NetworkInteractionSceneBridge(network, room, false, itemObject.scene);
                Field<Dictionary<string, CarryableItem>>(bridge, "items").Add(item.ObjectId, item);
                var state = new[] { new MatchObjectStateSnapshot(item.ObjectId, -1,
                    new Pose(Vector3.one * 10, Quaternion.identity), default, false, 9) };
                typeof(NetworkInteractionSceneBridge).GetField("objectStates", Private).SetValue(bridge, state);
                bridge.SuspendForHighlights();
                bridge.SuspendForHighlights();
                typeof(NetworkInteractionSceneBridge).GetMethod("ApplyObjectStates", Private).Invoke(bridge, null);
                typeof(NetworkInteractionSceneBridge).GetMethod("OnObjectStatesReceived", Private).Invoke(bridge, new object[] { state });
                bridge.Tick();
                Assert.That(itemObject.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(body.isKinematic, Is.True);
                Assert.That(body.detectCollisions, Is.False);
                Assert.That(item.enabled, Is.False);
                Assert.That(item.GetComponent<Collider>().enabled, Is.False);
                Assert.That(item.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(item.GetComponent<Renderer>().forceRenderingOff, Is.False);
                Assert.That(lobbyBody.isKinematic, Is.False);
                Assert.That(lobbyObject.GetComponent<Collider>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(lobbyObject);
            }
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, Private).GetValue(owner);
    }
}
