using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Server.Items
{
    public readonly struct PlacementVolume
    {
        public PlacementVolume(string objectId, Vector3 centerOffset, Vector3 halfExtents)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                throw new ArgumentException("Object id is required.", nameof(objectId));
            }

            if (!IsFinite(centerOffset) ||
                !IsFinite(halfExtents) ||
                halfExtents.x <= 0f ||
                halfExtents.y <= 0f ||
                halfExtents.z <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtents));
            }

            ObjectId = objectId.Trim();
            CenterOffset = centerOffset;
            HalfExtents = halfExtents;
        }

        public string ObjectId { get; }
        public Vector3 CenterOffset { get; }
        public Vector3 HalfExtents { get; }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }
    }

    public sealed class PhysicsPlacementValidator : IPlacementValidator
    {
        private readonly Dictionary<string, PlacementVolume> volumesById;
        private readonly int blockingLayerMask;
        private readonly int supportLayerMask;
        private readonly float maxSupportDistance;
        private readonly float skinWidth;
        private readonly Collider[] overlapBuffer = new Collider[32];

        public PhysicsPlacementValidator(
            IReadOnlyList<PlacementVolume> volumes,
            int blockingLayerMask,
            int supportLayerMask,
            float maxSupportDistance = 0.05f,
            float skinWidth = 0.01f)
        {
            if (volumes == null)
            {
                throw new ArgumentNullException(nameof(volumes));
            }

            if (blockingLayerMask == 0 || supportLayerMask == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockingLayerMask));
            }

            if (!float.IsFinite(maxSupportDistance) || maxSupportDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSupportDistance));
            }

            if (!float.IsFinite(skinWidth) || skinWidth < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth));
            }

            volumesById = new Dictionary<string, PlacementVolume>(
                volumes.Count,
                StringComparer.Ordinal);
            foreach (var volume in volumes)
            {
                var minimumHalfExtent = Mathf.Min(
                    volume.HalfExtents.x,
                    volume.HalfExtents.y,
                    volume.HalfExtents.z);
                if (skinWidth >= minimumHalfExtent ||
                    !volumesById.TryAdd(volume.ObjectId, volume))
                {
                    throw new ArgumentException(
                        "Placement volumes must have unique ids and be larger than the skin width.",
                        nameof(volumes));
                }
            }

            this.blockingLayerMask = blockingLayerMask;
            this.supportLayerMask = supportLayerMask;
            this.maxSupportDistance = maxSupportDistance;
            this.skinWidth = skinWidth;
        }

        public bool IsValid(string objectId, Pose pose)
        {
            if (string.IsNullOrWhiteSpace(objectId) ||
                !volumesById.TryGetValue(objectId.Trim(), out var volume) ||
                !IsFinite(pose.position) ||
                !IsFinite(pose.rotation))
            {
                return false;
            }

            var center = pose.position + (pose.rotation * volume.CenterOffset);
            var halfExtents = volume.HalfExtents - (Vector3.one * skinWidth);
            var overlapCount = Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    overlapBuffer,
                    pose.rotation,
                    blockingLayerMask,
                    QueryTriggerInteraction.Ignore);
            for (var index = 0; index < overlapCount; index++)
            {
                // Players are transient and use the same default physics layer
                // as props in the current scenes. They must not make a green
                // client preview fail only on authority.
                if (overlapBuffer[index].GetComponentInParent<CharacterController>() == null)
                {
                    return false;
                }
            }

            return Physics.Raycast(
                center,
                Vector3.down,
                volume.HalfExtents.y + maxSupportDistance,
                supportLayerMask,
                QueryTriggerInteraction.Ignore);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }
    }
}
