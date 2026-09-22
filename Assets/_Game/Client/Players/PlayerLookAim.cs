using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>
    /// Tilts neck and head to match camera pitch. Generic DGN bones are not
    /// Humanoid, so Animator look-at IK cannot be used.
    /// </summary>
    public static class PlayerLookAim
    {
        public const float MinPitch = -60f;
        public const float MaxPitch = 70f;
        public const float NeckShare = 0.4f;
        public const float ProneScale = 0.35f;

        public static float ClampPitch(float degrees, PlayerPosture posture = PlayerPosture.Standing)
        {
            var scaled = posture == PlayerPosture.Prone ? degrees * ProneScale : degrees;
            return Mathf.Clamp(scaled, MinPitch, MaxPitch);
        }

        public static void Apply(
            Transform character,
            Transform neck,
            Transform head,
            float pitchDegrees,
            PlayerPosture posture)
        {
            if (character == null)
            {
                return;
            }

            var pitch = ClampPitch(pitchDegrees, posture);
            if (Mathf.Abs(pitch) < 0.01f)
            {
                return;
            }

            var right = character.right;
            var neckPitch = pitch * NeckShare;
            var headPitch = pitch - neckPitch;
            if (neck != null)
            {
                neck.Rotate(right, neckPitch, Space.World);
            }

            if (head != null)
            {
                head.Rotate(right, headPitch, Space.World);
            }
        }
    }
}
