#if UNITY_SERVER
using System.Collections;
using System.Reflection;
using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace Game.Tests.PlayMode
{
    public sealed class DeferredCarryablePhysicsTests
    {
        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void CcdContactInIdenticalIndependentWorlds(bool firstDeferred, bool secondDeferred)
        {
            var scenes = new Scene[2];
            var projectiles = new Rigidbody[2];
            var items = new CarryableItem[2];
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    scenes[i] = SceneManager.CreateScene("CCD " + System.Guid.NewGuid(), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                    var deferred = i == 0 ? firstDeferred : secondDeferred;
                    Create(scenes[i], new Vector3(0, 20, 3), new Vector3(3, 5, .1f), deferred);
                    items[i] = Create(scenes[i], new Vector3(0, 20, 0), Vector3.one, deferred);
                }
                Physics.SyncTransforms();
                for (var step = 0; step < 25; step++)
                    foreach (var scene in scenes) scene.GetPhysicsScene().Simulate(.02f);
                for (var i = 0; i < 2; i++)
                {
                    var hand = new GameObject("Hand");
                    SceneManager.MoveGameObjectToScene(hand, scenes[i]);
                    hand.transform.position = new Vector3(0, 23, 0);
                    items[i].OnPickedUp(hand.transform);
                    items[i].OnReleased(new Pose(new Vector3(0, 20, 0), Quaternion.identity),
                        Vector3.forward * Game.Network.Match.InteractionAuthorityRules.DefaultMaxThrowSpeed);
                    projectiles[i] = items[i].GetComponent<Rigidbody>();
                }
                var maxDistance = 0f;
                for (var step = 0; step < 25; step++)
                {
                    foreach (var scene in scenes) scene.GetPhysicsScene().Simulate(.02f);
                    var distance = Vector3.Distance(projectiles[0].position, projectiles[1].position);
                    maxDistance = Mathf.Max(maxDistance, distance);
                    TestContext.WriteLine($"step={step} first={projectiles[0].position:F6} second={projectiles[1].position:F6} distance={distance:F6}");
                }
                Assert.That(projectiles[0].position.z, Is.LessThan(3f));
                Assert.That(projectiles[1].position.z, Is.LessThan(3f));
                Assert.That(maxDistance, Is.LessThan(.001f), "Same coordinates and fixed steps isolate body deferral from world-position rounding.");
            }
            finally
            {
                foreach (var scene in scenes)
                {
                    if (!scene.IsValid()) continue;
                    foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                    SceneManager.UnloadSceneAsync(scene);
                }
            }

            static CarryableItem Create(Scene scene, Vector3 position, Vector3 scale, bool deferred)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.SetActive(false);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetPositionAndRotation(position, Quaternion.identity);
                go.transform.localScale = scale;
                if (!deferred) go.AddComponent<Rigidbody>().isKinematic = true;
                var item = go.AddComponent<CarryableItem>();
                if (deferred) typeof(CarryableItem).GetField("deferredBody", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, true);
                go.SetActive(true);
                return item;
            }
        }

        [UnityTest]
        public IEnumerator StaticQueriesSupportPickupThrowAndNetworkRecoveryMatchExistingBodies()
        {
            var roots = new GameObject[2];
            var items = new CarryableItem[2];
            var uppers = new GameObject[2];
            var hands = new GameObject[2];
            var walls = new GameObject[2];
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    var offset = new Vector3(i * 10, 10, 0);
                    roots[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    roots[i].SetActive(false);
                    roots[i].transform.position = offset;
                    if (i == 0) roots[i].AddComponent<Rigidbody>().isKinematic = true;
                    items[i] = roots[i].AddComponent<CarryableItem>();
                    if (i == 1) typeof(CarryableItem).GetField("deferredBody", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(items[i], true);
                    roots[i].SetActive(true);
                    Physics.SyncTransforms();
                    Assert.That(Physics.Raycast(offset + Vector3.up * 2, Vector3.down, out var hit, 3), Is.True);
                    Assert.That(hit.collider, Is.SameAs(roots[i].GetComponent<Collider>()));
                    CollectionAssert.Contains(Physics.OverlapBox(offset, Vector3.one * .4f), hit.collider);
                    uppers[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    uppers[i].transform.position = offset + Vector3.up;
                    uppers[i].AddComponent<Rigidbody>();
                    hands[i] = new GameObject("Hand");
                    hands[i].transform.position = offset + Vector3.up * 3;
                    walls[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    walls[i].SetActive(false);
                    walls[i].transform.position = new Vector3(i * 10, 20, 3);
                    walls[i].transform.localScale = new Vector3(3, 5, .1f);
                    if (i == 0) walls[i].AddComponent<Rigidbody>().isKinematic = true;
                    var wallItem = walls[i].AddComponent<CarryableItem>();
                    if (i == 1) typeof(CarryableItem).GetField("deferredBody", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wallItem, true);
                    walls[i].SetActive(true);
                }
                yield return new WaitForSeconds(.5f);
                for (var i = 0; i < 2; i++)
                {
                    var upper = uppers[i].GetComponent<Rigidbody>();
                    Assert.That(upper.position.y, Is.EqualTo(11f).Within(.03f), "Both supports must block falling props.");
                    upper.Sleep();
                    items[i].OnPickedUp(hands[i].transform);
                    Assert.That(upper.IsSleeping(), Is.False, "Removing either support must wake its neighbour.");
                    Assert.That(items[i].IsCarried, Is.True);
                    Assert.That(roots[i].transform.position, Is.EqualTo(hands[i].transform.position));
                    Assert.That(roots[i].GetComponent<Collider>().enabled, Is.False);
                    items[i].OnReleased(new Pose(new Vector3(i * 10, 20, 0), Quaternion.identity), Vector3.forward * 5);
                }
                for (var step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
                var a = roots[0].GetComponent<Rigidbody>();
                var b = roots[1].GetComponent<Rigidbody>();
                Assert.That(Vector3.Distance(a.position + Vector3.right * 10, b.position), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(a.linearVelocity, b.linearVelocity), Is.LessThan(.001f));
                for (var i = 0; i < 2; i++)
                    items[i].OnReleased(new Pose(new Vector3(i * 10, 20, 0), Quaternion.identity),
                        Vector3.forward * Game.Network.Match.InteractionAuthorityRules.DefaultMaxThrowSpeed);
                for (var step = 0; step < 25; step++) yield return new WaitForFixedUpdate();
                Assert.That(a.position.z, Is.LessThan(3f), "CCD must block the original kinematic obstacle.");
                Assert.That(b.position.z, Is.LessThan(3f), "CCD must block the deferred static obstacle too.");
                // Contact trajectory equivalence is checked at identical coordinates in the test above.
                // Even two original bodies diverge here after contact at different world positions.
                for (var i = 0; i < 2; i++)
                {
                    Assert.That(uppers[i].transform.position.y, Is.LessThan(10.95f));
                    items[i].OnSettled(new Pose(new Vector3(i * 10, 10, 0), Quaternion.identity), true);
                    Assert.That(items[i].TryGetSettledPose(out _), Is.True);
                    items[i].OnStored(new Pose(Vector3.zero, Quaternion.identity));
                    Assert.That(roots[i].activeSelf, Is.False);
                    items[i].OnNetworkPose(new Pose(new Vector3(i * 10, 15, 0), Quaternion.identity));
                    Assert.That(roots[i].activeSelf, Is.True);
                    Assert.That(roots[i].GetComponent<Rigidbody>().isKinematic, Is.True);
                }
                yield return new WaitForSeconds(.2f);
                Assert.That(roots[1].GetComponent<Rigidbody>().position.y, Is.EqualTo(15f).Within(.001f));
            }
            finally
            {
                foreach (var go in roots) if (go != null) Object.Destroy(go);
                foreach (var go in uppers) if (go != null) Object.Destroy(go);
                foreach (var go in hands) if (go != null) Object.Destroy(go);
                foreach (var go in walls) if (go != null) Object.Destroy(go);
            }
        }
    }
}
#endif
