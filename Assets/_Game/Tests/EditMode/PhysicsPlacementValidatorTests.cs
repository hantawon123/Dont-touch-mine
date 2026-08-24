using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PhysicsPlacementValidatorTests
    {
        private GameObject floor;
        private GameObject obstacle;
        private PhysicsPlacementValidator validator;

        [SetUp]
        public void SetUp()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);

            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetPositionAndRotation(new Vector3(2f, 0.5f, 0f), Quaternion.identity);

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
    }
}
