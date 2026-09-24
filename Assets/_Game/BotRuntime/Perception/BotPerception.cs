using Game.Core.Bots;
using UnityEngine;

namespace Game.BotRuntime.Perception
{
    /// <summary>
    /// Checks what a bot may honestly see before information reaches a policy.
    /// Results are snapshots ("a photo"), never live references to the target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotPerception : MonoBehaviour
    {
        private const int MaximumRayHits = 32;
        private const int MaximumNearbyColliders = 64;

        [SerializeField]
        [Tooltip("Where the bot's eyes are. Empty uses this transform.")]
        private Transform eye;

        [SerializeField, Min(0.1f)]
        private float sightDistance = 12f;

        [SerializeField, Range(1f, 360f)]
        private float horizontalFieldOfView = 120f;

        [SerializeField]
        [Tooltip("Layers that can either be seen or block sight.")]
        private LayerMask sightLayers = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] rayHits = new RaycastHit[MaximumRayHits];
        private readonly Collider[] nearbyColliders =
            new Collider[MaximumNearbyColliders];

        public float SightDistance => sightDistance;
        public float HorizontalFieldOfView => horizontalFieldOfView;

        /// <summary>
        /// 눈 위치를 코드로 지정한다. 프리팹에 눈이 없는 봇에 실행부가 시야를 붙일 때 쓴다.
        /// null이면 이 Transform(발 위치)을 눈으로 쓴다.
        /// </summary>
        public void ConfigureEye(Transform eyeTransform)
        {
            eye = eyeTransform;
        }

        /// <summary>
        /// Looks through nearby candidates and returns only the closest one that
        /// passes the same range, angle, and wall-occlusion checks as TryObserve.
        /// </summary>
        public bool TryObserveClosest(
            LayerMask targetLayers,
            out BotSighting sighting)
        {
            sighting = default;
            var found = false;
            var eyeTransform = eye != null ? eye : transform;
            var count = Physics.OverlapSphereNonAlloc(
                eyeTransform.position,
                sightDistance,
                nearbyColliders,
                targetLayers,
                QueryTriggerInteraction.Ignore);

            var closestDistance = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var candidateCollider = nearbyColliders[index];
                if (candidateCollider == null)
                {
                    continue;
                }

                var candidate = candidateCollider.attachedRigidbody != null
                    ? candidateCollider.attachedRigidbody.transform
                    : candidateCollider.transform;
                if (IsPartOf(candidate, transform) ||
                    !TryObserve(candidate, out var candidateSighting))
                {
                    continue;
                }

                var distance = candidateSighting.Observation.NormalizedDistance;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                sighting = candidateSighting;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Returns a snapshot only when the target is in range, in front of the
        /// bot, and not hidden behind another collider.
        /// </summary>
        public bool TryObserve(
            Transform target,
            out BotSighting sighting)
        {
            sighting = default;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            var eyeTransform = eye != null ? eye : transform;
            var origin = eyeTransform.position;
            var targetPoint = ResolveTargetPoint(target);
            var offset = targetPoint - origin;
            var distance = offset.magnitude;

            if (distance <= Mathf.Epsilon || distance > sightDistance)
            {
                return false;
            }

            var flatForward = Vector3.ProjectOnPlane(
                eyeTransform.forward,
                Vector3.up);
            var flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (flatForward.sqrMagnitude <= Mathf.Epsilon ||
                flatOffset.sqrMagnitude <= Mathf.Epsilon ||
                Vector3.Angle(flatForward, flatOffset) >
                horizontalFieldOfView * 0.5f)
            {
                return false;
            }

            var direction = offset / distance;
            var hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                rayHits,
                distance,
                sightLayers,
                QueryTriggerInteraction.Ignore);

            var nearestTargetHit = float.PositiveInfinity;
            var nearestBlockerHit = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hitTransform = rayHits[index].collider.transform;
                if (IsPartOf(hitTransform, transform))
                {
                    continue;
                }

                if (IsPartOf(hitTransform, target))
                {
                    nearestTargetHit = Mathf.Min(
                        nearestTargetHit,
                        rayHits[index].distance);
                }
                else
                {
                    nearestBlockerHit = Mathf.Min(
                        nearestBlockerHit,
                        rayHits[index].distance);
                }
            }

