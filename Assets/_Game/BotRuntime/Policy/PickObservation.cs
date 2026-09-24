using System.Collections.Generic;
using UnityEngine;

namespace Game.BotRuntime.Policy
{
    /// <summary>직전 집기 동작의 결과. 관측의 "직전 결과" 원핫 3칸.</summary>
    public enum PickOutcome
    {
        None = 0,
        Success = 1,
        Failure = 2,
    }

    /// <summary>직전 실패의 종류. 관측의 "실행 피드백" 원핫 5칸.</summary>
    public enum PickFailure
    {
        None = 0,
        NoPath = 1,
        Stalled = 2,
        TargetLost = 3,
        Rejected = 4,
    }

    /// <summary>콜라이더 경계 상자의 최대 변 길이로 나눈 크기 등급. 원핫 3칸.</summary>
    public enum PickSizeClass
    {
        Small = 0,
        Medium = 1,
        Large = 2,
    }

    /// <summary>
    /// 후보 물건 하나를 정책이 읽는 성질로 표현한 것. 이름·좌표·Transform은 없다.
    /// TargetId는 실행부가 슬롯 번호를 실제 물건으로 되돌릴 때만 쓰고 벡터에는 들어가지 않는다.
    /// </summary>
    public readonly struct PickCandidate
    {
        public PickCandidate(
            float directionX,
            float directionZ,
            float normalizedDistance,
            PickSizeClass size,
            bool shreddable,
            bool heldByOther,
            bool goalMatch,
            float normalizedAge,
            string targetId)
        {
            Exists = true;
            DirectionX = directionX;
            DirectionZ = directionZ;
            NormalizedDistance = normalizedDistance;
            Size = size;
            Shreddable = shreddable;
            HeldByOther = heldByOther;
            GoalMatch = goalMatch;
            NormalizedAge = normalizedAge;
            TargetId = targetId ?? string.Empty;
        }

        public bool Exists { get; }
        public float DirectionX { get; }
        public float DirectionZ { get; }
        public float NormalizedDistance { get; }
        public PickSizeClass Size { get; }
        public bool Shreddable { get; }
        public bool HeldByOther { get; }
        public bool GoalMatch { get; }
        public float NormalizedAge { get; }
        public string TargetId { get; }
    }

    /// <summary>봇 자신의 상태. 관측 앞 6칸.</summary>
    public readonly struct PickSelfState
    {
        public PickSelfState(bool handOccupied, float timeLeftRatio, bool busy, PickOutcome lastOutcome)
        {
            HandOccupied = handOccupied;
            TimeLeftRatio = timeLeftRatio;
            Busy = busy;
            LastOutcome = lastOutcome;
        }

        public bool HandOccupied { get; }
        public float TimeLeftRatio { get; }
        public bool Busy { get; }
        public PickOutcome LastOutcome { get; }
    }

    /// <summary>
    /// 관측 벡터와 행동의 고정 배치. 이 숫자들이 바뀌면 모델 버전이 바뀐다.
    /// Behavior Parameters의 Vector Observation Space Size는 <see cref="VectorSize"/>,
    /// Discrete Branch 0 크기는 <see cref="ActionCount"/>와 같아야 한다.
    /// </summary>
    public static class PickObservationLayout
    {
        public const int SelfSize = 6;
        public const int CandidateSize = 11;
        public const int CandidateSlots = 3;
        public const int FeedbackSize = 5;
        public const int VectorSize = SelfSize + CandidateSize * CandidateSlots + FeedbackSize; // 44

        public const int ActionContinue = 0;
        public const int ActionExplore = 1;
        public const int ActionCollectSlot0 = 2;
        public const int ActionCount = ActionCollectSlot0 + CandidateSlots; // 5

        public const string Version = "pick-obs-v1-44";

        public static bool IsCollectAction(int action) =>
            action >= ActionCollectSlot0 && action < ActionCollectSlot0 + CandidateSlots;

        public static int SlotOfAction(int action) => action - ActionCollectSlot0;
    }

