using System.Collections;
using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests.PlayMode
{
 public class CarryableTickTests
 {
  [UnityTest]
  public IEnumerator IdleTickSleepsButNetworkAndLocalInteractionsResume()
  {
   var root=new GameObject("Tick test item"); root.SetActive(false);
   var body=root.AddComponent<Rigidbody>(); body.useGravity=false;
   root.AddComponent<BoxCollider>(); var item=root.AddComponent<CarryableItem>();
   var holder=new GameObject("Tick test holder");
   try
   {
    item.OnNetworkPose(new Pose(Vector3.zero,Quaternion.identity));
    Assert.That(item.enabled,Is.True,"First activation must survive Awake.");
    item.OnNetworkPose(new Pose(Vector3.right,Quaternion.identity));
    for(var i=0;i<12;i++) yield return new WaitForFixedUpdate();
    Assert.That(item.enabled,Is.False);
    Assert.That(Vector3.Distance(body.position,Vector3.right),Is.LessThan(.01f));
    item.OnNetworkPose(new Pose(Vector3.right*2,Quaternion.identity));
    Assert.That(item.enabled,Is.True);
    item.OnPickedUp(holder.transform);
    Assert.That(item.enabled,Is.False); Assert.That(item.IsCarried,Is.True);
    holder.transform.position=Vector3.up*2; yield return new WaitForFixedUpdate();
    Assert.That(item.transform.position,Is.EqualTo(holder.transform.position));
    item.OnThrown(Vector3.forward*2);
    Assert.That(body.isKinematic,Is.False); Assert.That(item.IsCarried,Is.False);
    var before=body.position;
    for(var i=0;i<3;i++) yield return new WaitForFixedUpdate();
    Assert.That(body.position.z,Is.GreaterThan(before.z));
    item.OnPlaced(Vector3.zero,Quaternion.identity); Assert.That(body.isKinematic,Is.False);
    item.OnStored(new Pose(Vector3.zero,Quaternion.identity)); Assert.That(root.activeSelf,Is.False);
    item.OnNetworkPose(new Pose(Vector3.right*3,Quaternion.identity));
    Assert.That(root.activeSelf&&item.enabled,Is.True); Assert.That(body.isKinematic,Is.True);
   }
   finally { Object.Destroy(root); Object.Destroy(holder); }
  }
 }
}