            if (nearestBlockerHit < nearestTargetHit ||
                float.IsPositiveInfinity(nearestTargetHit))
            {
                return false;
            }

            var now = (float)BotSimClock.Now;
            sighting = new BotSighting(
                new BotSightObservation(
                    eyeTransform.InverseTransformDirection(direction),
                    Mathf.Clamp01(distance / sightDistance),
                    ResolveKindKey(target),
                    now),
                new BotSightTargetHandle(
                    ResolveTargetId(target),
                    targetPoint,
                    now));
            return true;
        }

        private static Vector3 ResolveTargetPoint(Transform target)
        {
            var targetCollider = target.GetComponentInChildren<Collider>();
            return targetCollider != null
                ? targetCollider.bounds.center
                : target.position;
        }

        /// <summary>
        /// Asks the target for its stable id through the Core question sheet. A
        /// plain object without one (test cubes, decoration) falls back to its
        /// instance id so the executor can still tell two targets apart.
        /// </summary>
        private static string ResolveTargetId(Transform target)
        {
            var sightTarget = target.GetComponentInParent<IBotSightTarget>();
            var id = sightTarget?.SightTargetId;
            return string.IsNullOrWhiteSpace(id)
                ? "instance:" + target.gameObject.GetInstanceID()
                : id;
        }

        private static string ResolveKindKey(Transform target)
        {
            var kind = target.GetComponentInParent<BotSightKind>();
            return kind != null ? kind.KindKey : string.Empty;
        }

        /// <summary>
        /// A collider belongs to <paramref name="root"/> only when it sits on the
        /// root itself or below it. An ancestor of the root (a shelf the item is
        /// parented under, a player holding it) is a separate body: treating it as
        /// the target let occluded items count as seen.
        /// </summary>
        private static bool IsPartOf(Transform candidate, Transform root) =>
            candidate == root || candidate.IsChildOf(root);
    }

    /// <summary>
    /// One sighting, split into the photo the policy may read and the handle only
    /// the executor may use. Neither half references the target object.
    /// </summary>
    public readonly struct BotSighting
    {
        public BotSighting(
            BotSightObservation observation,
            BotSightTargetHandle handle)
        {
            Observation = observation;
            Handle = handle;
        }

        public BotSightObservation Observation { get; }
        public BotSightTargetHandle Handle { get; }
    }

    /// <summary>
    /// The small, normalized note that may be handed to a rule or learned policy.
    /// Everything here is a number or a visible kind label as of ObservedTime.
    /// </summary>
    public readonly struct BotSightObservation
    {
        public BotSightObservation(
            Vector3 localDirection,
            float normalizedDistance,
            string kindKey,
            float observedTime)
        {
            LocalDirection = localDirection;
            NormalizedDistance = normalizedDistance;
            KindKey = kindKey ?? string.Empty;
            ObservedTime = observedTime;
        }

        /// <summary>Unit direction in the eye's local frame at the moment of sighting.</summary>
        public Vector3 LocalDirection { get; }

        /// <summary>Distance divided by sight distance, clamped to 0..1.</summary>
        public float NormalizedDistance { get; }

        /// <summary>Visible kind label from BotSightKind, or empty when unmarked.</summary>
        public string KindKey { get; }

        /// <summary>BotSimClock.Now when the photo was taken (Fusion simulation time while bound).</summary>
        public float ObservedTime { get; }
    }

    /// <summary>
    /// What the movement and pickup executor needs to act on a sighting later:
    /// which object it was and where it stood when seen. Never given to a policy.
    /// The executor must re-observe before acting; this position can be stale.
    /// </summary>
    public readonly struct BotSightTargetHandle
    {
        public BotSightTargetHandle(
            string targetId,
            Vector3 observedWorldPosition,
            float observedTime)
        {
            TargetId = targetId ?? string.Empty;
            ObservedWorldPosition = observedWorldPosition;
            ObservedTime = observedTime;
        }

        public string TargetId { get; }
        public Vector3 ObservedWorldPosition { get; }
        public float ObservedTime { get; }

        public bool IsValid => !string.IsNullOrEmpty(TargetId);
    }
}
