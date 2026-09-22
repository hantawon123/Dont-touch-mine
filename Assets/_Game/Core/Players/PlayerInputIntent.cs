using System;

namespace Game.Core.Players
{
    public enum PlayerPosture : byte
    {
        Standing,
        Crouching,
        Prone
    }

    public interface IPlayerInputIntentSource
    {
        PlayerMovementSettings MovementSettings { get; }

        PlayerInputIntent CaptureInputIntent();
    }

    /// <summary>
    /// Engine-neutral movement values shared by local and network simulation.
    /// </summary>
    public readonly struct PlayerMovementSettings
    {
        public PlayerMovementSettings(
            float walkSpeed,
            float sprintSpeed,
            float rotationSpeedDegrees,
            float jumpHeight,
            float gravityMultiplier,
            float crouchSpeed = 2f,
            float proneSpeed = 0.8f,
            float standHeight = 1.8f,
            float crouchHeight = 1.2f,
            float proneHeight = 0.6f,
            float maxStamina = 100f,
            float staminaDrainPerSecond = 20f,
            float staminaRecoveryPerSecond = 15f)
        {
            if (!float.IsFinite(walkSpeed) || walkSpeed < 0f ||
                !float.IsFinite(sprintSpeed) || sprintSpeed < 0f ||
                !float.IsFinite(rotationSpeedDegrees) || rotationSpeedDegrees < 0f ||
                !float.IsFinite(jumpHeight) || jumpHeight < 0f ||
                !float.IsFinite(gravityMultiplier) || gravityMultiplier <= 0f ||
                !float.IsFinite(crouchSpeed) || crouchSpeed < 0f ||
                !float.IsFinite(proneSpeed) || proneSpeed < 0f ||
                !float.IsFinite(standHeight) || standHeight <= 0f ||
                !float.IsFinite(crouchHeight) || crouchHeight <= 0f ||
                !float.IsFinite(proneHeight) || proneHeight <= 0f ||
                !float.IsFinite(maxStamina) || maxStamina <= 0f ||
                !float.IsFinite(staminaDrainPerSecond) || staminaDrainPerSecond <= 0f ||
                !float.IsFinite(staminaRecoveryPerSecond) || staminaRecoveryPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(walkSpeed),
                    "Movement settings must be finite and non-negative.");
            }

            WalkSpeed = walkSpeed;
            SprintSpeed = sprintSpeed;
            RotationSpeedDegrees = rotationSpeedDegrees;
            JumpHeight = jumpHeight;
            GravityMultiplier = gravityMultiplier;
            CrouchSpeed = crouchSpeed;
            ProneSpeed = proneSpeed;
            StandHeight = standHeight;
            CrouchHeight = crouchHeight;
            ProneHeight = proneHeight;
            MaxStamina = maxStamina;
            StaminaDrainPerSecond = staminaDrainPerSecond;
            StaminaRecoveryPerSecond = staminaRecoveryPerSecond;
        }

