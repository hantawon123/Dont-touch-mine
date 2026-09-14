using System.Collections;
using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests.EditMode
{
    public class CarriedItemNetworkPoseTests
    {
        [UnityTest]
        public IEnumerator InactiveItemAcceptsNetworkPoseAndFollowsHolderAfterPickup()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new EnterPlayMode();
            var itemRoot = new GameObject("Inactive network item");
            itemRoot.SetActive(false);
            var body = itemRoot.AddComponent<Rigidbody>();
            var item = itemRoot.AddComponent<CarryableItem>();
            var holder = new GameObject("Holder");
            try
            {
                item.OnNetworkPose(new Pose(new Vector3(10, 2, 0), Quaternion.identity));
                yield return new WaitForFixedUpdate();
                item.OnPickedUp(holder.transform);
                Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.None));
                for (var i = 0; i < 4; i++)
                {
                    holder.transform.position += Vector3.right;
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    Assert.That(Vector3.Distance(item.transform.position, holder.transform.position), Is.LessThan(0.001f));
                }
                item.OnNetworkPose(new Pose(Vector3.zero, Quaternion.identity));
                Assert.That(item.transform.parent, Is.Null);
                Assert.That(item.IsCarried, Is.False);
                Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
            }
            finally { Object.DestroyImmediate(itemRoot); Object.DestroyImmediate(holder); }
            yield return new ExitPlayMode();
        }
    }
}
