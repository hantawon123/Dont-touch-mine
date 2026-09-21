using Game.Client.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerStunViewTests
    {
        [Test]
        public void FallingHead_LooksTowardTheCeiling()
        {
            var body = new GameObject("Body");
            var head = new GameObject("Head");
            try
            {
                body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                head.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
                var calibrated = PlayerStunView.Calibrate(head.transform.rotation, body.transform.rotation);
                head.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                var pose = PlayerStunView.Pose(head.transform, calibrated);
                Assert.That(PlayerStunView.LooksSkyward(pose.rotation), Is.True);
                Assert.That(pose.position.y, Is.EqualTo(1.6f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(head);
                Object.DestroyImmediate(body);
            }
        }
    }
}
