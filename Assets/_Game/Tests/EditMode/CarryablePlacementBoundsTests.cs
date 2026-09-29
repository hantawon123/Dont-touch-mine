using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class CarryablePlacementBoundsTests
    {
        [TestCase("sphere")]
        [TestCase("capsule")]
        [TestCase("mesh")]
        public void Bounds_MatchScaledColliderWhenInactive(string shape)
        {
            var root = GameObject.CreatePrimitive(shape == "sphere" ? PrimitiveType.Sphere :
                shape == "capsule" ? PrimitiveType.Capsule : PrimitiveType.Cube);
            try
            {
                root.transform.position = new Vector3(100, 100, 100);
                root.transform.localScale = new Vector3(.4f, .7f, .9f);
                var collider = root.GetComponent<Collider>();
                if (shape == "mesh")
                {
                    Object.DestroyImmediate(collider);
                    var mesh = root.AddComponent<MeshCollider>();
                    mesh.sharedMesh = root.GetComponent<MeshFilter>().sharedMesh;
                    collider = mesh;
                }
                Physics.SyncTransforms();
                var expected = collider.bounds;
                root.SetActive(false);
                var item = root.AddComponent<CarryableItem>();
                Assert.That(Vector3.Distance(item.PlacementHalfExtents, expected.extents), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(item.PlacementCenterOffset, expected.center - root.transform.position), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Bounds_PreserveShapeWhenInactiveOrHeld(bool held)
        {
            var root = new GameObject("Placement bounds regression");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(1000, 1000, 1000), Quaternion.Euler(0, 37, 0));
                root.transform.localScale = new Vector3(2, 3, 4);
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(.2f, 1, -.1f);
                box.size = new Vector3(1, 2, .5f);
                var item = root.AddComponent<CarryableItem>();
                if (held) box.enabled = false;
                else root.SetActive(false);

                Assert.That(Vector3.Distance(item.PlacementHalfExtents, new Vector3(1, 3, 1)), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(item.PlacementCenterOffset, new Vector3(.4f, 3, -.4f)), Is.LessThan(.001f));
                root.SetActive(true);
                box.enabled = true;
                Assert.That(Vector3.Distance(item.PlacementHalfExtents, new Vector3(1, 3, 1)), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