    /// <summary>
    /// 정책에 넘길 숫자 44개를 항상 같은 순서로 채운다. 모든 값은 -1~1 또는 0~1이다.
    /// 순서: 자기 상태 6 → 후보 슬롯 3×11(가까운 순, 빈 슬롯은 전부 0) → 실행 피드백 5.
    /// </summary>
    public static class PickObservationEncoder
    {
        public const float SmallMaxExtentMetres = 0.35f;
        public const float MediumMaxExtentMetres = 0.8f;

        public static void Encode(
            in PickSelfState self,
            IReadOnlyList<PickCandidate> candidates,
            PickFailure lastFailure,
            float[] buffer)
        {
            if (buffer == null || buffer.Length != PickObservationLayout.VectorSize)
            {
                throw new System.ArgumentException(
                    $"buffer must have exactly {PickObservationLayout.VectorSize} floats",
                    nameof(buffer));
            }

            var index = 0;

            // 자기 상태 (6)
            buffer[index++] = self.HandOccupied ? 1f : 0f;
            buffer[index++] = Mathf.Clamp01(self.TimeLeftRatio);
            buffer[index++] = self.Busy ? 1f : 0f;
            WriteOneHot(buffer, ref index, (int)self.LastOutcome, 3);

            // 후보 슬롯 (3 × 11)
            for (var slot = 0; slot < PickObservationLayout.CandidateSlots; slot++)
            {
                if (candidates != null && slot < candidates.Count && candidates[slot].Exists)
                {
                    var candidate = candidates[slot];
                    buffer[index++] = 1f;
                    buffer[index++] = Mathf.Clamp(candidate.DirectionX, -1f, 1f);
                    buffer[index++] = Mathf.Clamp(candidate.DirectionZ, -1f, 1f);
                    buffer[index++] = Mathf.Clamp01(candidate.NormalizedDistance);
                    WriteOneHot(buffer, ref index, (int)candidate.Size, 3);
                    buffer[index++] = candidate.Shreddable ? 1f : 0f;
                    buffer[index++] = candidate.HeldByOther ? 1f : 0f;
                    buffer[index++] = candidate.GoalMatch ? 1f : 0f;
                    buffer[index++] = Mathf.Clamp01(candidate.NormalizedAge);
                }
                else
                {
                    for (var k = 0; k < PickObservationLayout.CandidateSize; k++)
                    {
                        buffer[index++] = 0f;
                    }
                }
            }

            // 실행 피드백 (5)
            WriteOneHot(buffer, ref index, (int)lastFailure, PickObservationLayout.FeedbackSize);
        }

        public static PickSizeClass ClassifySize(float largestExtentMetres)
        {
            if (largestExtentMetres < SmallMaxExtentMetres) return PickSizeClass.Small;
            if (largestExtentMetres < MediumMaxExtentMetres) return PickSizeClass.Medium;
            return PickSizeClass.Large;
        }

        private static void WriteOneHot(float[] buffer, ref int index, int value, int size)
        {
            for (var k = 0; k < size; k++)
            {
                buffer[index++] = k == value ? 1f : 0f;
            }
        }
    }

    /// <summary>
    /// 불가능한 행동을 막는 규칙. Continue는 항상 유효하다. 동작 중에는 Continue만 유효하다.
    /// 후보가 없는 슬롯과 손이 찬 상태의 Collect는 막는다. "정답 아닌 물건"은 막지 않는다.
    /// </summary>
    public static class PickActionMask
    {
        public static void Compute(
            in PickSelfState self,
            IReadOnlyList<PickCandidate> candidates,
            bool[] enabled)
        {
            if (enabled == null || enabled.Length != PickObservationLayout.ActionCount)
            {
                throw new System.ArgumentException(
                    $"enabled must have exactly {PickObservationLayout.ActionCount} entries",
                    nameof(enabled));
            }

            enabled[PickObservationLayout.ActionContinue] = true;
            enabled[PickObservationLayout.ActionExplore] = !self.Busy;

            for (var slot = 0; slot < PickObservationLayout.CandidateSlots; slot++)
            {
                var hasCandidate = candidates != null && slot < candidates.Count && candidates[slot].Exists;
                enabled[PickObservationLayout.ActionCollectSlot0 + slot] =
                    !self.Busy && !self.HandOccupied && hasCandidate;
            }
        }
    }
}
