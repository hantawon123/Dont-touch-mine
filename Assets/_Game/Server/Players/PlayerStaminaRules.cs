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
                    current <= 0f);
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
