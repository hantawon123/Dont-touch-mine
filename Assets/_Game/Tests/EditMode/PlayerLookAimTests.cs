using Game.Client.Players;
using Game.Core.Players;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerLookAimTests
    {
        [Test]
        public void Apply_PitchesHeadDownWhenCameraLooksDown()
        {
            var root = new GameObject("Character");
            var neck = new GameObject("Neck");
            var head = new GameObject("Head");
            try
            {
                neck.transform.SetParent(root.transform, false);
                head.transform.SetParent(neck.transform, false);
                var before = head.transform.forward.y;
                PlayerLookAim.Apply(
                    root.transform, neck.transform, head.transform, PlayerLookAim.MaxPitch, PlayerPosture.Standing);
                Assert.That(head.transform.forward.y, Is.LessThan(before));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_PitchesHeadUpWhenCameraLooksUp()
        {
            var root = new GameObject("Character");
            var neck = new GameObject("Neck");
            var head = new GameObject("Head");
            try
            {
                neck.transform.SetParent(root.transform, false);
                head.transform.SetParent(neck.transform, false);
                var before = head.transform.forward.y;
                PlayerLookAim.Apply(
                    root.transform, neck.transform, head.transform, -40f, PlayerPosture.Standing);
                Assert.That(head.transform.forward.y, Is.GreaterThan(before));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        [Test]
        public void ClampPitch_LimitsProneMoreThanStanding()
        {
            Assert.That(PlayerLookAim.ClampPitch(200f), Is.EqualTo(PlayerLookAim.MaxPitch));
            Assert.That(
                Mathf.Abs(PlayerLookAim.ClampPitch(200f, PlayerPosture.Prone)),
                Is.LessThan(Mathf.Abs(PlayerLookAim.ClampPitch(200f))));
        }

        [Test]
        public void ClampPitch_KeepsHeadLevelThroughOrdinaryThirdPersonPitch()
        {
            // The camera rides above the character, so simply framing the floor
            // ahead must not bow the head at all.
            Assert.That(PlayerLookAim.ClampPitch(PlayerLookAim.DownLevelAngle - 5f), Is.EqualTo(0f));
            Assert.That(PlayerLookAim.ClampPitch(PlayerLookAim.DownLevelAngle), Is.EqualTo(0f));
        }

        [Test]
        public void ClampPitch_StillBowsHeadPastTheLevelBand()
        {
            var deep = PlayerLookAim.ClampPitch(PlayerLookAim.MaxPitch);
            Assert.That(deep, Is.GreaterThan(0f));
            Assert.That(deep, Is.LessThan(PlayerLookAim.MaxPitch));
            // Still monotonic, so the head keeps following the mouse downward.
            Assert.That(deep, Is.GreaterThan(PlayerLookAim.ClampPitch(50f)));
            Assert.That(
                PlayerLookAim.ClampPitch(50f),
                Is.GreaterThan(PlayerLookAim.ClampPitch(PlayerLookAim.DownLevelAngle + 1f)));
        }

        [Test]
        public void ClampPitch_LeavesUpwardLookUntouched()
        {
            Assert.That(PlayerLookAim.ClampPitch(-40f), Is.EqualTo(-40f));
            Assert.That(PlayerLookAim.ClampPitch(-80f), Is.EqualTo(PlayerLookAim.MinPitch));
        }

        [Test]
        public void ClampPitch_ReachesDeepBowAtTheCameraLimit()
        {
            // A full mouse-down must still reach the anatomical limit; the level
            // band is spread over the rig range instead of lost off the top end.
            Assert.That(
                PlayerLookAim.ClampPitch(PlayerLookAim.RigMaxPitch),
                Is.EqualTo(PlayerLookAim.MaxPitch).Within(0.001f));
        }
    }
}
