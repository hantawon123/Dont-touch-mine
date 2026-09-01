using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Client.Cameras;
using Game.Server.Match;
using UnityEngine;

namespace Game.Bootstrap
{
    public enum HighlightShotSubject
    {
        Target,
        ActorAndTarget,
        Overview
    }

    public enum HighlightShotFraming
    {
        Wide,
        Medium,
        Close
    }

    public readonly struct HighlightShot
    {
        public HighlightShot(
            double startedAt,
            double endedAt,
            HighlightShotSubject subject,
            HighlightShotFraming framing,
            bool hardCut,
            bool emphasizesEvent = false)
        {
            if (!double.IsFinite(startedAt) || startedAt < 0d)
                throw new ArgumentOutOfRangeException(nameof(startedAt));
            if (!double.IsFinite(endedAt) || endedAt <= startedAt)
                throw new ArgumentOutOfRangeException(nameof(endedAt));
            StartedAt = startedAt;
            EndedAt = endedAt;
            Subject = subject;
            Framing = framing;
            HardCut = hardCut;
            EmphasizesEvent = emphasizesEvent;
        }

        public double StartedAt { get; }
        public double EndedAt { get; }
        public HighlightShotSubject Subject { get; }
        public HighlightShotFraming Framing { get; }
        public bool HardCut { get; }
        public bool EmphasizesEvent { get; }
    }

    public static class HighlightShotPlanner
    {
        private const double MinimumShotSeconds = 0.1d;

        public static HighlightShot[] Build(HighlightCandidate highlight)
        {
            if (highlight.PlaybackDurationSeconds <= 0d) return Array.Empty<HighlightShot>();
            return highlight.Type switch
            {
                HighlightType.TteTanMulgun => BuildMontage(highlight, HighlightShotSubject.ActorAndTarget),
                HighlightType.LongestHidden => BuildJourney(highlight),
                HighlightType.MostStunned => BuildMontage(highlight, HighlightShotSubject.ActorAndTarget),
                _ => BuildEvent(highlight),
            };
        }

        private static HighlightShot[] BuildEvent(HighlightCandidate highlight)
        {
            var duration = highlight.PlaybackDurationSeconds;
            var eventAt = PlaybackTimeOf(highlight, highlight.EventAt);
            var establishEnd = Math.Min(duration, 1.2d);
            var actionStart = Math.Clamp(eventAt - 1d, establishEnd, duration);
            var payoffEnd = Math.Min(duration, eventAt + 1.2d);
            var shots = new List<HighlightShot>(4);
            Add(shots, 0d, establishEnd,
                HighlightShotSubject.Overview, HighlightShotFraming.Wide, true);
            Add(shots, establishEnd, actionStart,
                HighlightShotSubject.ActorAndTarget, HighlightShotFraming.Medium, false);
            Add(shots, actionStart, payoffEnd,
                HighlightShotSubject.ActorAndTarget, HighlightShotFraming.Close, true, true);
            Add(shots, payoffEnd, duration,
                HighlightShotSubject.ActorAndTarget, HighlightShotFraming.Wide, false);
            return shots.ToArray();
        }

        private static HighlightShot[] BuildMontage(
            HighlightCandidate highlight,
            HighlightShotSubject subject)
        {
            var shots = new List<HighlightShot>(highlight.Segments.Count);
            var cursor = 0d;
            for (var index = 0; index < highlight.Segments.Count; index++)
            {
                var end = cursor + highlight.Segments[index].PlaybackDurationSeconds;
                var framing = index == 0
                    ? HighlightShotFraming.Wide
                    : index == highlight.Segments.Count - 1
                        ? HighlightShotFraming.Close
                        : HighlightShotFraming.Medium;
                Add(shots, cursor, end, subject, framing, true,
                    index == highlight.Segments.Count - 1);
                cursor = end;
            }

            return shots.ToArray();
        }

