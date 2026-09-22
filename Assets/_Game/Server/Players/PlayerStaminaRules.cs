using System;
using Game.Core.Match;
using Game.Core.Players;

namespace Game.Server.Players
{
    public readonly struct PlayerStaminaState
    {
        internal PlayerStaminaState(float value, bool isExhausted)
        {
            Value = value;
            IsExhausted = isExhausted;
        }

        public float Value { get; }
        public bool IsExhausted { get; }
        public bool CanSprint => !IsExhausted && Value > 0f;
    }

    public static class PlayerStaminaRules
    {
        /// <summary>
        /// 스태미나가 실제로 소모되는 구간은 찾기 페이즈뿐이다. 로비·대기(Waiting, 시작 카운트다운·맵 로딩 포함),
        /// 숨기기, 하이라이트, 엔딩 무대(Result)에서는 무제한으로 달릴 수 있다.
        /// </summary>
        public static bool IsUnlimitedInPhase(MatchPhase phase) =>
            phase != MatchPhase.Searching;

        /// <summary>
        /// 찾기 페이즈의 마지막 구간인지. 구간 길이는 최종 경고(<c>MatchRulesSO.FinalWarningSeconds</c>)와
        /// 같은 값을 쓴다. 배너가 "마지막 30초"라고 알리는 그 구간이 곧 무제한 달리기 구간이다.
        /// </summary>
        public static bool IsFinalSprintPeriod(
            MatchPhase phase,
            double remainingSeconds,
            double windowSeconds) =>
            phase == MatchPhase.Searching &&
            windowSeconds > 0d &&
            remainingSeconds > 0d &&
            remainingSeconds <= windowSeconds;

        /// <summary>
        /// 지금 스태미나 소모 없이 달릴 수 있는지. 소모 구간은 찾기 페이즈뿐이고,
        /// 그중에서도 마지막 구간에 들어서면 다시 무제한이 된다.
        /// </summary>
        public static bool IsUnlimited(
            MatchPhase phase,
            double remainingSeconds,
            double windowSeconds) =>
            IsUnlimitedInPhase(phase) ||
            IsFinalSprintPeriod(phase, remainingSeconds, windowSeconds);

        public static PlayerStaminaState Step(
            float current,
            bool isExhausted,
            bool isSprinting,
            float deltaTime,
            PlayerMovementSettings settings,
            bool unlimited = false)
        {
            if (!float.IsFinite(current) || current < 0f || current > settings.MaxStamina)
            {
                throw new ArgumentOutOfRangeException(nameof(current));
            }

            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (unlimited) return new PlayerStaminaState(settings.MaxStamina, false);
            isExhausted |= current <= 0f;
            if (isExhausted)
            {
                current = MathF.Min(
                    settings.MaxStamina,
                    current + settings.StaminaRecoveryPerSecond * deltaTime);
                return new PlayerStaminaState(
                    current,
                    current < settings.MaxStamina);
            }

            if (isSprinting)
            {
                current = MathF.Max(
                    0f,
                    current - settings.StaminaDrainPerSecond * deltaTime);
                return new PlayerStaminaState(current, current <= 0f);
            }

            current = MathF.Min(
                settings.MaxStamina,
                current + settings.StaminaRecoveryPerSecond * deltaTime);
            return new PlayerStaminaState(current, false);
        }
    }
}
