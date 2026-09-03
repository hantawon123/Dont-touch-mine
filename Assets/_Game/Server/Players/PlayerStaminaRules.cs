using System;
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
        public static PlayerStaminaState Step(
            float current,
            bool isExhausted,
            bool isSprinting,
            float deltaTime,
            PlayerMovementSettings settings)
        {
            if (!float.IsFinite(current) || current < 0f || current > settings.MaxStamina)
            {
                throw new ArgumentOutOfRangeException(nameof(current));
            }

            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

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