        private static HighlightShot[] BuildJourney(HighlightCandidate highlight)
        {
            var shots = new List<HighlightShot>(highlight.Segments.Count);
            var cursor = 0d;
            for (var index = 0; index < highlight.Segments.Count; index++)
            {
                var segment = highlight.Segments[index];
                var end = cursor + segment.PlaybackDurationSeconds;
                var isLast = index == highlight.Segments.Count - 1;
                Add(
                    shots,
                    cursor,
                    end,
                    segment.PlaybackSpeed > 1d
                        ? HighlightShotSubject.Overview
                        : HighlightShotSubject.ActorAndTarget,
                    isLast ? HighlightShotFraming.Close : HighlightShotFraming.Wide,
                    true,
                    isLast);
                cursor = end;
            }

            return shots.ToArray();
        }

        internal static double PlaybackTimeOf(HighlightCandidate highlight, double sourceTime)
        {
            var playbackTime = 0d;
            foreach (var segment in highlight.Segments)
            {
                if (sourceTime < segment.StartedAt) return playbackTime;
                if (sourceTime <= segment.EndedAt)
                    return playbackTime + (sourceTime - segment.StartedAt) / segment.PlaybackSpeed;
                playbackTime += segment.PlaybackDurationSeconds;
            }

            return highlight.PlaybackDurationSeconds;
        }

        private static void Add(
            ICollection<HighlightShot> shots,
            double start,
            double end,
            HighlightShotSubject subject,
            HighlightShotFraming framing,
            bool hardCut,
            bool emphasizesEvent = false)
        {
            if (end - start < MinimumShotSeconds) return;
            shots.Add(new HighlightShot(start, end, subject, framing, hardCut, emphasizesEvent));
        }
    }

    public static class HighlightPlaybackPacing
    {
        public static double Map(HighlightCandidate highlight, double presentationTime)
        {
            var duration = highlight.PlaybackDurationSeconds;
            if (!double.IsFinite(presentationTime) || presentationTime < 0d)
                throw new ArgumentOutOfRangeException(nameof(presentationTime));
            if (duration <= 0d) return 0d;

            var time = Math.Min(duration, presentationTime);
            var eventTime = HighlightShotPlanner.PlaybackTimeOf(highlight, highlight.EventAt);
            var slowStart = Math.Max(0d, eventTime - 0.5d);
            var slowEnd = Math.Min(duration, eventTime + 0.7d);
            var sourceStart = Math.Max(0d, eventTime - 0.25d);
            var sourceEnd = Math.Min(duration, eventTime + 0.35d);
            if (slowEnd - slowStart < 0.2d || sourceEnd <= sourceStart)
                return time;
            if (time <= slowStart)
                return Lerp(0d, sourceStart, Ratio(time, 0d, slowStart));
            if (time <= slowEnd)
                return Lerp(sourceStart, sourceEnd, Ratio(time, slowStart, slowEnd));
            return Lerp(sourceEnd, duration, Ratio(time, slowEnd, duration));
        }

        private static double Ratio(double value, double start, double end) =>
            end <= start ? 1d : Math.Clamp((value - start) / (end - start), 0d, 1d);

        private static double Lerp(double start, double end, double t) =>
            start + (end - start) * t;
    }

