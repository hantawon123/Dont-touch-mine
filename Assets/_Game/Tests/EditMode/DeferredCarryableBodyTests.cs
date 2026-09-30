#if UNITY_SERVER
using System.Reflection;
using Game.Client.Interactions;
using Game.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Architecture.Tests
{
    public sealed class DeferredCarryableBodyTests
    {
        [TestCase("default", true)]
        [TestCase("mass", false)]
        [TestCase("gravity", false)]
        [TestCase("dynamic", false)]
        [TestCase("constraints", false)]
        [TestCase("layers", false)]
        [TestCase("interpolation", false)]
        [TestCase("trigger", false)]
        [TestCase("joint", false)]
        [TestCase("animatedParent", false)]
        public void OnlyDefaultStationaryIndependentBodiesAreDeferred(string variation, bool expected)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Shelf");
            SceneManager.MoveGameObjectToScene(root, scene);
            var itemObject = new GameObject("Prop", typeof(BoxCollider), typeof(Rigidbody), typeof(CarryableItem));
            SceneManager.MoveGameObjectToScene(itemObject, scene);
            itemObject.transform.SetParent(root.transform);
            var item = itemObject.GetComponent<CarryableItem>();
            var body = itemObject.GetComponent<Rigidbody>();
            body.isKinematic = true;
            switch (variation)
            {
                case "mass": body.mass = 2f; break;
                case "gravity": body.useGravity = false; break;
                case "dynamic": body.isKinematic = false; break;
                case "constraints": body.constraints = RigidbodyConstraints.FreezeRotation; break;
                case "layers": body.excludeLayers = 1 << 6; break;
                case "interpolation": body.interpolation = RigidbodyInterpolation.Interpolate; break;
                case "trigger": itemObject.GetComponent<Collider>().isTrigger = true; break;
                case "joint": itemObject.AddComponent<FixedJoint>(); break;
                case "animatedParent": root.AddComponent<Animator>(); break;
            }
            var id = item.ObjectId;
            var collider = itemObject.GetComponent<Collider>();
            try
            {
                Assert.That(CarryableSceneBuildPreparation.PrepareDeferredBodies(scene), Is.EqualTo(expected ? 1 : 0));
                Assert.That(itemObject.GetComponent<Rigidbody>() == null, Is.EqualTo(expected));
                Assert.That(item.ObjectId, Is.EqualTo(id));
                Assert.That(itemObject.GetComponent<Collider>(), Is.SameAs(collider));
                if (!expected) return;
                typeof(CarryableItem).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(item, null);
                Assert.That(item.TryGetPhysicsPose(out _, out _, out _), Is.False);
                Assert.That(item.TryGetSettledPose(out _), Is.False);
                Assert.That(itemObject.GetComponent<Rigidbody>(), Is.Null, "Read-only sampling must not activate the body.");
                item.OnReleased(new Pose(Vector3.up, Quaternion.identity), Vector3.forward * 4);
                body = itemObject.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null);
                Assert.That(body.mass, Is.EqualTo(1f));
                Assert.That(body.angularDamping, Is.EqualTo(.05f));
                Assert.That(body.linearDamping, Is.Zero);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.useGravity, Is.True);
                Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.forward * 4));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
