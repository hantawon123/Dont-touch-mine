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
                    root.transform, neck.transform, head.transform, 40f, PlayerPosture.Standing);
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
            Assert.That(PlayerLookAim.ClampPitch(80f), Is.EqualTo(PlayerLookAim.MaxPitch));
            Assert.That(
                Mathf.Abs(PlayerLookAim.ClampPitch(80f, PlayerPosture.Prone)),
                Is.LessThan(Mathf.Abs(PlayerLookAim.ClampPitch(80f))));
        }
    }
}