    public sealed class HighlightCameraDirector : IDisposable
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
        private readonly HighlightReplayCameraRig replayCameraRig;
        private Transform currentTarget;
        private float currentDistance;
        private HighlightType currentType;
        private Vector3 overviewAnchor;
        private Vector3 shotDirection = Vector3.back;
        private Transform supportingPlayer;
        private HighlightCandidate currentHighlight;
        private HighlightShot[] shots = Array.Empty<HighlightShot>();
        private int currentShotIndex = -1;

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
            replayCameraRig = HighlightReplayCameraRig.TryCreate(cameraTransform, collisionLayerMask);
        }

        public Transform CurrentTarget => currentTarget;
        public HighlightShot? CurrentShot => currentShotIndex >= 0 && currentShotIndex < shots.Length
            ? shots[currentShotIndex]
            : null;

        public bool Focus(HighlightCandidate highlight)
        {
            ClearOccluders();
            currentType = highlight.Type;
            currentHighlight = highlight;
            shots = HighlightShotPlanner.Build(highlight);
            currentShotIndex = -1;
            currentTarget = ResolveTarget(highlight.TargetId);
            if (currentTarget == null)
            {
                ApplyFallback();
                return false;
            }

            overviewAnchor = currentTarget.position;
            if (shots.Length == 0)
            {
                ApplyFallback();
                return false;
            }

            SetPlaybackTime(0d);
            return true;
        }

        public void SetPlaybackTime(double playbackTime)
        {
            if (!double.IsFinite(playbackTime) || playbackTime < 0d)
                throw new ArgumentOutOfRangeException(nameof(playbackTime));
            if (shots.Length == 0) return;
            var next = shots.Length - 1;
            for (var index = 0; index < shots.Length; index++)
            {
                if (playbackTime < shots[index].EndedAt)
                {
                    next = index;
                    break;
                }
            }

            if (next == currentShotIndex) return;
            currentShotIndex = next;
            ApplyShot(shots[next]);
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

        private Transform ResolvePlayer(int playerIndex) =>
            playerIndex >= 0 && playerIndex < playerTargets.Count
                ? playerTargets[playerIndex]
                : null;

        private void ApplyShot(HighlightShot shot)
        {
            currentTarget = ResolveTarget(currentHighlight.TargetId);
            supportingPlayer = shot.Subject == HighlightShotSubject.Overview
                ? null
                : ResolvePlayer(currentHighlight.ActorPlayerIndex);
            if (supportingPlayer == currentTarget)
                supportingPlayer = ResolvePlayer(currentHighlight.SecondaryPlayerIndex);
            if (supportingPlayer == null && shot.Subject == HighlightShotSubject.ActorAndTarget)
                supportingPlayer = FindNearestPlayer(currentTarget);

            currentDistance = shot.Framing switch
            {
                HighlightShotFraming.Wide => wideDistance,
                HighlightShotFraming.Medium => (closeDistance + wideDistance) * 0.5f,
                _ => closeDistance,
            };
            var actor = supportingPlayer != null ? supportingPlayer : currentTarget;
            var side = currentShotIndex % 2 == 0 ? 0.65f : -0.65f;
            shotDirection = supportingPlayer != null
                ? (-actor.forward + actor.right * side).normalized
                : Vector3.back;
            replayCameraRig?.SetTargets(currentTarget, supportingPlayer);
            ApplyTargetPose(shot.HardCut ? 1f : 0f);
        }

        private Transform FindNearestPlayer(Transform target)
        {
            Transform nearestPlayer = null;
            var nearestDistance = 6f;
            foreach (var player in playerTargets)
            {
                if (player == null || player == target) continue;
                var distance = Vector3.Distance(player.position, target.position);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestPlayer = player;
            }

            return nearestPlayer;
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
            var desiredRotation = Quaternion.LookRotation(
                focusPosition - desiredPosition,
                Vector3.up);
            UpdateOccluders(focusPosition, desiredPosition);
            if (replayCameraRig != null)
            {
                replayCameraRig.SetPose(
                    desiredPosition,
                    desiredRotation,
                    t,
                    FramingSizeOf(CurrentShot?.Framing ?? HighlightShotFraming.Medium),
                    t >= 1f);
                return;
            }

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

        private static float FramingSizeOf(HighlightShotFraming framing) => framing switch
        {
            HighlightShotFraming.Wide => 0.45f,
            HighlightShotFraming.Medium => 0.6f,
            _ => 0.75f,
        };

        public void Dispose()
        {
            ClearOccluders();
            replayCameraRig?.Dispose();
        }
    }
}
