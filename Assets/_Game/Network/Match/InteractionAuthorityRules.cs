using System;
using UnityEngine;

namespace Game.Network.Match
{
    public sealed class InteractionAuthorityRules
    {
        public const float DefaultInteractionDistance = 2f;

        /// <summary>
        /// 놓기·던지기·배치 위치가 플레이어에서 떨어질 수 있는 한도. 배치 모드는 발 기준 손 거리
        /// (InteractionConfig.PlacementMaxDistance, 2.8 m)까지 물건을 두므로 잡기 거리(2 m)보다 넉넉해야 한다.
        /// </summary>
        public const float DefaultReleaseDistance = 3f;
        public const float DefaultMaxThrowSpeed = 8f;

        private const float RotationTolerance = 0.01f;
        private readonly float interactionDistanceSquared;
        private readonly float releaseDistanceSquared;
        private readonly float maxThrowSpeedSquared;

        public InteractionAuthorityRules(
            float interactionDistance = DefaultInteractionDistance,
            float maxThrowSpeed = DefaultMaxThrowSpeed,
            float releaseDistance = DefaultReleaseDistance)
        {
            if (!float.IsFinite(interactionDistance) || interactionDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(interactionDistance));
            }

            if (!float.IsFinite(maxThrowSpeed) || maxThrowSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxThrowSpeed));
            }

            if (!float.IsFinite(releaseDistance) || releaseDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(releaseDistance));
            }

            interactionDistanceSquared = interactionDistance * interactionDistance;
            releaseDistanceSquared = releaseDistance * releaseDistance;
            maxThrowSpeedSquared = maxThrowSpeed * maxThrowSpeed;
        }

        public bool IsWithinReleaseDistance(Vector3 playerPosition, Vector3 releasePosition)
        {
            return IsFinite(playerPosition) &&
                   IsFinite(releasePosition) &&
                   (releasePosition - playerPosition).sqrMagnitude <= releaseDistanceSquared;
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
            return IsWithinReleaseDistance(
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
                   // Normalizing a direction and multiplying by the exact cap can round a few ULPs above it.
                   initialVelocity.sqrMagnitude <= maxThrowSpeedSquared + 0.0001f;
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
