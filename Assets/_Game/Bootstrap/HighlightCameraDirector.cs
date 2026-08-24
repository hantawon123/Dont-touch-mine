using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Server.Match;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class HighlightCameraDirector
    {
        private readonly Transform cameraTransform;
        private readonly Transform fallbackTransform;
        private readonly IReadOnlyList<Transform> playerTargets;
        private readonly Dictionary<string, Transform> objectTargets;
        private readonly float closeDistance;
        private readonly float wideDistance;
        private readonly float height;
        private readonly float followSharpness;
        private readonly int collisionLayerMask;
        private Transform currentTarget;
        private float currentDistance;

        public HighlightCameraDirector(
            Transform cameraTransform,
            Transform fallbackTransform,
            IReadOnlyList<Transform> playerTargets,
            IReadOnlyList<SceneWorldObjectReference> objectTargets,
            float closeDistance = 5f,
            float wideDistance = 9f,
            float height = 3f,
            float followSharpness = 10f,
            int collisionLayerMask = 0)
        {
            this.cameraTransform = cameraTransform ??
                throw new ArgumentNullException(nameof(cameraTransform));
            this.fallbackTransform = fallbackTransform ??
                throw new ArgumentNullException(nameof(fallbackTransform));
            this.playerTargets = playerTargets ??
                throw new ArgumentNullException(nameof(playerTargets));
            if (objectTargets == null)
            {
                throw new ArgumentNullException(nameof(objectTargets));
            }

            if (closeDistance <= 0f || wideDistance <= 0f || height < 0f || followSharpness <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(closeDistance));
            }

            this.objectTargets = new Dictionary<string, Transform>(
                objectTargets.Count,
                StringComparer.Ordinal);
            foreach (var reference in objectTargets)
            {
                if (reference == null ||
                    reference.Target == null ||
                    string.IsNullOrWhiteSpace(reference.ObjectId) ||
                    !this.objectTargets.TryAdd(reference.ObjectId.Trim(), reference.Target))
                {
                    throw new ArgumentException(
                        "Camera targets must have unique ids and Transforms.",
                        nameof(objectTargets));
                }
            }

            this.closeDistance = closeDistance;
            this.wideDistance = wideDistance;
            this.height = height;
            this.followSharpness = followSharpness;
            this.collisionLayerMask = collisionLayerMask;
        }

        public Transform CurrentTarget => currentTarget;

        public bool Focus(HighlightCandidate highlight)
        {
            currentTarget = ResolveTarget(highlight.TargetId);
            if (currentTarget == null)
            {
                ApplyFallback();
                return false;
            }

            currentDistance = highlight.Type == HighlightType.TteTanMulgun ||
                              highlight.Type == HighlightType.LongestHidden
                ? wideDistance
                : closeDistance;
            ApplyTargetPose(1f);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (currentTarget == null)
            {
                ApplyFallback();
                return;
            }

            var t = 1f - Mathf.Exp(-followSharpness * deltaSeconds);
            ApplyTargetPose(t);
        }

        private Transform ResolveTarget(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return null;
            }

            var normalizedId = targetId.Trim();
            if (objectTargets.TryGetValue(normalizedId, out var objectTarget))
            {
                return objectTarget;
            }

            return int.TryParse(
                       normalizedId,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out var playerIndex) &&
                   playerIndex >= 0 &&
                   playerIndex < playerTargets.Count
                ? playerTargets[playerIndex]
                : null;
        }

        private void ApplyTargetPose(float t)
        {
            var focusPosition = currentTarget.position + (Vector3.up * 1f);
            var desiredPosition = focusPosition +
                                  (Vector3.back * currentDistance) +
                                  (Vector3.up * height);
            if (collisionLayerMask != 0 &&
                Physics.Linecast(
                    focusPosition,
                    desiredPosition,
                    out var hit,
                    collisionLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                desiredPosition = hit.point + (hit.normal * 0.2f);
            }

            var desiredRotation = Quaternion.LookRotation(
                focusPosition - desiredPosition,
                Vector3.up);
            cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(cameraTransform.position, desiredPosition, t),
                Quaternion.Slerp(cameraTransform.rotation, desiredRotation, t));
        }

        private void ApplyFallback()
        {
            cameraTransform.SetPositionAndRotation(
                fallbackTransform.position,
                fallbackTransform.rotation);
        }
    }
}
