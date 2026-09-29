using Game.Bootstrap;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PhysicsPlacementValidatorTests
    {
        private GameObject floor;
        private GameObject obstacle;
        private GameObject player;
        private PhysicsPlacementValidator validator;

        [SetUp]
        public void SetUp()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);

            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetPositionAndRotation(new Vector3(2f, 0.5f, 0f), Quaternion.identity);

            player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.AddComponent<CharacterController>();

            Physics.SyncTransforms();
            var defaultLayerMask = 1 << 0;
            validator = new PhysicsPlacementValidator(
                new[]
                {
                    new PlacementVolume("apple", Vector3.zero, Vector3.one * 0.5f)
                },
                defaultLayerMask,
                defaultLayerMask);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(obstacle);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void IsValid_IgnoresOwnPlacedColliderButRejectsOtherItems()
        {
            var item = obstacle.AddComponent<Game.Client.Interactions.CarryableItem>();
            item.UseObjectId("apple");
            var pose = new Pose(obstacle.transform.position, Quaternion.identity);
            Assert.That(validator.IsValid("apple", pose), Is.True);
            item.UseObjectId("other");
            Assert.That(validator.IsValid("apple", pose), Is.False);
        }

        [Test]
        public void IsValid_RequiresSupportAndRejectsObstacleOverlap()
        {
            Assert.That(
                validator.IsValid(
                    "apple",
                    new Pose(new Vector3(0f, 0.5f, 0f), Quaternion.identity)),
                Is.True);
            Assert.That(
                validator.IsValid(
                    "apple",
                    new Pose(new Vector3(2f, 0.5f, 0f), Quaternion.identity)),
                Is.False);
            Assert.That(
                validator.IsValid(
                    "apple",
                    new Pose(new Vector3(20f, 0.5f, 0f), Quaternion.identity)),
                Is.False);
            Assert.That(validator.IsValid("unknown", Pose.identity), Is.False);
        }

        [Test]
        public void IsValid_TiltedItemRestingOnCornerIsSupported()
        {
            // 45도 기울인 정육면체는 모서리로 바닥에 닿는다. 회전한 반높이는 0.5*(cos45+sin45) ≈ 0.707.
            // 예전 검사는 회전을 무시한 0.5 + 0.05만 내려 봐서 바닥을 못 찾고 거부했다.
            var tilt = Quaternion.AngleAxis(45f, Vector3.right);
            var restingHeight = PhysicsPlacementValidator.RotatedVerticalExtent(tilt, Vector3.one * 0.5f);
            Assert.That(restingHeight, Is.EqualTo(0.7071f).Within(0.001f));

            Assert.That(
                validator.IsValid("apple", new Pose(new Vector3(0f, restingHeight + 0.01f, 0f), tilt)),
                Is.True,
                "모서리가 바닥에 닿은 기울인 물건은 받침이 있다.");
            Assert.That(
                validator.IsValid("apple", new Pose(new Vector3(0f, restingHeight + 0.3f, 0f), tilt)),
                Is.False,
                "바닥에서 떠 있으면 거부한다.");
        }
    }
}
