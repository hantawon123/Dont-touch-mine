using System.Collections.Generic;
using Game.BotRuntime.Policy;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// 관측 벡터의 칸 수·순서·기본값을 잠근다. 여기가 바뀌면 학습된 ONNX와 호환이 깨진다.
    /// </summary>
    public sealed class PickObservationEncoderTests
    {
        private static PickCandidate Candidate(
            float dx = 0f, float dz = 1f, float dist = 0.5f,
            PickSizeClass size = PickSizeClass.Medium,
            bool goal = false, float age = 0f, string id = "a") =>
            new(dx, dz, dist, size, shreddable: true, heldByOther: false, goalMatch: goal, normalizedAge: age, targetId: id);

        [Test]
        public void Layout_IsFortyFourFloatsAndFiveActions()
        {
            Assert.That(PickObservationLayout.VectorSize, Is.EqualTo(44));
            Assert.That(PickObservationLayout.ActionCount, Is.EqualTo(5));
            Assert.That(PickObservationLayout.Version, Is.EqualTo("pick-obs-v1-44"));
        }

        [Test]
        public void Encode_SelfBlock_IsInFixedOrder()
        {
            var buffer = new float[PickObservationLayout.VectorSize];
            var self = new PickSelfState(handOccupied: true, timeLeftRatio: 0.25f, busy: false, lastOutcome: PickOutcome.Failure);

            PickObservationEncoder.Encode(self, new List<PickCandidate>(), PickFailure.None, buffer);

            Assert.That(buffer[0], Is.EqualTo(1f));      // 손 점유
            Assert.That(buffer[1], Is.EqualTo(0.25f));   // 남은 시간
            Assert.That(buffer[2], Is.EqualTo(0f));      // 실행 중
            Assert.That(new[] { buffer[3], buffer[4], buffer[5] }, Is.EqualTo(new[] { 0f, 0f, 1f })); // 직전 결과 = 실패
        }

        [Test]
        public void Encode_EmptySlots_AreAllZero()
        {
            var buffer = new float[PickObservationLayout.VectorSize];
            var self = new PickSelfState(false, 1f, false, PickOutcome.None);

            PickObservationEncoder.Encode(self, new List<PickCandidate> { Candidate() }, PickFailure.None, buffer);

            // 슬롯 2와 3 (인덱스 17~38)은 전부 0
            for (var i = PickObservationLayout.SelfSize + PickObservationLayout.CandidateSize;
                 i < PickObservationLayout.SelfSize + PickObservationLayout.CandidateSize * 3;
                 i++)
            {
                Assert.That(buffer[i], Is.EqualTo(0f), $"index {i}");
            }
        }

        [Test]
        public void Encode_CandidateBlock_IsInFixedOrder()
        {
            var buffer = new float[PickObservationLayout.VectorSize];
            var self = new PickSelfState(false, 1f, false, PickOutcome.None);
            var candidate = Candidate(dx: -0.5f, dz: 0.8f, dist: 0.4f, size: PickSizeClass.Large, goal: true, age: 0.5f);

            PickObservationEncoder.Encode(self, new List<PickCandidate> { candidate }, PickFailure.None, buffer);

            var b = PickObservationLayout.SelfSize;
            Assert.That(buffer[b + 0], Is.EqualTo(1f));     // 존재
            Assert.That(buffer[b + 1], Is.EqualTo(-0.5f));  // 방향 X
            Assert.That(buffer[b + 2], Is.EqualTo(0.8f));   // 방향 Z
            Assert.That(buffer[b + 3], Is.EqualTo(0.4f));   // 거리
            Assert.That(new[] { buffer[b + 4], buffer[b + 5], buffer[b + 6] }, Is.EqualTo(new[] { 0f, 0f, 1f })); // 크기 = 큼
            Assert.That(buffer[b + 7], Is.EqualTo(1f));     // 파쇄 가능
            Assert.That(buffer[b + 8], Is.EqualTo(0f));     // 타인 점유
            Assert.That(buffer[b + 9], Is.EqualTo(1f));     // 목표 일치
            Assert.That(buffer[b + 10], Is.EqualTo(0.5f));  // 관측 나이
        }

        [Test]
        public void Encode_FeedbackBlock_IsOneHotAtTheEnd()
        {
            var buffer = new float[PickObservationLayout.VectorSize];
            var self = new PickSelfState(false, 1f, false, PickOutcome.None);

            PickObservationEncoder.Encode(self, null, PickFailure.TargetLost, buffer);

            var f = PickObservationLayout.VectorSize - PickObservationLayout.FeedbackSize;
            Assert.That(new[] { buffer[f], buffer[f + 1], buffer[f + 2], buffer[f + 3], buffer[f + 4] },
                Is.EqualTo(new[] { 0f, 0f, 0f, 1f, 0f }));
        }

        [Test]
        public void Encode_ClampsOutOfRangeValues()
        {
            var buffer = new float[PickObservationLayout.VectorSize];
            var self = new PickSelfState(false, timeLeftRatio: 7f, false, PickOutcome.None);

            PickObservationEncoder.Encode(self, new List<PickCandidate> { Candidate(dx: 3f, dist: -1f, age: 9f) }, PickFailure.None, buffer);

            Assert.That(buffer[1], Is.EqualTo(1f));
            var b = PickObservationLayout.SelfSize;
            Assert.That(buffer[b + 1], Is.EqualTo(1f));
            Assert.That(buffer[b + 3], Is.EqualTo(0f));
            Assert.That(buffer[b + 10], Is.EqualTo(1f));
        }

        [Test]
        public void Encode_WrongBufferSize_Throws()
        {
            var self = new PickSelfState(false, 1f, false, PickOutcome.None);
            Assert.Throws<System.ArgumentException>(() =>
                PickObservationEncoder.Encode(self, null, PickFailure.None, new float[10]));
        }

        [Test]
        public void ClassifySize_UsesFixedThresholds()
        {
            Assert.That(PickObservationEncoder.ClassifySize(0.2f), Is.EqualTo(PickSizeClass.Small));
            Assert.That(PickObservationEncoder.ClassifySize(0.5f), Is.EqualTo(PickSizeClass.Medium));
            Assert.That(PickObservationEncoder.ClassifySize(1.2f), Is.EqualTo(PickSizeClass.Large));
        }

        [Test]
        public void Mask_Idle_AllowsExploreAndOnlyExistingSlots()
        {
            var enabled = new bool[PickObservationLayout.ActionCount];
            var self = new PickSelfState(false, 1f, busy: false, PickOutcome.None);

            PickActionMask.Compute(self, new List<PickCandidate> { Candidate(), Candidate(id: "b") }, enabled);

            Assert.That(enabled, Is.EqualTo(new[] { true, true, true, true, false }));
        }

        [Test]
        public void Mask_Busy_AllowsOnlyContinue()
        {
            var enabled = new bool[PickObservationLayout.ActionCount];
            var self = new PickSelfState(false, 1f, busy: true, PickOutcome.None);

            PickActionMask.Compute(self, new List<PickCandidate> { Candidate() }, enabled);

            Assert.That(enabled, Is.EqualTo(new[] { true, false, false, false, false }));
        }

        [Test]
        public void Mask_HandOccupied_BlocksCollectButNotExplore()
        {
            var enabled = new bool[PickObservationLayout.ActionCount];
            var self = new PickSelfState(handOccupied: true, 1f, busy: false, PickOutcome.None);

            PickActionMask.Compute(self, new List<PickCandidate> { Candidate() }, enabled);

            Assert.That(enabled, Is.EqualTo(new[] { true, true, false, false, false }));
        }

        [Test]
        public void Mask_DoesNotHideWrongGoalCandidates()
        {
            var enabled = new bool[PickObservationLayout.ActionCount];
            var self = new PickSelfState(false, 1f, false, PickOutcome.None);

            PickActionMask.Compute(self, new List<PickCandidate> { Candidate(goal: false) }, enabled);

            Assert.That(enabled[PickObservationLayout.ActionCollectSlot0], Is.True,
                "정답 아닌 물건도 선택 가능해야 모델이 배울 문제가 남는다");
        }
    }
}
