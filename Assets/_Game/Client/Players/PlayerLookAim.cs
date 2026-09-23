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

        /// <summary>Downward pitch the head absorbs while staying level.</summary>
        public const float DownLevelAngle = 28f;

        /// <summary>
        /// Deepest pitch the camera rig allows, mirroring the serialized
        /// <c>maxPitch</c> on PlayerCameraController. Only used to spread the
        /// remaining head travel over the range the mouse can actually reach.
        /// </summary>
        public const float RigMaxPitch = 85f;

        /// <summary>
        /// How much of the pitch past <see cref="DownLevelAngle"/> reaches the
        /// head. Sized so a full mouse-down still bows the head all the way to
        /// <see cref="MaxPitch"/>, rather than losing the level band off the top
        /// end and leaving the deepest look short.
        /// </summary>
        public static float DownSoftScale => MaxPitch / (RigMaxPitch - DownLevelAngle);

        /// <summary>
        /// Eases off downward pitch. The third-person camera sits above the
        /// character, so the ordinary pitch that just frames the ground ahead
        /// used to bow the head enough to read as walking while staring at the
        /// floor. That whole range now leaves the head level; past it the head
        /// bows again, so deliberately looking down still reads. Upward pitch is
        /// left alone.
        /// </summary>
        /// <remarks>
        /// Applied to every character rather than gated on the view mode: the
        /// first-person camera is placed from the rig instead of the head bone
        /// and hides the body, so the head pose is only ever seen in third
        /// person. Gating it would also miss remote players, whose view mode is
        /// not replicated alongside their pitch.
        /// </remarks>
        public static float SoftenDown(float degrees) =>
            degrees <= DownLevelAngle
                ? Mathf.Min(degrees, 0f)
                : (degrees - DownLevelAngle) * DownSoftScale;

        public static float ClampPitch(float degrees, PlayerPosture posture = PlayerPosture.Standing)
        {
            var softened = Mathf.Clamp(SoftenDown(degrees), MinPitch, MaxPitch);
            return posture == PlayerPosture.Prone ? softened * ProneScale : softened;
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
