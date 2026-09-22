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

        [Test]
        public void StabilizedView_KeepsLookingUpWithoutRollingOrTurning()
        {
            // 천장을 보는 순간 머리 본이 좌우로 뒤틀려도 시점은 기절 직전 방향을 지킨다.
            var twisted = Quaternion.Euler(-88f, 150f, 120f);
            var view = PlayerStunView.Stabilize(twisted, 40f);
            Assert.That(PlayerStunView.LooksSkyward(view), Is.True);
            Assert.That(view.eulerAngles.y, Is.EqualTo(40f).Within(0.5f));
            Assert.That((view * Vector3.right).y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void StandingBackUp_UnwindsPitchWithoutASpin()
        {
            // 일어나는 동안 머리가 좌우로 한 바퀴 돌아도 화면은 위에서 정면으로만 내려온다.
            const float lockedYaw = 40f;
            var previous = PlayerStunView.Stabilize(Quaternion.Euler(-90f, 0f, 0f), lockedYaw);
            for (var turn = 0f; turn <= 360f; turn += 45f)
            {
                var head = Quaternion.Euler(Mathf.Lerp(-90f, 0f, turn / 360f), turn, turn);
                var view = PlayerStunView.Stabilize(head, lockedYaw);
                Assert.That(Quaternion.Angle(previous, view), Is.LessThan(20f),
                    $"머리가 {turn}도 돌아간 프레임에서 시점이 튀었다.");
                previous = view;
            }

            Assert.That(PlayerStunView.Pitch(previous), Is.EqualTo(0f).Within(0.5f));
            Assert.That(previous.eulerAngles.y, Is.EqualTo(lockedYaw).Within(0.5f));
        }
    }
}
