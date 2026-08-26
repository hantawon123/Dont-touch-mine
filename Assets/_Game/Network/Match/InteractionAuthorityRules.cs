using System;
using UnityEngine;

namespace Game.Network.Match
{
    public sealed class InteractionAuthorityRules
    {
        public const float DefaultInteractionDistance = 2f;
        public const float DefaultMaxThrowSpeed = 8f;

        private const float RotationTolerance = 0.01f;
        private readonly float interactionDistanceSquared;
        private readonly float maxThrowSpeedSquared;

        public InteractionAuthorityRules(
            float interactionDistance = DefaultInteractionDistance,
            float maxThrowSpeed = DefaultMaxThrowSpeed)
        {
            if (!float.IsFinite(interactionDistance) || interactionDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(interactionDistance));
            }

            if (!float.IsFinite(maxThrowSpeed) || maxThrowSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxThrowSpeed));
            }

            interactionDistanceSquared = interactionDistance * interactionDistance;
            maxThrowSpeedSquared = maxThrowSpeed * maxThrowSpeed;
        }

        public bool IsWithinInteractionDistance(
            Vector3 playerPosition,
            Vector3 targetPosition)
        {
            return IsFinite(playerPosition) &&
                   IsFinite(targetPosition) &&
                   (targetPosition - playerPosition).sqrMagnitude <=
                   interactionDistanceSquared;
        }

        public bool IsValidRelease(Pose playerPose, Pose releasePose)
        {
            return IsWithinInteractionDistance(
                       playerPose.position,
                       releasePose.position) &&
                   IsValidRotation(releasePose.rotation);
        }

        public bool IsValidThrow(
            Pose playerPose,
            Pose releasePose,
            Vector3 initialVelocity)
        {
            return IsValidRelease(playerPose, releasePose) &&
                   IsFinite(initialVelocity) &&
                   initialVelocity.sqrMagnitude > 0f &&
                   initialVelocity.sqrMagnitude <= maxThrowSpeedSquared;
        }

        private static bool IsValidRotation(Quaternion rotation)
        {
            if (!float.IsFinite(rotation.x) ||
                !float.IsFinite(rotation.y) ||
                !float.IsFinite(rotation.z) ||
                !float.IsFinite(rotation.w))
            {
                return false;
            }

            return Mathf.Abs(rotation.x * rotation.x +
                             rotation.y * rotation.y +
                             rotation.z * rotation.z +
                             rotation.w * rotation.w - 1f) <= RotationTolerance;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }
    }
}
