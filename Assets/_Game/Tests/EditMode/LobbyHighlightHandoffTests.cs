using System.Reflection;
using Game.Bootstrap;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class LobbyHighlightHandoffTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CaptureStaging_SkipsDestroyedRoots_AndStillTransitionsSurvivingObjects(bool destroyedLobbyRoot)
        {
            var lobbyRoot = new GameObject("Inactive lobby scope");
            var playgroundRoot = new GameObject("Inactive playground scope");
            lobbyRoot.SetActive(false);
            playgroundRoot.SetActive(false);
            var lobbyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mapObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var destroyedItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var sharedRig = new GameObject("Transferred camera rig");
            sharedRig.SetActive(false);
            sharedRig.AddComponent<Game.Client.Cameras.PlayerCameraController>();
            var sharedCamera = sharedRig.AddComponent<Camera>();
            var transferredOutput = new GameObject("Transferred output camera");
            var outputCamera = transferredOutput.AddComponent<Camera>();
            try
            {
                var lobby = lobbyRoot.AddComponent<LobbyLifetimeScope>();
                var playground = playgroundRoot.AddComponent<PlaygroundLifetimeScope>();
                Set(lobby, "sceneRoots", destroyedLobbyRoot ? new[] { destroyedItem, lobbyObject } : new[] { lobbyObject });
                typeof(PlaygroundLifetimeScope).GetField("sceneRoots", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(playground, new[] { destroyedItem, mapObject, sharedRig, transferredOutput });
                Object.DestroyImmediate(destroyedItem);

                Assert.DoesNotThrow(() => typeof(LobbyLifetimeScope)
                    .GetMethod("CaptureStagingPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(lobby, null));
                Show(lobby, false);
                Assert.That(lobbyObject.GetComponent<Renderer>().forceRenderingOff, Is.True);
                Assert.That(mapObject.GetComponent<Renderer>().forceRenderingOff, Is.False);
                Show(lobby, true);
                Assert.That(lobbyObject.GetComponent<Renderer>().forceRenderingOff, Is.False);
                Assert.That(mapObject.GetComponent<Renderer>().forceRenderingOff, Is.True);
                Assert.That(mapObject.GetComponent<Collider>().enabled, Is.False);
                Assert.That(outputCamera.enabled, Is.True,
                    "The output camera already in Lobby must remain enabled with its rig.");
                Assert.That(sharedCamera.enabled, Is.True,
                    "The transferred lobby rig must not be disabled with the outgoing map.");
            }
            finally
            {
                Object.DestroyImmediate(transferredOutput);
                Object.DestroyImmediate(sharedRig);
                Object.DestroyImmediate(destroyedItem);
                Object.DestroyImmediate(mapObject);
                Object.DestroyImmediate(lobbyObject);
                Object.DestroyImmediate(playgroundRoot);
                Object.DestroyImmediate(lobbyRoot);
            }
        }

        [Test]
        public void ShowingLobby_HidesOutgoingGeometryAndCollisions_AndPreservesOriginalStates()
        {
            var root = new GameObject("Inactive lobby test");
            root.SetActive(false);
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var scope = root.AddComponent<LobbyLifetimeScope>();
                var renderer = building.GetComponent<Renderer>();
                var collider = building.GetComponent<Collider>();
                var hiddenRenderer = hidden.GetComponent<Renderer>();
                var disabledCollider = hidden.GetComponent<Collider>();
                hiddenRenderer.forceRenderingOff = true;
                disabledCollider.enabled = false;
                Set(scope, "outgoingRenderers", new[] { renderer, hiddenRenderer });
                Set(scope, "outgoingRendererStates", new[] { false, true });
                Set(scope, "outgoingRendererEnabledStates", new[] { true, true });
                Set(scope, "outgoingColliders", new[] { collider, disabledCollider });
                Set(scope, "outgoingColliderStates", new[] { true, false });

                Show(scope, true);
                Assert.IsTrue(renderer.forceRenderingOff);
                Assert.IsFalse(renderer.enabled);
                Assert.IsFalse(collider.enabled);
                // A later replay cleanup restores an occluding wall, while others
                // are still watching. The skipped peer must keep its lobby clear.
                renderer.forceRenderingOff = false;
                collider.enabled = true;
                typeof(LobbyLifetimeScope).GetMethod("HideOutgoingGeometry", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scope, null);
                Assert.IsTrue(renderer.forceRenderingOff);
                Assert.IsFalse(collider.enabled);
                Show(scope, false);
                Assert.IsFalse(renderer.forceRenderingOff);
                Assert.IsTrue(collider.enabled);
                Assert.IsTrue(hiddenRenderer.forceRenderingOff);
                Assert.IsFalse(disabledCollider.enabled);

                Object.DestroyImmediate(building);
                Assert.DoesNotThrow(() => Show(scope, true), "Outgoing scene may unload before phase reset.");
            }
            finally
            {
                Object.DestroyImmediate(building);
                Object.DestroyImmediate(hidden);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetiredMapCleanup_IsBoundedAndPreservesProtectedSubtreesAndLobby()
        {
            var root = new GameObject("Retired map");
            var lobby = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                for (var i = 0; i < 150; i++)
                {
                    var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prop.transform.SetParent(root.transform);
                }
                var protectedRoot = new GameObject("Protected camera", typeof(Camera));
                protectedRoot.transform.SetParent(root.transform);
                var protectedChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                protectedChild.transform.SetParent(protectedRoot.transform);
                var cleanup = new RetiredMapCleanup(new[] { root }, root.scene);
                var destroyed = 0;
                void Destroy(GameObject target) { destroyed++; Object.DestroyImmediate(target); }
                cleanup.Tick(Destroy);
                Assert.That(destroyed, Is.LessThanOrEqualTo(64));
                for (var i = 0; i < 1000 && !cleanup.IsComplete; i++) cleanup.Tick(Destroy);
                Assert.That(cleanup.IsComplete, Is.True);
                Assert.That(destroyed, Is.EqualTo(150));
                Assert.That(protectedRoot != null && protectedChild != null, Is.True);
                Assert.That(lobby != null && lobby.GetComponent<Renderer>().enabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(lobby); }
        }

        private static void Set(LobbyLifetimeScope scope, string name, object value) =>
            typeof(LobbyLifetimeScope).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(scope, value);

        private static void Show(LobbyLifetimeScope scope, bool visible) =>
            typeof(LobbyLifetimeScope).GetMethod("SetStagingVisible", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(scope, new object[] { visible });
    }
}
