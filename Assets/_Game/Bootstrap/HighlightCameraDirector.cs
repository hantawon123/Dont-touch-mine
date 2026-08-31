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
        private readonly HashSet<Renderer> hiddenRenderers = new();
        private Transform currentTarget;
        private float currentDistance;
        private HighlightType currentType;
        private Vector3 overviewAnchor;
        private Vector3 shotDirection = Vector3.back;
        private Transform supportingPlayer;

        public HighlightCameraDirector(
            Transform cameraTransform,
            Transform fallbackTransform,
            IReadOnlyList<Transform> playerTargets,
            IReadOnlyList<SceneWorldObjectReference> objectTargets,
            float closeDistance = 8f,
            float wideDistance = 12f,
            float height = 4.5f,
            float followSharpness = 10f,
            int collisionLayerMask = Physics.DefaultRaycastLayers)
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
            ClearOccluders();
            currentType = highlight.Type;
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
            overviewAnchor = currentTarget.position;
            supportingPlayer = null;
            var nearest = 6f;
            foreach (var player in playerTargets)
            {
                if (player == null || player == currentTarget) continue;
                var distance = Vector3.Distance(player.position, currentTarget.position);
                if (distance < nearest) { nearest = distance; supportingPlayer = player; }
            }
            var actor = supportingPlayer != null ? supportingPlayer : currentTarget;
            shotDirection = supportingPlayer != null
                ? (-actor.forward + actor.right * 0.65f).normalized : Vector3.back;
            // Keep one side of the action through the shot; do not orbit with every player turn.
            ApplyTargetPose(1f);
            return true;
        }

        public void ClearOccluders()
        {
            foreach (var renderer in hiddenRenderers)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = false;
                }
            }

            hiddenRenderers.Clear();
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
            if (currentType == HighlightType.LongestHidden && currentTarget.gameObject.activeInHierarchy &&
                Vector3.Distance(currentTarget.position, overviewAnchor) > 4f)
                overviewAnchor = currentTarget.position;
            var subjectPosition = currentType == HighlightType.LongestHidden ? overviewAnchor : currentTarget.position;
            var focusPosition = subjectPosition + Vector3.up;
            var distance = currentDistance;
            if (currentType != HighlightType.LongestHidden && supportingPlayer != null &&
                Vector3.Distance(subjectPosition, supportingPlayer.position) < 6f)
            {
                focusPosition = (subjectPosition + supportingPlayer.position) * 0.5f + Vector3.up;
                distance = Mathf.Max(distance, Vector3.Distance(subjectPosition, supportingPlayer.position) * 1.5f);
            }
            var desiredPosition = focusPosition +
                                  (shotDirection * distance) +
                                  (Vector3.up * height);
            UpdateOccluders(focusPosition, desiredPosition);

            var desiredRotation = Quaternion.LookRotation(
                focusPosition - desiredPosition,
                Vector3.up);
            cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(cameraTransform.position, desiredPosition, t),
                Quaternion.Slerp(cameraTransform.rotation, desiredRotation, t));
        }

        private void ApplyFallback()
        {
            ClearOccluders();
            cameraTransform.SetPositionAndRotation(
                fallbackTransform.position,
                fallbackTransform.rotation);
        }

        private void UpdateOccluders(Vector3 focusPosition, Vector3 cameraPosition)
        {
            ClearOccluders();
            if (collisionLayerMask == 0)
            {
                return;
            }

            var direction = cameraPosition - focusPosition;
            var distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return;
            }

            foreach (var hit in Physics.SphereCastAll(
                         focusPosition,
                         0.5f,
                         direction / distance,
                         distance,
                         collisionLayerMask,
                         QueryTriggerInteraction.Ignore))
            {
                HideIfOccluding(hit.collider.GetComponent<Renderer>());
                HideIfOccluding(hit.collider.GetComponentInParent<Renderer>());
                foreach (var renderer in hit.collider.GetComponentsInChildren<Renderer>(true))
                {
                    HideIfOccluding(renderer);
                }
            }
        }

        private void HideIfOccluding(Renderer renderer)
        {
            if (renderer == null || renderer.forceRenderingOff || IsReplaySubject(renderer.transform))
            {
                return;
            }

            renderer.forceRenderingOff = true;
            hiddenRenderers.Add(renderer);
        }

        private bool IsReplaySubject(Transform candidate)
        {
            if (IsSameHierarchy(candidate, currentTarget))
            {
                return true;
            }

            foreach (var player in playerTargets)
            {
                if (IsSameHierarchy(candidate, player))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameHierarchy(Transform left, Transform right) =>
            left != null && right != null &&
            (left == right || left.IsChildOf(right) || right.IsChildOf(left));
    }
}
