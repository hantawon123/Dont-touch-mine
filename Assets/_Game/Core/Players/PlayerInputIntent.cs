using System;

namespace Game.Core.Players
{
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
            float gravityMultiplier)
        {
            if (!float.IsFinite(walkSpeed) || walkSpeed < 0f ||
                !float.IsFinite(sprintSpeed) || sprintSpeed < 0f ||
                !float.IsFinite(rotationSpeedDegrees) || rotationSpeedDegrees < 0f ||
                !float.IsFinite(jumpHeight) || jumpHeight < 0f ||
                !float.IsFinite(gravityMultiplier) || gravityMultiplier <= 0f)
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
        }

        public float WalkSpeed { get; }
        public float SprintSpeed { get; }
        public float RotationSpeedDegrees { get; }
        public float JumpHeight { get; }
        public float GravityMultiplier { get; }
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
        {
            if (!float.IsFinite(moveX) ||
                !float.IsFinite(moveY) ||
                !float.IsFinite(lookYawDegrees))
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
            Buttons = buttons;
        }

        public float MoveX { get; }
        public float MoveY { get; }
        public float LookYawDegrees { get; }
        public PlayerInputButtons Buttons { get; }

        public bool IsPressed(PlayerInputButtons button) =>
            (Buttons & button) == button;

        private static float NormalizeYaw(float degrees)
        {
            var normalized = degrees % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }
    }
}
