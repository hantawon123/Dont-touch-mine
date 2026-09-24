using Game.BotRuntime.Perception;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class BotPerceptionTests
    {
        private GameObject observer;
        private GameObject target;
        private BotPerception perception;

        [SetUp]
        public void SetUp()
        {
            observer = new GameObject("Observer");
            perception = observer.AddComponent<BotPerception>();
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Target";
            target.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(observer);
            Object.DestroyImmediate(target);

            var blocker = GameObject.Find("Sight Blocker");
            if (blocker != null)
            {
                Object.DestroyImmediate(blocker);
            }
        }

        [Test]
        public void TryObserve_TargetInFront_ReturnsSnapshot()
        {
            Assert.That(perception.TryObserve(target.transform, out var result), Is.True);
            Assert.That(result.Observation.LocalDirection.z, Is.GreaterThan(0.99f));
            Assert.That(result.Observation.NormalizedDistance, Is.InRange(0f, 1f));
            Assert.That(result.Handle.IsValid, Is.True);
            Assert.That(Vector3.Distance(result.Handle.ObservedWorldPosition, target.transform.position), Is.LessThan(0.0001f));
        }

        [Test]
        public void TryObserve_TargetBehind_ReturnsFalse()
        {
            target.transform.position = new Vector3(0f, 0f, -5f);
            Physics.SyncTransforms();

            Assert.That(perception.TryObserve(target.transform, out _), Is.False);
        }

        [Test]
        public void TryObserve_TargetBehindWall_ReturnsFalse()
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Sight Blocker";
            blocker.transform.position = new Vector3(0f, 0f, 2.5f);
            blocker.transform.localScale = new Vector3(2f, 2f, 0.5f);
            Physics.SyncTransforms();

            Assert.That(perception.TryObserve(target.transform, out _), Is.False);
        }

        /// <summary>
        /// 정보 누출 재현: 물건이 선반의 자식으로 놓여 있고 선반 판이 시선을 가릴 때,
        /// 선반 콜라이더를 "물건의 일부"로 착각해 보인다고 답하면 안 된다.
        /// 2026-09-23: 판정 함수가 "목표가 맞은 것의 자식"까지 목표로 인정해 실패했고(결함 증거),
        /// 그 조건을 제거한 뒤 통과했다.
        /// </summary>
        [Test]
        public void TryObserve_TargetParentedUnderBlockingShelf_ReturnsFalse()
        {
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Sight Blocker";
            shelf.transform.position = new Vector3(0f, 0f, 2.5f);
            shelf.transform.localScale = new Vector3(2f, 2f, 0.5f);

            // 물건을 선반의 자식으로 옮긴다. 월드 위치는 그대로 (0, 0, 5).
            target.transform.SetParent(shelf.transform, worldPositionStays: true);
            Physics.SyncTransforms();

            Assert.That(perception.TryObserve(target.transform, out _), Is.False);
        }

        /// <summary>
        /// 종류 스티커(BotSightKind)가 붙어 있으면 관측에 그 키가 실린다. 없으면 빈 문자열이다.
        /// </summary>
        [Test]
        public void TryObserve_KindMarker_FillsKindKey()
        {
            Assert.That(perception.TryObserve(target.transform, out var unmarked), Is.True);
            Assert.That(unmarked.Observation.KindKey, Is.Empty);

            target.AddComponent<BotSightKind>().SetKindKey("vase");

            Assert.That(perception.TryObserve(target.transform, out var marked), Is.True);
            Assert.That(marked.Observation.KindKey, Is.EqualTo("vase"));
        }

        /// <summary>
        /// 누출 (다) 방지: 관측 결과는 사진이다. 관측 뒤 물건이 옮겨져도 손잡이에 남은 위치는
        /// 관측 당시 값이어야 하고, 물건을 따라 바뀌면 안 된다.
        /// </summary>
        [Test]
        public void TryObserve_HandlePosition_IsSnapshotNotLiveReference()
        {
            Assert.That(perception.TryObserve(target.transform, out var result), Is.True);
            var seenAt = result.Handle.ObservedWorldPosition;

            target.transform.position = new Vector3(3f, 0f, 9f);
            Physics.SyncTransforms();

            Assert.That(Vector3.Distance(result.Handle.ObservedWorldPosition, seenAt), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(result.Handle.ObservedWorldPosition, target.transform.position), Is.GreaterThan(0.1f));
        }

        [Test]
        public void TryObserveClosest_VisibleCarryable_ReturnsTarget()
        {
            target.layer = LayerMask.NameToLayer("Carryable");
            Physics.SyncTransforms();

            Assert.That(
                perception.TryObserveClosest(
                    LayerMask.GetMask("Carryable"),
                    out var result),
                Is.True);
            Assert.That(Vector3.Distance(result.Handle.ObservedWorldPosition, target.transform.position), Is.LessThan(0.0001f));
        }

        [Test]
        public void TryObserveClosest_CarryableBehindWall_ReturnsFalse()
        {
            target.layer = LayerMask.NameToLayer("Carryable");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Sight Blocker";
            blocker.transform.position = new Vector3(0f, 0f, 2.5f);
            blocker.transform.localScale = new Vector3(2f, 2f, 0.5f);
            Physics.SyncTransforms();

            Assert.That(
                perception.TryObserveClosest(
                    LayerMask.GetMask("Carryable"),
                    out _),
                Is.False);
        }
    }
}