        public float WalkSpeed { get; }
        public float SprintSpeed { get; }
        public float RotationSpeedDegrees { get; }
        public float JumpHeight { get; }
        public float GravityMultiplier { get; }
        public float CrouchSpeed { get; }
        public float ProneSpeed { get; }
        public float StandHeight { get; }
        public float CrouchHeight { get; }
        public float ProneHeight { get; }
        public float MaxStamina { get; }
        public float StaminaDrainPerSecond { get; }
        public float StaminaRecoveryPerSecond { get; }
    }

    /// <summary>
    /// Movement math shared by the local Update loop and Fusion simulation.
    /// Keeping it here prevents lobby/network movement from quietly acquiring
    /// different stopping or jumping behaviour.
    /// </summary>
    public static class PlayerMovementKinematics
    {
        private const float GroundedStickVelocity = -2f;

        public static float MoveSpeed(
            PlayerMovementSettings settings,
            bool sprinting) =>
            sprinting ? settings.SprintSpeed : settings.WalkSpeed;

        public static float StepVerticalVelocity(
            float currentVelocity,
            bool grounded,
            bool jumpRequested,
            float physicsGravityY,
            float deltaTime,
            PlayerMovementSettings settings)
        {
            if (grounded)
            {
                currentVelocity = jumpRequested
                    ? MathF.Sqrt(
                        2f * -physicsGravityY * settings.GravityMultiplier *
                        settings.JumpHeight)
                    : GroundedStickVelocity;
            }

            return currentVelocity -
                (-physicsGravityY * settings.GravityMultiplier * deltaTime);
        }
    }

    [Flags]
    public enum PlayerInputButtons : byte
    {
        None = 0,
        Jump = 1 << 0,
        Sprint = 1 << 1,
        Crouch = 1 << 2,
        Prone = 1 << 3,
        Attack = 1 << 4
    }

    /// <summary>
    /// Engine- and transport-neutral movement input for one network tick.
    /// </summary>
    public readonly struct PlayerInputIntent
    {
        private const PlayerInputButtons AllButtons =
            PlayerInputButtons.Jump |
            PlayerInputButtons.Sprint |
            PlayerInputButtons.Crouch |
            PlayerInputButtons.Prone |
            PlayerInputButtons.Attack;

        public PlayerInputIntent(
            float moveX,
            float moveY,
            float lookYawDegrees,
            PlayerInputButtons buttons)
            : this(moveX, moveY, lookYawDegrees, 0f, buttons)
        {
        }

        public PlayerInputIntent(
            float moveX,
            float moveY,
            float lookYawDegrees,
            float lookPitchDegrees,
            PlayerInputButtons buttons)
            : this(moveX, moveY, lookYawDegrees, lookPitchDegrees, buttons, 0, 0)
        {
        }

        /// <param name="emoteSequence">
        /// 감정 표현 요청 번호. 0은 요청 없음이고, 고를 때마다 1..255로 바뀐다.
        /// 같은 값이 계속 실리므로 입력 틱이 빠져도 요청이 사라지지 않는다.
        /// </param>
        /// <param name="emoteId">요청한 감정 표현의 카탈로그 ID.</param>
        public PlayerInputIntent(
            float moveX,
            float moveY,
            float lookYawDegrees,
            float lookPitchDegrees,
            PlayerInputButtons buttons,
            byte emoteSequence,
            byte emoteId)
        {
            if (!float.IsFinite(moveX) ||
                !float.IsFinite(moveY) ||
                !float.IsFinite(lookYawDegrees) ||
                !float.IsFinite(lookPitchDegrees))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moveX),
                    "Input values must be finite.");
            }

            if ((buttons & ~AllButtons) != PlayerInputButtons.None)
            {
                throw new ArgumentOutOfRangeException(nameof(buttons));
            }

            var magnitudeSquared = moveX * moveX + moveY * moveY;
            if (magnitudeSquared > 1f)
            {
                var scale = 1f / (float)Math.Sqrt(magnitudeSquared);
                moveX *= scale;
                moveY *= scale;
            }

            MoveX = moveX;
            MoveY = moveY;
            LookYawDegrees = NormalizeYaw(lookYawDegrees);
            LookPitchDegrees = ClampPitch(lookPitchDegrees);
            Buttons = buttons;
            EmoteSequence = emoteSequence;
            EmoteId = emoteId;
        }

        public float MoveX { get; }
        public float MoveY { get; }
        public float LookYawDegrees { get; }
        public float LookPitchDegrees { get; }
        public PlayerInputButtons Buttons { get; }
        public byte EmoteSequence { get; }
        public byte EmoteId { get; }

        /// <summary>다음 감정 표현 요청 번호. 0은 "요청 없음"이라 건너뛴다.</summary>
        public static byte NextEmoteSequence(byte current) =>
            current == byte.MaxValue ? (byte)1 : (byte)(current + 1);

        public bool IsPressed(PlayerInputButtons button) =>
            (Buttons & button) == button;

        private static float NormalizeYaw(float degrees)
        {
            var normalized = degrees % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static float ClampPitch(float degrees) =>
            Math.Clamp(degrees, -89f, 89f);
    }
}
