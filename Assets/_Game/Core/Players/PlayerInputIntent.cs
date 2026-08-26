using System;

namespace Game.Core.Players
{
    [Flags]
    public enum PlayerInputButtons : byte
    {
        None = 0,
        Jump = 1 << 0,
        Sprint = 1 << 1,
        Crouch = 1 << 2,
        Prone = 1 << 3
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
            PlayerInputButtons.Prone;

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
