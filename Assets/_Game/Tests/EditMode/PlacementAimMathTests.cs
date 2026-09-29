using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlacementAimMathTests
    {
        private static readonly Vector3 Feet = Vector3.zero;
        private static readonly Vector3 Eye = new(0f, 1.42f, 0f);

        [Test]
        public void MaxDistance_LevelView_ReachesTheHorizontalEdgeOfTheReachSphere()
        {
            var d = PlacementAimMath.MaxDistanceAlongView(Eye, Vector3.forward, Feet, 2.8f, 0.45f);
            // 눈높이 1.42에서 정면: sqrt(2.8² − 1.42²)
            Assert.That(d, Is.EqualTo(Mathf.Sqrt(2.8f * 2.8f - 1.42f * 1.42f)).Within(1e-4f));
            var end = Eye + Vector3.forward * d;
            Assert.That(Vector3.Distance(end, Feet), Is.EqualTo(2.8f).Within(1e-4f), "끝점은 발에서 정확히 한도 거리");
        }

        [Test]
        public void MaxDistance_LookingDown_IsLongerAndStaysOnTheViewRay()
        {
            var down = (Vector3.forward + Vector3.down).normalized;
            var d = PlacementAimMath.MaxDistanceAlongView(Eye, down, Feet, 2.8f, 0.45f);
            var level = PlacementAimMath.MaxDistanceAlongView(Eye, Vector3.forward, Feet, 2.8f, 0.45f);
            Assert.That(d, Is.GreaterThan(level));
            Assert.That(Vector3.Distance(Eye + down * d, Feet), Is.EqualTo(2.8f).Within(1e-4f));
        }

        [Test]
        public void MaxDistance_LookingUp_IsShorterThanLevel()
        {
            var up = (Vector3.forward + Vector3.up * 0.5f).normalized;
            var d = PlacementAimMath.MaxDistanceAlongView(Eye, up, Feet, 2.8f, 0.45f);
            var level = PlacementAimMath.MaxDistanceAlongView(Eye, Vector3.forward, Feet, 2.8f, 0.45f);
            Assert.That(d, Is.LessThan(level));
        }

        [Test]
        public void MaxDistance_NeverBelowMinimum()
        {
            // 발이 한도 밖(예외 상황)이거나 한도가 아주 작으면 최소 거리로 떨어진다
            var d = PlacementAimMath.MaxDistanceAlongView(Eye, Vector3.forward, Feet, 0.5f, 0.45f);
            Assert.That(d, Is.EqualTo(0.45f));
        }

        [TestCase(0f, 0f)]
        [TestCase(90f, 0.25f)]
        [TestCase(-180f, 0.5f)]
        [TestCase(360f, 1f)]
        [TestCase(500f, 1f)]
        public void FillFraction_MapsDegreesToTheCircle(float degrees, float expected)
        {
            Assert.That(PlacementAimMath.FillFraction(degrees), Is.EqualTo(expected).Within(1e-6f));
        }

        [Test]
        public void FilterDominantAxis_DropsSmallCrossAxisJitterButKeepsDiagonals()
        {
            Assert.That(PlacementAimMath.FilterDominantAxis(new Vector2(10f, 1f), 2.5f), Is.EqualTo(new Vector2(10f, 0f)));
            Assert.That(PlacementAimMath.FilterDominantAxis(new Vector2(1f, -10f), 2.5f), Is.EqualTo(new Vector2(0f, -10f)));
            Assert.That(PlacementAimMath.FilterDominantAxis(new Vector2(6f, 5f), 2.5f), Is.EqualTo(new Vector2(6f, 5f)));
        }
    }
}
